using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace WinMemoryCleaner
{
    /// <summary>A folder whose contents may be deleted.</summary>
    internal sealed class CleanTarget
    {
        public CleanTarget(string path, TimeSpan minAge, string pattern = "*", bool recursive = true)
        {
            Path = path; MinAge = minAge; Pattern = pattern; Recursive = recursive;
        }

        public string Path { get; private set; }
        /// <summary>Files newer than this (by write or creation time) are left alone.</summary>
        public TimeSpan MinAge { get; private set; }
        /// <summary>Applies to files only; subfolders are always walked when recursive.</summary>
        public string Pattern { get; private set; }
        public bool Recursive { get; private set; }
    }

    /// <summary>Deletes folder contents without ever leaving the folder.</summary>
    internal static class FileCleaner
    {
        /// <summary>
        /// Refuses roots that would be catastrophic if a path variable were empty or wrong:
        /// drive roots, the Windows folder, Program Files, profiles. Non-recursive pattern
        /// targets (e.g. MEMORY.DMP in %WINDIR%) are allowed because they cannot descend.
        /// </summary>
        public static bool IsSafeRoot(CleanTarget target)
        {
            if (target == null || string.IsNullOrEmpty(target.Path) || !System.IO.Path.IsPathRooted(target.Path)) return false;
            string full;
            try { full = System.IO.Path.GetFullPath(target.Path).TrimEnd('\\'); }
            catch (Exception) { return false; }
            if (full.StartsWith(@"\\", StringComparison.Ordinal)) return false;
            if (!target.Recursive && target.Pattern != "*") return full.Length > 3;
            var depth = full.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (depth < 3) return false;
            var forbidden = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            return !forbidden.Any(f => !string.IsNullOrEmpty(f) && string.Equals(f.TrimEnd('\\'), full, StringComparison.OrdinalIgnoreCase));
        }

        public static void Clean(CleanTarget target, bool apply, CleanTaskResult result, CancellationToken token)
        {
            Clean(target, apply, result, DateTime.UtcNow, token);
        }

        internal static void Clean(CleanTarget target, bool apply, CleanTaskResult result, DateTime nowUtc, CancellationToken token)
        {
            if (!IsSafeRoot(target))
            {
                result.Failed++;
                result.Detail("Ruta rechazada por seguridad: " + (target == null ? "(nula)" : target.Path));
                return;
            }
            DirectoryInfo root;
            try
            {
                root = new DirectoryInfo(target.Path);
                // A junction as root would redirect the whole walk somewhere else.
                if (!root.Exists || (root.Attributes & FileAttributes.ReparsePoint) != 0) return;
            }
            catch (Exception) { return; }
            Walk(root, target, apply, result, nowUtc, true, token);
        }

        private static void Walk(DirectoryInfo dir, CleanTarget target, bool apply, CleanTaskResult result, DateTime nowUtc, bool isRoot, CancellationToken token)
        {
            FileSystemInfo[] entries;
            try { entries = dir.GetFileSystemInfos(); }
            catch (PathTooLongException) { result.Skipped++; return; }
            catch (UnauthorizedAccessException) { result.Skipped++; return; }
            catch (IOException) { result.Skipped++; return; }

            foreach (var entry in entries)
            {
                token.ThrowIfCancellationRequested();
                FileAttributes attributes;
                try { attributes = entry.Attributes; }
                catch (Exception) { result.Skipped++; continue; }

                // Junctions and symbolic links are never followed nor deleted.
                if ((attributes & FileAttributes.ReparsePoint) != 0) { result.Skipped++; continue; }

                var sub = entry as DirectoryInfo;
                if (sub != null)
                {
                    if (!target.Recursive) continue;
                    Walk(sub, target, apply, result, nowUtc, false, token);
                    if (apply) TryRemoveEmpty(sub, target, nowUtc);
                    continue;
                }

                var file = (FileInfo)entry;
                if (isRoot && target.Pattern != "*" && !Matches(file.Name, target.Pattern)) continue;
                long length; DateTime newest;
                try
                {
                    length = file.Length;
                    newest = file.LastWriteTimeUtc > file.CreationTimeUtc ? file.LastWriteTimeUtc : file.CreationTimeUtc;
                }
                catch (Exception) { result.Skipped++; continue; }
                if (nowUtc - newest < target.MinAge) continue;

                if (!apply) { result.Bytes += length; result.Files++; continue; }
                try
                {
                    if ((attributes & FileAttributes.ReadOnly) != 0) file.Attributes = attributes & ~FileAttributes.ReadOnly;
                    file.Delete();
                    result.Bytes += length; result.Files++;
                }
                // In use, locked or protected: expected, reported as skipped rather than failed.
                catch (IOException) { result.Skipped++; }
                catch (UnauthorizedAccessException) { result.Skipped++; }
            }
        }

        private static void TryRemoveEmpty(DirectoryInfo dir, CleanTarget target, DateTime nowUtc)
        {
            try
            {
                dir.Refresh();
                if (nowUtc - dir.CreationTimeUtc < target.MinAge) return;
                if (dir.EnumerateFileSystemInfos().Any()) return;
                dir.Delete(false);
            }
            catch (Exception) { }
        }

        // Simple * and ? matching, enough for names like thumbcache_*.db.
        internal static bool Matches(string name, string pattern)
        {
            return Match(name.ToUpperInvariant(), 0, pattern.ToUpperInvariant(), 0);
        }

        private static bool Match(string s, int i, string p, int j)
        {
            while (j < p.Length)
            {
                if (p[j] == '*')
                {
                    for (var k = i; k <= s.Length; k++) if (Match(s, k, p, j + 1)) return true;
                    return false;
                }
                if (i >= s.Length || (p[j] != '?' && p[j] != s[i])) return false;
                i++; j++;
            }
            return i == s.Length;
        }

        /// <summary>Total size of a folder, without following links; used for before/after measures.</summary>
        public static long Measure(string path)
        {
            var result = new CleanTaskResult(CleanCategory.None);
            try
            {
                var root = new DirectoryInfo(path);
                if (!root.Exists || (root.Attributes & FileAttributes.ReparsePoint) != 0) return 0;
                Walk(root, new CleanTarget(path, TimeSpan.Zero), false, result, DateTime.UtcNow, true, CancellationToken.None);
            }
            catch (Exception) { }
            return result.Bytes;
        }
    }
}
