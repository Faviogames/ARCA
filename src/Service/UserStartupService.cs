using System;
using System.IO;
using System.Runtime.InteropServices;

namespace WinMemoryCleaner
{
    /// <summary>Per-user Startup shortcut; no scheduled task or elevation.</summary>
    internal static class UserStartupService
    {
        internal static string ShortcutPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "ARCA.lnk"); }
        }

        internal static bool IsEnabled { get { return File.Exists(ShortcutPath); } }

        internal static void SetEnabled(bool enabled)
        {
            SetEnabled(enabled, ShortcutPath, Helper.GetExecutablePath());
        }

        internal static void SetEnabled(bool enabled, string shortcutPath, string executable)
        {
            if (!enabled) { if (File.Exists(shortcutPath)) File.Delete(shortcutPath); return; }
            Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath));
            var shortcut = (ShellInterop.IShellLink)new ShellInterop.ShellLink();
            try
            {
                shortcut.SetPath(executable);
                shortcut.SetWorkingDirectory(Path.GetDirectoryName(executable));
                shortcut.SetDescription("ARCA — Another RAM Cleaner Application");
                ((System.Runtime.InteropServices.ComTypes.IPersistFile)shortcut).Save(shortcutPath, true);
            }
            finally
            {
                Marshal.FinalReleaseComObject(shortcut);
            }
        }
    }
}
