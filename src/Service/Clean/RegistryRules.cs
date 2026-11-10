using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace WinMemoryCleaner
{
    internal enum RegistryMutation { Removed, Skipped, Failed }

    /// <summary>Explicit registry rules, with export, revalidation and post-delete verification.</summary>
    internal static class RegistryRules
    {
        private const string Uninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        private const string AppPaths = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";
        private const string MuiCache = @"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache";
        private const string GameDvr = @"System\GameConfigStore\Children";
        private static readonly string[][] UserValues =
        {
            new[] { @"Software\Discord\Modules\discord_overlay_sdk", "Path_x86", "Path_x64" },
            new[] { @"Software\BraveSoftware\Promo", "StubInstallerPath" },
            new[] { @"Software\iZotope\RX11\NeuronPlugin\VSTPlugin64Folders", "VSTPluginFolder1" },
            new[] { @"Software\Microsoft\OneDrive", "ODPatcherRunCurrentVersionFile" },
            new[] { @"Software\SyncEngines\Providers\OneDrive", "DisplayNameResource", "IconResource" },
            new[] { @"Software\DAUM\PotPlayerMini64\SimpleOpen", "DefPathChild" },
            new[] { @"Software\SplitCam", "Last Project" },
            new[] { @"Software\StartIsBack", "OrbBitmap" }
        };
        private static readonly string[][] MachineValues =
        {
            new[] { @"SOFTWARE\EpicGames\Unreal Engine\4.0", "InstalledDirectory" },
            new[] { @"SOFTWARE\SmokePatch\SP Football Life 2027", "InstallPath" },
            new[] { @"SOFTWARE\iZotope\RX11CONNECT", "FactoryPresetFolder" },
            new[] { @"SOFTWARE\iZotope\RX11MONITOR", "FactoryPresetFolder" },
            new[] { @"SOFTWARE\iZotope\RX11REPAIRASSIST", "FactoryPresetFolder" },
            new[] { @"SOFTWARE\iZotope\RX11SPECTRALDE-NOISE", "FactoryPresetFolder" }
        };
        // HKLM\Software\Clients is shared between the WOW64 views on supported Windows.
        private static readonly string[][] SharedMachineValues =
        {
            new[] { @"SOFTWARE\Clients\Media\AIMP", "IconLibrary" },
            new[] { @"SOFTWARE\Clients\Media\AIMP\Capabilities", "ApplicationIcon" },
            new[] { @"SOFTWARE\Clients\Media\AIMP\InstallInfo", "HideIconsCommand", "ReinstallCommand", "ShowIconsCommand" }
        };

        internal static void Run(bool apply, string backup, CleanTaskResult result, CancellationToken token)
        {
            var native = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32;
            using (var user = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, native))
            {
                // These HKCU paths are shared: scan only once to avoid duplicate estimates.
                AppPathKeys(user, "HKCU", native, apply, backup, result, token);
                UninstallKeys(user, "HKCU", native, apply, backup, result, token);
                Values(user, "HKCU", native, UserValues, apply, backup, result, token);
                ValuesMatching(user, "HKCU", MuiCache, native,
                    name => RegistryCleanTask.MuiExecutable(name) != null,
                    (key, name) => MissingReference(RegistryCleanTask.MuiExecutable(name)),
                    "MUICache", apply, backup, result, token);
                ValuesMatching(user, "HKCU", @"Software\DAUM\PotPlayerMini64\RememberFiles", native,
                    name => Regex.IsMatch(name, @"^\d+$"),
                    (key, name) => MissingReference(key.GetValue(name) as string),
                    "Referencia inexistente", apply, backup, result, token);
                Children(user, "HKCU", GameDvr, native, apply, backup, result, token,
                    key => MissingReference(key.GetValue("MatchedExeFullPath") as string), "GameDVR");
            }
            foreach (var view in Environment.Is64BitOperatingSystem
                ? new[] { RegistryView.Registry64, RegistryView.Registry32 } : new[] { RegistryView.Registry32 })
            {
                using (var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                {
                    AppPathKeys(machine, "HKLM", view, apply, backup, result, token);
                    UninstallKeys(machine, "HKLM", view, apply, backup, result, token);
                    Values(machine, "HKLM", view, MachineValues, apply, backup, result, token);
                    if (view == native) Values(machine, "HKLM", view, SharedMachineValues, apply, backup, result, token);
                }
            }
        }

        internal static string ReferencePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();
            if (value.StartsWith("@", StringComparison.Ordinal)) value = value.Substring(1);
            if (value.StartsWith("\"", StringComparison.Ordinal))
            {
                var end = value.IndexOf('"', 1);
                if (end <= 1) return null;
                value = value.Substring(1, end - 1);
            }
            else
            {
                value = Regex.Replace(value, @",\s*-?\d+\s*$", string.Empty);
                var exe = Regex.Match(value, @"\.exe(?=\s|$)", RegexOptions.IgnoreCase);
                if (exe.Success) value = value.Substring(0, exe.Index + 4);
            }
            value = Environment.ExpandEnvironmentVariables(value);
            return value.Length > 3 && char.IsLetter(value[0]) && value[1] == ':' && value[2] == '\\'
                && value.IndexOfAny(Path.GetInvalidPathChars()) < 0 && !value.Contains("%") ? value : null;
        }

        private static bool MissingReference(string value)
        {
            var path = ReferencePath(value);
            return MissingPath.IsMissing(path) && MissingPath.IsMissing(ShortcutCleanTask.SwapProgramFiles(path))
                && MissingPath.IsMissing(AlternativeSystemPath(path));
        }

        internal static string AlternativeSystemPath(string path)
        {
            if (path == null || !Environment.Is64BitOperatingSystem) return path;
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var native = Path.Combine(windows, "System32") + "\\";
            var wow = Path.Combine(windows, "SysWOW64") + "\\";
            if (path.StartsWith(native, StringComparison.OrdinalIgnoreCase)) return wow + path.Substring(native.Length);
            if (path.StartsWith(wow, StringComparison.OrdinalIgnoreCase)) return native + path.Substring(wow.Length);
            return path;
        }

        internal static bool ProtectedUninstall(Func<string, object> read)
        {
            if (string.IsNullOrWhiteSpace(read("DisplayName") as string)) return true;
            foreach (var flag in new[] { "WindowsInstaller", "SystemComponent", "NoRemove" })
            {
                var value = read(flag);
                if (value != null && !string.Equals(Convert.ToString(value, CultureInfo.InvariantCulture), "0", StringComparison.Ordinal))
                    return true;
            }
            return !string.IsNullOrEmpty(read("ParentKeyName") as string) || !string.IsNullOrEmpty(read("ReleaseType") as string);
        }

        internal static bool StaleUninstall(string name, string location, string command,
            Func<string, bool> missing, Func<string, bool> accessibleDirectory)
        {
            var directory = ReferencePath(location);
            if (directory == null || !missing(directory)) return false;
            if ((name ?? string.Empty).StartsWith("Steam App ", StringComparison.OrdinalIgnoreCase))
            {
                var id = Regex.Match(name, @"^Steam App ([1-9][0-9]*)$", RegexOptions.IgnoreCase);
                var install = Regex.Match(directory.TrimEnd('\\'),
                    @"^(?<root>[A-Za-z]:\\.+)\\steamapps\\common\\[^\\]+$", RegexOptions.IgnoreCase);
                if (!id.Success || !install.Success) return false;
                var apps = Path.Combine(install.Groups["root"].Value, "steamapps");
                return accessibleDirectory(apps) && missing(Path.Combine(apps, "appmanifest_" + id.Groups[1].Value + ".acf"));
            }
            var exe = ReferencePath(RegistryCleanTask.AppPathExecutable(command));
            // An MSI entry is never removed by this rule, even if its metadata is incomplete.
            return exe != null && exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(Path.GetFileName(exe), "msiexec.exe", StringComparison.OrdinalIgnoreCase)
                && missing(exe);
        }

        private static bool CanList(string path)
        {
            try { Directory.GetFileSystemEntries(path); return true; }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        private static void UninstallKeys(RegistryKey hive, string hiveName, RegistryView view, bool apply,
            string backup, CleanTaskResult result, CancellationToken token)
        {
            Children(hive, hiveName, Uninstall, view, apply, backup, result, token, key =>
                !ProtectedUninstall(name => key.GetValue(name)) &&
                StaleUninstall(key.Name.Substring(key.Name.LastIndexOf('\\') + 1),
                    key.GetValue("InstallLocation") as string, key.GetValue("UninstallString") as string,
                    MissingReference, CanList), "Desinstalación huérfana");
        }

        private static void AppPathKeys(RegistryKey hive, string hiveName, RegistryView view, bool apply,
            string backup, CleanTaskResult result, CancellationToken token)
        {
            Children(hive, hiveName, AppPaths, view, apply, backup, result, token, key =>
            {
                var exe = RegistryCleanTask.AppPathExecutable(key.GetValue(null) as string);
                return exe != null && exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && MissingReference(exe);
            }, "App Paths");
        }

        private static void Children(RegistryKey hive, string hiveName, string parentPath, RegistryView view,
            bool apply, string backup, CleanTaskResult result, CancellationToken token,
            Func<RegistryKey, bool> candidate, string reason)
        {
            try
            {
                using (var parent = hive.OpenSubKey(parentPath))
                {
                    if (parent == null) return;
                    foreach (var name in parent.GetSubKeyNames())
                    {
                        token.ThrowIfCancellationRequested();
                        var child = parentPath + "\\" + name;
                        Process(hive, hiveName, child, null, view, candidate, reason, apply, backup, result, token);
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { result.Skipped++; result.Detail(hiveName + "\\" + parentPath + ": " + e.Message); }
        }

        private static void Values(RegistryKey hive, string hiveName, RegistryView view, string[][] rules,
            bool apply, string backup, CleanTaskResult result, CancellationToken token)
        {
            foreach (var rule in rules)
                ValuesMatching(hive, hiveName, rule[0], view,
                    name => rule.Skip(1).Contains(name, StringComparer.OrdinalIgnoreCase),
                    (key, name) => MissingReference(key.GetValue(name) as string),
                    "Referencia inexistente", apply, backup, result, token);
        }

        private static void ValuesMatching(RegistryKey hive, string hiveName, string path, RegistryView view,
            Func<string, bool> match, Func<RegistryKey, string, bool> candidate, string reason,
            bool apply, string backup, CleanTaskResult result, CancellationToken token)
        {
            try
            {
                using (var key = hive.OpenSubKey(path))
                {
                    if (key == null) return;
                    foreach (var value in key.GetValueNames().Where(match))
                    {
                        token.ThrowIfCancellationRequested();
                        var capturedName = value;
                        Process(hive, hiveName, path, capturedName, view, k => candidate(k, capturedName),
                            reason, apply, backup, result, token);
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { result.Skipped++; result.Detail(hiveName + "\\" + path + ": " + e.Message); }
        }

        internal static void Process(RegistryKey hive, string hiveName, string path, string valueName,
            RegistryView view, Func<RegistryKey, bool> candidate, string reason, bool apply, string backup,
            CleanTaskResult result, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var label = hiveName + "\\" + path + (valueName == null ? string.Empty : " → " + valueName)
                + " [" + (view == RegistryView.Registry32 ? "32" : "64") + "] · " + Tr.Get(reason);
            if (path.IndexOf('"') >= 0 || path.IndexOf('\0') >= 0) { result.Skipped++; return; }
            try
            {
                string expected;
                using (var key = hive.OpenSubKey(path))
                {
                    if (key == null || !candidate(key)) return;
                    expected = Snapshot(key);
                }
                if (expected == null) { result.Skipped++; return; }
                result.Progress(Tr.Format("Comprobando registro: {0}", label));
                if (!apply) { result.Entries++; result.Detail(label); return; }
                var outcome = ApplyWithBackup(expected,
                    () => { token.ThrowIfCancellationRequested(); using (var k = hive.OpenSubKey(path)) return k == null ? null : Snapshot(k); },
                    () => { using (var k = hive.OpenSubKey(path)) return k != null && candidate(k); },
                    () => RegistryCleanTask.Export(hiveName + "\\" + path, backup,
                        (view == RegistryView.Registry32 ? "r32-" : "r64-") + Guid.NewGuid().ToString("N"), result, view),
                    () => Remove(hive, path, valueName, expected, candidate));
                if (outcome == RegistryMutation.Removed) { result.Entries++; result.Detail(label); }
                else if (outcome == RegistryMutation.Skipped) { result.Skipped++; result.Detail(Tr.Get("Registro conservado tras cambiar su estado: ") + label); }
                else result.Failed++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { result.Skipped++; result.Detail(label + ": " + e.Message); }
        }

        internal static RegistryMutation ApplyWithBackup(string expected, Func<string> snapshot,
            Func<bool> eligible, Func<bool> backup, Func<bool> remove)
        {
            if (expected == null || snapshot() != expected || !eligible()) return RegistryMutation.Skipped;
            if (!backup()) return RegistryMutation.Failed;
            if (snapshot() != expected || !eligible()) return RegistryMutation.Skipped;
            return remove() ? RegistryMutation.Removed : RegistryMutation.Failed;
        }

        private static bool Remove(RegistryKey hive, string path, string valueName, string expected, Func<RegistryKey, bool> candidate)
        {
            using (var key = hive.OpenSubKey(path, true))
            {
                if (key == null || Snapshot(key) != expected || !candidate(key)) return false;
                if (valueName != null)
                {
                    key.DeleteValue(valueName, false);
                    return !key.GetValueNames().Contains(valueName, StringComparer.OrdinalIgnoreCase);
                }
            }
            var split = path.LastIndexOf('\\');
            if (split < 1) return false;
            using (var parent = hive.OpenSubKey(path.Substring(0, split), true))
            {
                if (parent == null) return false;
                parent.DeleteSubKeyTree(path.Substring(split + 1));
            }
            using (var check = hive.OpenSubKey(path)) return check == null;
        }

        // Structural snapshot, including subkeys and typed values, catches changes since export.
        // Oversized/unreadable entries are left untouched.
        internal static string Snapshot(RegistryKey key)
        {
            var text = new StringBuilder();
            var budget = 1024;
            return AppendSnapshot(key, text, 0, ref budget) ? text.ToString() : null;
        }

        private static bool AppendSnapshot(RegistryKey key, StringBuilder text, int depth, ref int budget)
        {
            if (depth > 8 || --budget < 0) return false;
            foreach (var name in key.GetValueNames().OrderBy(n => n, StringComparer.Ordinal))
            {
                if (--budget < 0) return false;
                var kind = key.GetValueKind(name);
                var data = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (data is byte[] && ((byte[])data).Length > 1024 * 1024) return false;
                if (data is string && ((string)data).Length > 1024 * 1024) return false;
                string encoded;
                if (data is byte[]) encoded = Convert.ToBase64String((byte[])data);
                else if (data is string[]) encoded = string.Join(string.Empty, ((string[])data).Select(s => s.Length + ":" + s).ToArray());
                else encoded = Convert.ToString(data, CultureInfo.InvariantCulture) ?? string.Empty;
                text.Append(name.Length).Append(':').Append(name).Append(':').Append((int)kind)
                    .Append(':').Append(encoded.Length).Append(':').Append(encoded).Append(';');
            }
            foreach (var name in key.GetSubKeyNames().OrderBy(n => n, StringComparer.Ordinal))
            {
                text.Append('{').Append(name.Length).Append(':').Append(name);
                using (var sub = key.OpenSubKey(name))
                    if (sub == null || !AppendSnapshot(sub, text, depth + 1, ref budget)) return false;
                text.Append('}');
            }
            return text.Length < 1024 * 1024;
        }
    }
}
