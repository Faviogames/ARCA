using System;
using System.IO;
using NUnit.Framework;

#pragma warning disable CS1591
namespace WinMemoryCleaner.Test
{
    [TestFixture]
    public class PermissionTests
    {
        [TestCase(CleanCategory.Caches, false)]
        [TestCase(CleanCategory.RecycleBin, false)]
        [TestCase(CleanCategory.Temp, true)]
        [TestCase(CleanCategory.Registry, true)]
        [TestCase(CleanCategory.Usb, true)]
        [TestCase(CleanCategory.Extreme, true)]
        [TestCase(CleanCategory.Caches | CleanCategory.Usb, true)]
        public void OnlySystemCleaningRequestsElevation(CleanCategory categories, bool expected)
        {
            Assert.AreEqual(expected, ElevatedOperations.NeedsElevation(categories, true));
            Assert.IsFalse(ElevatedOperations.NeedsElevation(categories, false), "Analysis must not launch an elevated worker.");
        }

        [TestCase(0, false)]
        [TestCase(-1, false)]
        [TestCase(4, false)]
        [TestCase(128, false)]
        [TestCase(123, true)]
        public void WorkerRejectsUnknownCategoryBits(int selection, bool valid)
        {
            Assert.AreEqual(valid, ElevatedOperations.ValidCategories(selection));
        }

        [TestCase(0, false)]
        [TestCase(-1, false)]
        [TestCase(256, false)]
        [TestCase(255, true)]
        [TestCase(128, true)]
        public void WorkerRejectsUnknownMemoryBits(int selection, bool valid)
        {
            Assert.AreEqual(valid, ElevatedOperations.ValidMemoryAreas(selection));
        }

        [Test]
        public void ElevatedReportRetainsCountersNotesAndDetails()
        {
            var source = new CleanTaskResult(CleanCategory.Usb) { State = CleanTaskState.Completed, Bytes = 54321, Entries = 3, Files = 2, Skipped = 4, Failed = 1, Note = "Dispositivo omitido" };
            source.Detail("USB de prueba");
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            using (var reader = new BinaryReader(stream))
            {
                ElevatedOperations.WriteResult(writer, source);
                stream.Position = 0;
                var restored = ElevatedOperations.ReadResult(reader, source.Category, source.State);
                Assert.AreEqual(source.Bytes, restored.Bytes);
                Assert.AreEqual(source.Entries, restored.Entries);
                Assert.AreEqual(source.Files, restored.Files);
                Assert.AreEqual(source.Skipped, restored.Skipped);
                Assert.AreEqual(source.Failed, restored.Failed);
                Assert.AreEqual(source.Note, restored.Note);
                CollectionAssert.AreEqual(source.Details, restored.Details);
            }
        }

        [Test]
        public void ShortcutCanBeEnabledAndDisabledWithoutTouchingRealStartup()
        {
            var root = Path.Combine(Path.GetTempPath(), "arca-startup-test-" + Guid.NewGuid().ToString("N"));
            var shortcut = Path.Combine(root, "ARCA.lnk");
            try
            {
                UserStartupService.SetEnabled(true, shortcut, Helper.GetExecutablePath());
                Assert.IsTrue(File.Exists(shortcut));
                Assert.Greater(new FileInfo(shortcut).Length, 0);
                var link = (ShellInterop.IShellLink)new ShellInterop.ShellLink();
                try
                {
                    ((System.Runtime.InteropServices.ComTypes.IPersistFile)link).Load(shortcut, 0);
                    var path = new System.Text.StringBuilder(1024);
                    link.GetPath(path, path.Capacity, IntPtr.Zero, 0);
                    Assert.AreEqual(Helper.GetExecutablePath(), path.ToString());
                    link.GetArguments(path, path.Capacity);
                    Assert.AreEqual(string.Empty, path.ToString(), "Startup must not request any cleaning operation.");
                }
                finally { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link); }
                UserStartupService.SetEnabled(false, shortcut, Helper.GetExecutablePath());
                Assert.IsFalse(File.Exists(shortcut));
                UserStartupService.SetEnabled(false, shortcut, Helper.GetExecutablePath());
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [Test]
        public void WorkerArgumentIsHandledBeforeNormalApplicationStartup()
        {
            Assert.IsFalse(ElevatedOperations.TryRunWorker(new string[0]));
            Assert.IsFalse(ElevatedOperations.TryRunWorker(new[] { "--other" }));
            Assert.IsTrue(ElevatedOperations.TryRunWorker(new[] { "--arca-worker", "invalid", "0" }));
        }
    }
}
