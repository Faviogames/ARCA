using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

#pragma warning disable CS1591
#pragma warning disable CA1062 // NUnit supplies the test case arguments.
namespace WinMemoryCleaner.Test
{
    [TestFixture]
    public class OneClickCleanTests
    {
        private string _root;
        private static readonly DateTime Now = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "arca-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            // Junction first, so the outside folder's contents can never be reached by the recursive delete.
            foreach (var dir in Directory.GetDirectories(_root, "*", SearchOption.AllDirectories).Where(d => (File.GetAttributes(d) & FileAttributes.ReparsePoint) != 0).ToList())
                Directory.Delete(dir);
            Directory.Delete(_root, true);
        }

        private string Make(string relative, int bytes, TimeSpan age)
        {
            var path = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, new byte[bytes]);
            File.SetCreationTimeUtc(path, Now - age);
            File.SetLastWriteTimeUtc(path, Now - age);
            return path;
        }

        private CleanTaskResult Clean(CleanTarget target, bool apply)
        {
            var result = new CleanTaskResult(CleanCategory.Temp);
            FileCleaner.Clean(target, apply, result, Now, CancellationToken.None);
            return result;
        }

        [Test]
        public void Analysis_MeasuresButDeletesNothing()
        {
            var a = Make(@"a.tmp", 100, TimeSpan.FromDays(3));
            var b = Make(@"sub\b.tmp", 50, TimeSpan.FromDays(3));
            var r = Clean(new CleanTarget(_root, TimeSpan.FromHours(48)), false);
            Assert.AreEqual(150, r.Bytes);
            Assert.AreEqual(2, r.Files);
            Assert.IsTrue(File.Exists(a) && File.Exists(b));
        }

        [Test]
        public void Clean_DeletesOldFiles_KeepsRecentOnes_AndCountsOnlyDeleted()
        {
            var old = Make(@"sub\old.tmp", 100, TimeSpan.FromDays(3));
            var recent = Make(@"recent.tmp", 70, TimeSpan.FromHours(2));
            var r = Clean(new CleanTarget(_root, TimeSpan.FromHours(48)), true);
            Assert.IsFalse(File.Exists(old));
            Assert.IsTrue(File.Exists(recent));
            Assert.AreEqual(100, r.Bytes);
            Assert.AreEqual(1, r.Files);
            Assert.AreEqual(0, r.Skipped);
        }

        [Test]
        public void Clean_FileInUse_IsSkippedNotFailed()
        {
            var locked = Make(@"locked.tmp", 10, TimeSpan.FromDays(3));
            using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var r = Clean(new CleanTarget(_root, TimeSpan.Zero), true);
                Assert.AreEqual(1, r.Skipped);
                Assert.AreEqual(0, r.Failed);
                Assert.AreEqual(0, r.Bytes);
            }
            Assert.IsTrue(File.Exists(locked));
        }

        [Test]
        public void Clean_ReadOnlyFile_IsDeleted()
        {
            var file = Make(@"ro.tmp", 10, TimeSpan.FromDays(3));
            File.SetAttributes(file, FileAttributes.ReadOnly);
            Clean(new CleanTarget(_root, TimeSpan.Zero), true);
            Assert.IsFalse(File.Exists(file));
        }

        [Test]
        public void Clean_NeverFollowsJunctions()
        {
            var outside = Path.Combine(_root, "outside");
            var keep = Make(@"outside\precious.txt", 10, TimeSpan.FromDays(30));
            var target = Path.Combine(_root, "target");
            Directory.CreateDirectory(target);
            var junction = Path.Combine(target, "link");
            using (var p = Process.Start(new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + junction + "\" \"" + outside + "\"") { CreateNoWindow = true, UseShellExecute = false }))
                p.WaitForExit(10000);
            if (!Directory.Exists(junction)) Assert.Ignore("mklink /J no disponible");

            var r = Clean(new CleanTarget(target, TimeSpan.Zero), true);
            Assert.IsTrue(File.Exists(keep));
            Assert.IsTrue(Directory.Exists(junction));
            Assert.AreEqual(1, r.Skipped);

            // A junction as root is not walked either.
            Clean(new CleanTarget(junction, TimeSpan.Zero), true);
            Assert.IsTrue(File.Exists(keep));
        }

        [Test]
        public void Clean_PatternTarget_IsNotRecursive()
        {
            var thumb = Make(@"thumbcache_256.db", 10, TimeSpan.FromDays(1));
            var other = Make(@"iconcache_16.db", 10, TimeSpan.FromDays(1));
            var nested = Make(@"sub\thumbcache_32.db", 10, TimeSpan.FromDays(1));
            Clean(new CleanTarget(_root, TimeSpan.Zero, "thumbcache_*.db", false), true);
            Assert.IsFalse(File.Exists(thumb));
            Assert.IsTrue(File.Exists(other));
            Assert.IsTrue(File.Exists(nested));
        }

        [Test]
        public void Clean_RemovesEmptiedOldFolders()
        {
            Make(@"empty-soon\x.tmp", 1, TimeSpan.FromDays(3));
            Directory.SetCreationTimeUtc(Path.Combine(_root, "empty-soon"), Now - TimeSpan.FromDays(3));
            Clean(new CleanTarget(_root, TimeSpan.FromHours(48)), true);
            Assert.IsFalse(Directory.Exists(Path.Combine(_root, "empty-soon")));
            Assert.IsTrue(Directory.Exists(_root));
        }

        [Test]
        public void SafeRoot_RejectsDangerousRoots()
        {
            Assert.IsFalse(FileCleaner.IsSafeRoot(new CleanTarget(@"C:\", TimeSpan.Zero)));
            Assert.IsFalse(FileCleaner.IsSafeRoot(new CleanTarget(string.Empty, TimeSpan.Zero)));
            Assert.IsFalse(FileCleaner.IsSafeRoot(new CleanTarget(@"Temp", TimeSpan.Zero)));
            Assert.IsFalse(FileCleaner.IsSafeRoot(new CleanTarget(@"\\server\share\temp", TimeSpan.Zero)));
            Assert.IsFalse(FileCleaner.IsSafeRoot(new CleanTarget(Environment.GetFolderPath(Environment.SpecialFolder.Windows), TimeSpan.Zero)));
            Assert.IsFalse(FileCleaner.IsSafeRoot(new CleanTarget(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), TimeSpan.Zero)));
            Assert.IsFalse(FileCleaner.IsSafeRoot(new CleanTarget(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), TimeSpan.Zero)));
            Assert.IsTrue(FileCleaner.IsSafeRoot(new CleanTarget(_root, TimeSpan.Zero)));
            Assert.IsTrue(FileCleaner.IsSafeRoot(new CleanTarget(Environment.GetFolderPath(Environment.SpecialFolder.Windows), TimeSpan.Zero, "MEMORY.DMP", false)));
        }

        [Test]
        public void Clean_UnsafeRoot_IsRefusedAsFailure()
        {
            var r = Clean(new CleanTarget(@"C:\", TimeSpan.Zero), false);
            Assert.AreEqual(1, r.Failed);
            Assert.AreEqual(0, r.Files);
        }

        [TestCase("thumbcache_256.db", "thumbcache_*.db", true)]
        [TestCase("THUMBCACHE_idx.DB", "thumbcache_*.db", true)]
        [TestCase("iconcache_16.db", "thumbcache_*.db", false)]
        [TestCase("MEMORY.DMP", "memory.dmp", true)]
        [TestCase("a.dmp", "*.dmp", true)]
        [TestCase("a.dmp.txt", "*.dmp", false)]
        public void PatternMatching(string name, string pattern, bool expected)
        {
            Assert.AreEqual(expected, FileCleaner.Matches(name, pattern));
        }

        private static bool Missing(string path, bool exists = false, DriveType drive = DriveType.Fixed)
        {
            return MissingPath.IsMissing(path, p => exists, r => drive);
        }

        [Test]
        public void MissingPath_OnlyAbsoluteFixedLocalPathsCanBeMissing()
        {
            Assert.IsTrue(Missing(@"C:\Program Files\Old\old.exe"));
            Assert.IsTrue(Missing("\"C:\\Program Files\\Old\\old.exe\""));
            Assert.IsFalse(Missing(@"C:\Program Files\App\app.exe", exists: true));
            Assert.IsFalse(Missing(@"E:\Games\game.exe", drive: DriveType.Removable));
            Assert.IsFalse(Missing(@"Z:\tools\x.exe", drive: DriveType.Network));
            Assert.IsFalse(Missing(@"F:\x.exe", drive: DriveType.Unknown));
            Assert.IsFalse(Missing(@"\\nas\apps\x.exe"));
            Assert.IsFalse(Missing("notepad.exe"));
            Assert.IsFalse(Missing(string.Empty));
            Assert.IsFalse(Missing(null));
        }

        [Test]
        public void MissingPath_ExpandsEnvironmentVariables()
        {
            string seen = null;
            MissingPath.IsMissing(@"%SystemRoot%\x.exe", p => { seen = p; return true; }, r => DriveType.Fixed);
            Assert.AreEqual(Environment.ExpandEnvironmentVariables(@"%SystemRoot%\x.exe"), seen);
        }

        [TestCase(@"C:\Apps\Tool\tool.exe.FriendlyAppName", @"C:\Apps\Tool\tool.exe")]
        [TestCase(@"C:\Apps\Tool\tool.exe.ApplicationCompany", @"C:\Apps\Tool\tool.exe")]
        [TestCase(@"@%SystemRoot%\system32\shell32.dll,-22019", null)]
        [TestCase(@"LangID", null)]
        [TestCase("", null)]
        public void Registry_MuiCacheExecutable(string value, string expected)
        {
            Assert.AreEqual(expected, RegistryCleanTask.MuiExecutable(value));
        }

        [TestCase("\"C:\\Program Files\\App\\app.exe\" --flag", @"C:\Program Files\App\app.exe")]
        [TestCase(@"C:\Program Files\App\app.exe", @"C:\Program Files\App\app.exe")]
        [TestCase(@"%ProgramFiles%\App\app.EXE /x", @"%ProgramFiles%\App\app.EXE")]
        [TestCase("", null)]
        [TestCase(null, null)]
        public void Registry_AppPathExecutable(string value, string expected)
        {
            Assert.AreEqual(expected, RegistryCleanTask.AppPathExecutable(value));
        }

        [Test]
        public void Shortcuts_ProgramFilesSwap()
        {
            Assert.AreEqual(@"C:\Program Files\A\a.exe", ShortcutCleanTask.SwapProgramFiles(@"C:\Program Files (x86)\A\a.exe"));
            Assert.AreEqual(@"C:\Program Files (x86)\A\a.exe", ShortcutCleanTask.SwapProgramFiles(@"C:\Program Files\A\a.exe"));
            Assert.AreEqual(@"D:\Games\a.exe", ShortcutCleanTask.SwapProgramFiles(@"D:\Games\a.exe"));
        }

        [Test]
        public void Usb_DisconnectedDevicesHaveNoAgeOrDateRequirement()
        {
            Assert.IsTrue(UsbCleanTask.ShouldRemove(false));
            Assert.IsFalse(UsbCleanTask.ShouldRemove(true));
        }

        [Test]
        public void Usb_ReconnectedDeviceIsNotRemovedAfterAnalysis()
        {
            Assert.IsTrue(UsbCleanTask.ShouldRemove(false)); // disconnected during analysis
            var calls = 0;
            var outcome = UsbCleanTask.RemoveIfDisconnected(() => true, () => { calls++; return true; });
            Assert.IsNull(outcome);
            Assert.AreEqual(0, calls);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Usb_RemovalReportsActualSuccessOrFailure(bool removalSucceeds)
        {
            var calls = 0;
            var outcome = UsbCleanTask.RemoveIfDisconnected(() => false, () => { calls++; return removalSucceeds; });
            Assert.AreEqual(removalSucceeds, outcome);
            Assert.AreEqual(1, calls);
        }

        [Test]
        public void WindowsUpdate_BlockedByPendingRebootOrInstallation()
        {
            Assert.IsNull(ExtremeCleanTask.UpdateBlocker(k => false, p => false));
            StringAssert.Contains("reinicio", ExtremeCleanTask.UpdateBlocker(k => k.EndsWith("RebootRequired", StringComparison.Ordinal), p => false));
            StringAssert.Contains("reinicio", ExtremeCleanTask.UpdateBlocker(k => k.EndsWith("RebootPending", StringComparison.Ordinal), p => false));
            StringAssert.Contains("instalación", ExtremeCleanTask.UpdateBlocker(k => false, p => p == "TiWorker"));
            Assert.IsNull(ExtremeCleanTask.UpdateBlocker(k => false, p => p == "TrustedInstaller"));
        }

        private sealed class FakeTask : ICleanTask
        {
            public CleanCategory Category { get; set; }
            public Action<CleanTaskResult> Body { get; set; }
            public bool Applied;
            public void Run(bool apply, CleanTaskResult result, CancellationToken token)
            {
                Applied = apply;
                if (Body != null) Body(result);
                token.ThrowIfCancellationRequested();
            }
        }

        [Test]
        public void Runner_FailingTaskDoesNotStopTheOthers()
        {
            var states = new List<string>();
            var tasks = new List<ICleanTask>
            {
                new FakeTask { Category = CleanCategory.Temp, Body = r => { throw new InvalidOperationException("boom"); } },
                new FakeTask { Category = CleanCategory.Caches, Body = r => { r.Bytes = 2048; r.Files = 2; } }
            };
            var results = OneClickCleanService.Run(tasks, false, (c, s, r) => states.Add(c + ":" + s), CancellationToken.None);
            Assert.AreEqual(CleanTaskState.Failed, results[0].State);
            StringAssert.Contains("boom", results[0].Note);
            Assert.AreEqual(CleanTaskState.Completed, results[1].State);
            CollectionAssert.Contains(states, "Caches:Analyzing");
            CollectionAssert.Contains(states, "Caches:Completed");
            StringAssert.Contains("1 categoría con error", CleanText.Summary(results));
        }

        [Test]
        public void Runner_CancellationKeepsPartialWorkAndCancelsTheRest()
        {
            using (var cancel = new CancellationTokenSource())
            {
            var second = new FakeTask { Category = CleanCategory.Caches };
            var tasks = new List<ICleanTask>
            {
                new FakeTask { Category = CleanCategory.Temp, Body = r => { r.Bytes = 500; r.Files = 1; cancel.Cancel(); } },
                second
            };
            var results = OneClickCleanService.Run(tasks, false, null, cancel.Token);
            Assert.AreEqual(CleanTaskState.Cancelled, results[0].State);
            Assert.AreEqual(500, results[0].Bytes);
            Assert.AreEqual(CleanTaskState.Cancelled, results[1].State);
                Assert.IsFalse(second.Applied);
            }
        }

        [Test]
        public void Runner_SkippedWhenOnlyANoteAndNothingDone()
        {
            var tasks = new List<ICleanTask> { new FakeTask { Category = CleanCategory.Extreme, Body = r => r.AddNote("Windows Update: actualización pendiente de reinicio") } };
            Assert.AreEqual(CleanTaskState.Skipped, OneClickCleanService.Run(tasks, false, null, CancellationToken.None)[0].State);
        }

        [Test]
        public void Runner_RejectsASecondSimultaneousRun()
        {
            IList<CleanTaskResult> inner = new List<CleanTaskResult>();
            var tasks = new List<ICleanTask>
            {
                new FakeTask { Category = CleanCategory.Temp, Body = r => inner = OneClickCleanService.Run(new List<ICleanTask>(), false, null, CancellationToken.None) }
            };
            Assert.IsNotNull(OneClickCleanService.Run(tasks, false, null, CancellationToken.None));
            Assert.IsNull(inner);
            Assert.IsFalse(OneClickCleanService.IsRunning);
        }

        [Test]
        public void Runner_FollowsSafeOrder_RecycleBinBeforeTemporariesAndShortcuts()
        {
            var order = OneClickCleanService.Order.ToList();
            Assert.Less(order.IndexOf(CleanCategory.RecycleBin), order.IndexOf(CleanCategory.Temp));
            Assert.IsFalse(order.Contains(CleanCategory.Shortcuts));
            Assert.AreEqual(6, order.Count);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TemporariesIncludeShortcutsInAnalysisAndCleaning(bool apply)
        {
            var files = new FakeTask { Category = CleanCategory.Temp, Body = r => { r.Files = 2; r.Bytes = 1024; } };
            var shortcuts = new FakeTask { Category = CleanCategory.Shortcuts, Body = r => r.Entries++ };
            var combined = new TemporaryCleanTask(files, shortcuts);
            var results = OneClickCleanService.Run(new List<ICleanTask> { combined }, apply, null, CancellationToken.None);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(CleanCategory.Temp, results[0].Category);
            Assert.AreEqual(apply, files.Applied);
            Assert.AreEqual(apply, shortcuts.Applied);
            Assert.AreEqual(2, results[0].Files);
            Assert.AreEqual(1, results[0].Entries);
            StringAssert.Contains("1 acceso", CleanText.Brief(results[0], !apply));
            StringAssert.Contains("1 acceso retirado", CleanText.Summary(results));
        }

        [Test]
        public void TemporariesCancellationDoesNotRunShortcutCleanup()
        {
            using (var cancel = new CancellationTokenSource())
            {
                var files = new FakeTask { Category = CleanCategory.Temp, Body = r => { r.Files = 1; cancel.Cancel(); } };
                var calls = 0;
                var shortcuts = new FakeTask { Category = CleanCategory.Shortcuts, Body = r => calls++ };
                var results = OneClickCleanService.Run(new List<ICleanTask> { new TemporaryCleanTask(files, shortcuts) }, false, null, cancel.Token);
                Assert.AreEqual(0, calls);
                Assert.AreEqual(1, results[0].Files);
                Assert.AreEqual(CleanTaskState.Cancelled, results[0].State);
            }
        }

        [Test]
        public void StandaloneLegacyShortcutSelectionDoesNotScheduleCleaning()
        {
            var results = OneClickCleanService.Run(CleanCategory.Shortcuts, false, null, CancellationToken.None);
            Assert.AreEqual(0, results.Count);
            Assert.IsInstanceOf<TemporaryCleanTask>(OneClickCleanService.Create(CleanCategory.Temp));
        }

        [Test]
        public void Summary_CountsEachKindSeparately()
        {
            var temp = new CleanTaskResult(CleanCategory.Temp) { Bytes = 850L << 20, Files = 120, State = CleanTaskState.Completed };
            var reg = new CleanTaskResult(CleanCategory.Registry) { Entries = 3, State = CleanTaskState.Completed };
            var usb = new CleanTaskResult(CleanCategory.Usb) { Entries = 1, State = CleanTaskState.Completed };
            Assert.AreEqual("850 MB liberados · 120 archivos eliminados · 3 entradas retiradas · 1 dispositivo retirado", CleanText.Summary(new[] { temp, reg, usb }));
        }

        [Test]
        public void Short_DescribesEmptyAndSkipped()
        {
            Assert.AreEqual("Ya estaba limpio", CleanText.Brief(new CleanTaskResult(CleanCategory.Temp), false));
            Assert.AreEqual("Nada que limpiar", CleanText.Brief(new CleanTaskResult(CleanCategory.Temp), true));
            Assert.AreEqual("1 KB · 1 archivo · 2 omitidos", CleanText.Brief(new CleanTaskResult(CleanCategory.Temp) { Bytes = 1024, Files = 1, Skipped = 2 }, false));
        }

        [Test]
        public void EveryCategoryHasATask()
        {
            foreach (var category in OneClickCleanService.Order)
                Assert.AreEqual(category, OneClickCleanService.Create(category).Category);
        }
    }
}
