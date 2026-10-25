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
            var source = new CleanTaskResult(CleanCategory.Usb) { State = CleanTaskState.Completed, Bytes = 54321, Entries = 3, Files = 2, Skipped = 4, Failed = 1, Note = "Dispositivo omitido", ProgressMessage = "Retirando 2/3 · Micrófono" };
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
                Assert.AreEqual(source.ProgressMessage, restored.ProgressMessage);
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

        [Test]
        public void ProgressCanBeSentWhileWorkerWaitsForCancellation()
        {
            var name = "arca-duplex-test-" + Guid.NewGuid().ToString("N");
            using (var server = new System.IO.Pipes.NamedPipeServerStream(name, System.IO.Pipes.PipeDirection.InOut, 1,
                System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous))
            using (var client = ElevatedOperations.CreateWorkerPipe(name))
            using (var listening = new System.Threading.ManualResetEvent(false))
            {
                var connection = server.BeginWaitForConnection(null, null);
                client.Connect(3000);
                server.EndWaitForConnection(connection);
                connection.AsyncWaitHandle.Close();
                Exception readError = null, writeError = null;
                var readThread = new System.Threading.Thread(() =>
                {
                    try { listening.Set(); client.ReadByte(); }
                    catch (Exception e) { readError = e; }
                }) { IsBackground = true };
                var writeThread = new System.Threading.Thread(() =>
                {
                    try { client.WriteByte(42); client.Flush(); }
                    catch (Exception e) { writeError = e; }
                }) { IsBackground = true };
                readThread.Start();
                Assert.IsTrue(listening.WaitOne(1000));
                System.Threading.Thread.Sleep(100); // Let the cancellation read enter Windows before sending progress.
                var data = new byte[1];
                var progress = server.BeginRead(data, 0, 1, null, null);
                bool arrived;
                try
                {
                    writeThread.Start();
                    arrived = progress.AsyncWaitHandle.WaitOne(2000);
                }
                finally
                {
                    server.WriteByte(1); server.Flush(); // Release the reader even with the old synchronous bug.
                    readThread.Join(3000); writeThread.Join(3000);
                }
                Assert.IsTrue(arrived, "Progress was blocked by the pending cancellation read.");
                Assert.AreEqual(1, server.EndRead(progress));
                progress.AsyncWaitHandle.Close();
                Assert.AreEqual(42, data[0]);
                Assert.IsNull(readError);
                Assert.IsNull(writeError);
            }
        }

        [TestCase(CleanTaskState.Idle, false)]
        [TestCase(CleanTaskState.Pending, false)]
        [TestCase(CleanTaskState.Analyzing, false)]
        [TestCase(CleanTaskState.Cleaning, false)]
        [TestCase(CleanTaskState.Completed, true)]
        [TestCase(CleanTaskState.Cancelled, true)]
        [TestCase(CleanTaskState.Failed, true)]
        [TestCase(CleanTaskState.Skipped, true)]
        public void InterimProgressIsNotCountedAsAnotherFinalResult(CleanTaskState state, bool expected)
        {
            Assert.AreEqual(expected, ElevatedOperations.IsFinalResult(state));
        }

        [TestCase(0, "No hay dispositivos desconectados para limpiar")]
        [TestCase(1, "Ningún registro de dispositivo retirado")]
        public void EmptyUsbResultDistinguishesNothingToCleanFromFailure(int failures, string expected)
        {
            var result = new CleanTaskResult(CleanCategory.Usb) { Failed = failures };
            StringAssert.StartsWith(expected, CleanText.Brief(result, false));
        }
    }
}
