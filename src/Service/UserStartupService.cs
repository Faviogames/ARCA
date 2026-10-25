using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace WinMemoryCleaner
{
    /// <summary>Per-user Startup shortcut; no scheduled task or elevation.</summary>
    internal static class UserStartupService
    {
        // Opens ARCA in the tray at sign-in; never requests any cleaning.
        internal const string Arguments = "/" + Constants.App.CommandLineArgument.Tray;

        internal static string ShortcutPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "ARCA.lnk"); }
        }

        internal static bool IsEnabled { get { return File.Exists(ShortcutPath); } }

        /// <summary>Rewrites a shortcut from an earlier version that lacks the tray argument.</summary>
        internal static void Upgrade()
        {
            if (IsEnabled && ReadArguments(ShortcutPath) != Arguments) SetEnabled(true);
        }

        internal static string ReadArguments(string shortcutPath)
        {
            var shortcut = (ShellInterop.IShellLink)new ShellInterop.ShellLink();
            try
            {
                ((System.Runtime.InteropServices.ComTypes.IPersistFile)shortcut).Load(shortcutPath, 0);
                var arguments = new StringBuilder(1024);
                shortcut.GetArguments(arguments, arguments.Capacity);
                return arguments.ToString();
            }
            finally
            {
                Marshal.FinalReleaseComObject(shortcut);
            }
        }

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
                shortcut.SetArguments(Arguments);
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
