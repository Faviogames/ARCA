using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace WinMemoryCleaner
{
    /// <summary>Decides whether a referenced path is really gone, not just unreachable.</summary>
    internal static class MissingPath
    {
        public static bool IsMissing(string path)
        {
            return IsMissing(path, p => File.Exists(p) || Directory.Exists(p), DriveTypeOf);
        }

        /// <summary>
        /// Only absolute paths on a present, fixed local drive can be declared missing. Network
        /// shares, removable/unplugged drives, empty or relative targets always count as present.
        /// </summary>
        internal static bool IsMissing(string path, Func<string, bool> exists, Func<string, DriveType> driveType)
        {
            if (string.IsNullOrEmpty(path)) return false;
            path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
            if (path.Length < 4 || path[1] != ':' || path[2] != '\\' || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0) return false;
            if (driveType(path.Substring(0, 3)) != DriveType.Fixed) return false;
            return !exists(path);
        }

        private static DriveType DriveTypeOf(string root)
        {
            try
            {
                var drive = new DriveInfo(root);
                return drive.IsReady ? drive.DriveType : DriveType.Unknown;
            }
            catch (Exception) { return DriveType.Unknown; }
        }
    }

    /// <summary>Moves broken shortcuts to the recycle bin, so each one can still be restored.</summary>
    internal sealed class ShortcutCleanTask : ICleanTask
    {
        public CleanCategory Category { get { return CleanCategory.Shortcuts; } }

        public void Run(bool apply, CleanTaskResult result, CancellationToken token)
        {
            var roots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
            };
            foreach (var root in roots.Where(r => !string.IsNullOrEmpty(r)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var link in Links(root, root != roots[0] && root != roots[1]))
                {
                    token.ThrowIfCancellationRequested();
                    string target;
                    try { target = ShellLink.Target(link); }
                    catch (Exception) { result.Skipped++; continue; }
                    if (!MissingPath.IsMissing(target) || !MissingPath.IsMissing(SwapProgramFiles(target))) continue;
                    result.Detail(link + "  →  " + target);
                    if (!apply) { result.Entries++; continue; }
                    if (Recycle(link) && !File.Exists(link)) result.Entries++;
                    else result.Failed++;
                }
            }
        }

        internal static bool Recycle(string file)
        {
            var operation = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = file + "\0\0",
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI
            };
            return SHFileOperationW(ref operation) == 0 && !operation.fAnyOperationsAborted;
        }

        private const int FO_DELETE = 3;
        private const short FOF_SILENT = 0x4, FOF_NOCONFIRMATION = 0x10, FOF_ALLOWUNDO = 0x40, FOF_NOERRORUI = 0x400;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public int wFunc;
            public string pFrom;
            public string pTo;
            public short fFlags;
            [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHFileOperationW(ref SHFILEOPSTRUCT operation);

        // Links written by 32-bit installers can name the other Program Files folder.
        internal static string SwapProgramFiles(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            const string x86 = @"\Program Files (x86)\", x64 = @"\Program Files\";
            var i = path.IndexOf(x86, StringComparison.OrdinalIgnoreCase);
            if (i >= 0) return path.Substring(0, i) + x64 + path.Substring(i + x86.Length);
            i = path.IndexOf(x64, StringComparison.OrdinalIgnoreCase);
            return i >= 0 ? path.Substring(0, i) + x86 + path.Substring(i + x64.Length) : path;
        }

        private static IEnumerable<string> Links(string root, bool recursive)
        {
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var dir = pending.Pop();
                string[] files = new string[0], dirs = new string[0];
                try
                {
                    files = Directory.GetFiles(dir, "*.lnk");
                    if (recursive) dirs = Directory.GetDirectories(dir);
                }
                catch (Exception) { }
                foreach (var file in files) yield return file;
                foreach (var sub in dirs)
                {
                    try { if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) != 0) continue; }
                    catch (Exception) { continue; }
                    pending.Push(sub);
                }
            }
        }
    }

    /// <summary>Reads a .lnk target path.</summary>
    internal static class ShellLink
    {
        // GetPath never searches; only IShellLink::Resolve would, and it is not called.
        public static string Target(string file)
        {
            var link = (IShellLinkW)new CShellLink();
            try
            {
                ((IPersistFile)link).Load(file, 0);
                var buffer = new StringBuilder(1024);
                link.GetPath(buffer, buffer.Capacity, IntPtr.Zero, 0);
                return buffer.ToString();
            }
            finally { Marshal.ReleaseComObject(link); }
        }

        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class CShellLink { }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, int fFlags);
            // Remaining members are not called; declaring the first one is enough for its vtable slot.
        }
    }

    /// <summary>
    /// Two narrow registry rules: MUICache values and App Paths keys that point to an executable
    /// which no longer exists on a fixed drive. Each touched key is exported first; without a
    /// backup nothing is changed. An empty key or value alone is never considered invalid.
    /// </summary>
    internal sealed class RegistryCleanTask : ICleanTask
    {
        private const string MuiCache = @"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache";
        private const string AppPaths = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";

        public CleanCategory Category { get { return CleanCategory.Registry; } }

        /// <summary>Folder holding one subfolder of .reg exports per run.</summary>
        public static string BackupRoot
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"ARCA\Respaldos"); }
        }

        public void Run(bool apply, CleanTaskResult result, CancellationToken token)
        {
            var backup = Path.Combine(BackupRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture));

            // MUICache: value names look like "C:\Path\app.exe.FriendlyAppName".
            using (var key = Registry.CurrentUser.OpenSubKey(MuiCache, apply))
            {
                if (key != null)
                {
                    var stale = key.GetValueNames().Where(v => MissingPath.IsMissing(MuiExecutable(v))).ToList();
                    if (stale.Count > 0)
                    {
                        if (!apply) { result.Entries += stale.Count; foreach (var v in stale) result.Detail("MUICache  " + v); }
                        else if (Export(@"HKCU\" + MuiCache, backup, "muicache", result))
                        {
                            foreach (var value in stale)
                            {
                                token.ThrowIfCancellationRequested();
                                try { key.DeleteValue(value, false); result.Entries++; result.Detail("MUICache  " + value); }
                                catch (Exception) { result.Failed++; }
                            }
                        }
                    }
                }
            }

            AppPathKeys(Registry.CurrentUser, "HKCU", apply, backup, result, token);
            AppPathKeys(Registry.LocalMachine, "HKLM", apply, backup, result, token);
        }

        /// <summary>Executable referenced by a MUICache value name, or null when it is not a file path.</summary>
        internal static string MuiExecutable(string valueName)
        {
            if (string.IsNullOrEmpty(valueName) || valueName.StartsWith("@", StringComparison.Ordinal)) return null;
            var end = valueName.LastIndexOf(".exe.", StringComparison.OrdinalIgnoreCase);
            return end > 0 ? valueName.Substring(0, end + 4) : null;
        }

        /// <summary>Executable named in an App Paths default value, which may be quoted or carry arguments.</summary>
        internal static string AppPathExecutable(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            value = value.Trim();
            if (value.StartsWith("\"", StringComparison.Ordinal))
            {
                var close = value.IndexOf('"', 1);
                return close > 1 ? value.Substring(1, close - 1) : null;
            }
            var exe = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            return exe > 0 ? value.Substring(0, exe + 4) : value;
        }

        private static void AppPathKeys(RegistryKey hive, string hiveName, bool apply, string backup, CleanTaskResult result, CancellationToken token)
        {
            using (var key = hive.OpenSubKey(AppPaths, apply))
            {
                if (key == null) return;
                foreach (var name in key.GetSubKeyNames())
                {
                    token.ThrowIfCancellationRequested();
                    string exe;
                    try
                    {
                        using (var sub = key.OpenSubKey(name)) exe = sub == null ? null : AppPathExecutable(sub.GetValue(null) as string);
                    }
                    catch (Exception) { result.Skipped++; continue; }
                    if (!MissingPath.IsMissing(exe)) continue;
                    var label = hiveName + @"\" + AppPaths + @"\" + name;
                    if (!apply) { result.Entries++; result.Detail(label); continue; }
                    if (!Export(label, backup, hiveName + "-apppaths-" + name, result)) continue;
                    try { key.DeleteSubKeyTree(name); result.Entries++; result.Detail(label); }
                    catch (Exception) { result.Failed++; }
                }
            }
        }

        private static bool Export(string key, string folder, string name, CleanTaskResult result)
        {
            try
            {
                Directory.CreateDirectory(folder);
                foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
                var file = Path.Combine(folder, name + ".reg");
                if (RunReg("export \"" + key + "\" \"" + file + "\" /y") && File.Exists(file)) return true;
            }
            catch (Exception) { }
            result.Failed++;
            result.AddNote("Sin respaldo, no se modificó " + key);
            return false;
        }

        /// <summary>Imports every .reg file of the most recent backup. Returns the files restored, or -1.</summary>
        public static int UndoLast()
        {
            if (!Directory.Exists(BackupRoot)) return -1;
            var last = Directory.GetDirectories(BackupRoot).OrderByDescending(d => d, StringComparer.Ordinal).FirstOrDefault();
            if (last == null) return -1;
            var restored = 0;
            foreach (var file in Directory.GetFiles(last, "*.reg"))
                if (RunReg("import \"" + file + "\"")) restored++;
            if (restored > 0)
            {
                try { Directory.Move(last, last + "-restaurado"); } catch (Exception) { }
            }
            return restored;
        }

        private static bool RunReg(string arguments)
        {
            var info = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "reg.exe"), arguments)
            {
                CreateNoWindow = true, UseShellExecute = false, WindowStyle = ProcessWindowStyle.Hidden
            };
            using (var process = Process.Start(info))
            {
                if (!process.WaitForExit(30000)) { try { process.Kill(); } catch (Exception) { } return false; }
                return process.ExitCode == 0;
            }
        }
    }

    /// <summary>Empties the recycle bin through the shell, so its own size accounting is used.</summary>
    internal sealed class RecycleBinTask : ICleanTask
    {
        public CleanCategory Category { get { return CleanCategory.RecycleBin; } }

        public void Run(bool apply, CleanTaskResult result, CancellationToken token)
        {
            var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO)) };
            if (SHQueryRecycleBinW(null, ref info) != 0 || info.i64NumItems == 0) return;
            if (apply)
            {
                // The HRESULT is not trusted alone: some shells report failure for partially emptied bins.
                var hr = SHEmptyRecycleBinW(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                if (hr != 0) Logger.Debug("ARCA: SHEmptyRecycleBin 0x" + hr.ToString("X8", System.Globalization.CultureInfo.InvariantCulture));
                var after = new SHQUERYRBINFO { cbSize = info.cbSize };
                if (SHQueryRecycleBinW(null, ref after) == 0 && after.i64NumItems > 0)
                {
                    result.Failed += (int)Math.Min(int.MaxValue, after.i64NumItems);
                    info.i64Size -= after.i64Size; info.i64NumItems -= after.i64NumItems;
                }
            }
            result.Bytes = info.i64Size;
            result.Files = (int)Math.Min(int.MaxValue, info.i64NumItems);
        }

        private const int SHERB_NOCONFIRMATION = 1, SHERB_NOPROGRESSUI = 2, SHERB_NOSOUND = 4;

        [StructLayout(LayoutKind.Sequential)]
        private struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHQueryRecycleBinW(string pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBinW(IntPtr hwnd, string pszRootPath, int dwFlags);
    }
}
