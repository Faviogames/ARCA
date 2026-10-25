using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace WinMemoryCleaner
{
    /// <summary>Temporary files and broken shortcuts share one visible category.</summary>
    internal sealed class TemporaryCleanTask : ICleanTask
    {
        private readonly ICleanTask _files;
        private readonly ICleanTask _shortcuts;

        public TemporaryCleanTask() : this(new FileCleanTask(CleanCategory.Temp, r => CleanCatalog.Temp()), new ShortcutCleanTask()) { }

        internal TemporaryCleanTask(ICleanTask files, ICleanTask shortcuts) { _files = files; _shortcuts = shortcuts; }

        public CleanCategory Category { get { return CleanCategory.Temp; } }

        public void Run(bool apply, CleanTaskResult result, CancellationToken token)
        {
            foreach (var task in new[] { _files, _shortcuts })
            {
                token.ThrowIfCancellationRequested();
                try { task.Run(apply, result, token); }
                catch (OperationCanceledException) { throw; }
                catch (Exception e) { result.Failed++; result.AddNote(CleanText.Name(task.Category) + ": " + e.Message); }
            }
        }
    }

    /// <summary>A category made only of folder targets.</summary>
    internal sealed class FileCleanTask : ICleanTask
    {
        private readonly Func<CleanTaskResult, IList<CleanTarget>> _targets;

        public FileCleanTask(CleanCategory category, Func<CleanTaskResult, IList<CleanTarget>> targets)
        {
            Category = category; _targets = targets;
        }

        public CleanCategory Category { get; private set; }

        public void Run(bool apply, CleanTaskResult result, CancellationToken token)
        {
            foreach (var target in _targets(result))
            {
                token.ThrowIfCancellationRequested();
                var before = result.Bytes;
                FileCleaner.Clean(target, apply, result, token);
                if (result.Bytes > before) result.Detail(CleanText.Size(result.Bytes - before) + "  " + target.Path);
            }
        }
    }

    /// <summary>Where each file category looks. Paths are built at run time from the current user.</summary>
    internal static class CleanCatalog
    {
        private static readonly TimeSpan Recent = TimeSpan.FromHours(48);
        private static readonly TimeSpan OldDump = TimeSpan.FromDays(7);

        private static string Local { get { return Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData); } }
        private static string Roaming { get { return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); } }
        private static string Common { get { return Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData); } }
        private static string Windows { get { return Environment.GetFolderPath(Environment.SpecialFolder.Windows); } }
        private static string LocalLow { get { return Path.Combine(Path.GetDirectoryName(Local) ?? string.Empty, "LocalLow"); } }

        public static IList<CleanTarget> Temp()
        {
            return new List<CleanTarget>
            {
                new CleanTarget(Path.GetTempPath(), Recent),
                new CleanTarget(Path.Combine(Windows, "Temp"), Recent),
                new CleanTarget(Path.Combine(Common, @"Microsoft\Windows\WER\ReportArchive"), Recent),
                new CleanTarget(Path.Combine(Common, @"Microsoft\Windows\WER\ReportQueue"), Recent),
                new CleanTarget(Path.Combine(Common, @"Microsoft\Windows\WER\Temp"), Recent),
                new CleanTarget(Path.Combine(Local, @"Microsoft\Windows\WER\ReportArchive"), Recent),
                new CleanTarget(Path.Combine(Local, @"Microsoft\Windows\WER\ReportQueue"), Recent),
                new CleanTarget(Path.Combine(Local, "CrashDumps"), OldDump),
                new CleanTarget(Path.Combine(Windows, "Minidump"), OldDump),
                new CleanTarget(Path.Combine(Windows, "LiveKernelReports"), OldDump, "*.dmp"),
                new CleanTarget(Windows, OldDump, "MEMORY.DMP", false)
            };
        }

        // Browser data folders: only the disposable cache subfolders are touched, never
        // profiles, cookies, history, passwords or offline site storage.
        private static readonly string[][] _chromium =
        {
            new[] { "chrome", "Google Chrome", @"Google\Chrome\User Data" },
            new[] { "msedge", "Microsoft Edge", @"Microsoft\Edge\User Data" },
            new[] { "brave", "Brave", @"BraveSoftware\Brave-Browser\User Data" },
            new[] { "vivaldi", "Vivaldi", @"Vivaldi\User Data" },
            new[] { "chromium", "Chromium", @"Chromium\User Data" }
        };

        // Cookies, Local Storage and the login session stay; only caches (including favourite GIFs) go.
        private static readonly string[][] _discord =
        {
            new[] { "Discord", "Discord", "discord" },
            new[] { "DiscordPTB", "Discord PTB", "discordptb" },
            new[] { "DiscordCanary", "Discord Canary", "discordcanary" }
        };

        public static IList<CleanTarget> Caches(CleanTaskResult result)
        {
            return Caches(result, IsRunning);
        }

        internal static IList<CleanTarget> Caches(CleanTaskResult result, Func<string, bool> isRunning)
        {
            var targets = new List<CleanTarget>
            {
                new CleanTarget(Path.Combine(Local, @"Microsoft\Windows\INetCache"), TimeSpan.Zero),
                new CleanTarget(Path.Combine(Local, @"Microsoft\Windows\Explorer"), TimeSpan.Zero, "thumbcache_*.db", false)
            };
            foreach (var browser in _chromium)
            {
                var data = Path.Combine(Local, browser[2]);
                if (!Directory.Exists(data)) continue;
                if (isRunning(browser[0])) { SkipBrowser(result, browser[1]); continue; }
                foreach (var profile in Profiles(data))
                    foreach (var cache in new[] { "Cache", "Code Cache", "GPUCache" })
                        targets.Add(new CleanTarget(Path.Combine(profile, cache), TimeSpan.Zero));
            }
            foreach (var opera in new[] { new[] { "opera", "Opera", @"Opera Software\Opera Stable" }, new[] { "opera_gx", "Opera GX", @"Opera Software\Opera GX Stable" } })
            {
                var cache = Path.Combine(Local, opera[2]);
                if (!Directory.Exists(cache)) continue;
                if (isRunning(opera[0])) { SkipBrowser(result, opera[1]); continue; }
                targets.Add(new CleanTarget(Path.Combine(cache, "Cache"), TimeSpan.Zero));
                targets.Add(new CleanTarget(Path.Combine(cache, "Code Cache"), TimeSpan.Zero));
            }
            // Discord is Electron: same disposable cache folders, but it keeps running in the tray.
            foreach (var discord in _discord)
            {
                var data = Path.Combine(Roaming, discord[2]);
                if (!Directory.Exists(data)) continue;
                if (isRunning(discord[0])) { SkipBrowser(result, discord[1]); continue; }
                foreach (var cache in new[] { "Cache", "Code Cache", "GPUCache" })
                    targets.Add(new CleanTarget(Path.Combine(data, cache), TimeSpan.Zero));
            }
            var firefox = Path.Combine(Local, @"Mozilla\Firefox\Profiles");
            if (Directory.Exists(firefox))
            {
                if (isRunning("firefox")) SkipBrowser(result, "Firefox");
                else foreach (var profile in SafeDirectories(firefox)) targets.Add(new CleanTarget(Path.Combine(profile, "cache2"), TimeSpan.Zero));
            }
            return targets;
        }

        public static IList<CleanTarget> Shaders()
        {
            return new List<CleanTarget>
            {
                new CleanTarget(Path.Combine(Local, @"NVIDIA\DXCache"), TimeSpan.Zero),
                new CleanTarget(Path.Combine(Local, @"NVIDIA\GLCache"), TimeSpan.Zero),
                new CleanTarget(Path.Combine(Local, @"NVIDIA Corporation\NV_Cache"), TimeSpan.Zero),
                new CleanTarget(Path.Combine(Common, @"NVIDIA Corporation\NV_Cache"), TimeSpan.Zero),
                new CleanTarget(Path.Combine(LocalLow, @"NVIDIA\PerDriverVersion\DXCache"), TimeSpan.Zero),
                new CleanTarget(Path.Combine(LocalLow, @"NVIDIA\PerDriverVersion\GLCache"), TimeSpan.Zero),
                new CleanTarget(Path.Combine(Local, "D3DSCache"), TimeSpan.Zero),
                new CleanTarget(Path.Combine(Local, @"AMD\DxCache"), TimeSpan.Zero),
                new CleanTarget(Path.Combine(Local, @"AMD\DxcCache"), TimeSpan.Zero),
                new CleanTarget(Path.Combine(Local, @"AMD\VkCache"), TimeSpan.Zero),
                new CleanTarget(Path.Combine(Local, @"AMD\GLCache"), TimeSpan.Zero),
                new CleanTarget(Path.Combine(LocalLow, @"Intel\ShaderCache"), TimeSpan.Zero)
            };
        }

        private static void SkipBrowser(CleanTaskResult result, string name)
        {
            result.Skipped++;
            result.AddNote(Tr.Format("{0} abierto", name));
        }

        private static IEnumerable<string> Profiles(string userData)
        {
            return SafeDirectories(userData).Where(d =>
            {
                var name = Path.GetFileName(d);
                return name == "Default" || name.StartsWith("Profile ", StringComparison.Ordinal) || name == "Guest Profile";
            });
        }

        private static IEnumerable<string> SafeDirectories(string path)
        {
            try { return Directory.GetDirectories(path); }
            catch (Exception) { return new string[0]; }
        }

        private static bool IsRunning(string name)
        {
            var processes = Process.GetProcessesByName(name);
            foreach (var p in processes) p.Dispose();
            return processes.Length > 0;
        }
    }
}
