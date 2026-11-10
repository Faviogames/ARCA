using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;
using NUnit.Framework;

#pragma warning disable CS1591
#pragma warning disable CA1062 // NUnit supplies test case arguments.
namespace WinMemoryCleaner.Test
{
    [TestFixture]
    public class RegistryRulesTests
    {
        [TestCase("\"C:\\Apps\\Player\\player.exe\" /hide", @"C:\Apps\Player\player.exe")]
        [TestCase(@"C:\Apps\Player\player.exe /hide", @"C:\Apps\Player\player.exe")]
        [TestCase(@"@C:\Apps\Player\icons.dll,-4", @"C:\Apps\Player\icons.dll")]
        [TestCase(@"C:\Apps\Folder", @"C:\Apps\Folder")]
        [TestCase(@"\\server\share\gone.exe", null)]
        [TestCase(@"steam://uninstall/1203190", null)]
        [TestCase("relative.exe", null)]
        [TestCase("%ARCA_UNDEFINED_VARIABLE%\\missing.exe", null)]
        public void ReferenceParserDistinguishesPathsFromOtherData(string value, string expected)
        {
            Assert.AreEqual(expected, RegistryRules.ReferencePath(value));
        }

        [Test]
        public void OrdinaryUninstallRequiresBothInstallationAndUninstallerToBeMissing()
        {
            const string folder = @"C:\Gone\App";
            const string exe = @"C:\Gone\App\uninstall.exe";
            Assert.IsTrue(RegistryRules.StaleUninstall("App", folder, "\"" + exe + "\" /silent", p => true, p => true));
            Assert.IsFalse(RegistryRules.StaleUninstall("App", folder, exe, p => p != folder, p => true));
            Assert.IsFalse(RegistryRules.StaleUninstall("App", folder, exe, p => p != exe, p => true));
            Assert.IsFalse(RegistryRules.StaleUninstall("App", folder, "msiexec.exe /x {GUID}", p => true, p => true));
            Assert.IsFalse(RegistryRules.StaleUninstall("App", folder, @"C:\Windows\System32\msiexec.exe /x {GUID}", p => true, p => true));
            Assert.IsFalse(RegistryRules.StaleUninstall("App", folder, "", p => true, p => true));
            Assert.IsFalse(RegistryRules.StaleUninstall("App", "", exe, p => true, p => true));
        }

        [Test]
        public void SteamManifestOrUnavailableLibraryProtectsAnEntry()
        {
            const string folder = @"D:\SteamLibrary\steamapps\common\Old Game";
            const string manifest = @"D:\SteamLibrary\steamapps\appmanifest_1203190.acf";
            Assert.IsTrue(RegistryRules.StaleUninstall("Steam App 1203190", folder, "steam://uninstall/1203190", p => true, p => true));
            Assert.IsFalse(RegistryRules.StaleUninstall("Steam App 1203190", folder, "", p => p != manifest, p => true));
            Assert.IsFalse(RegistryRules.StaleUninstall("Steam App 1203190", folder, "", p => true, p => false));
            Assert.IsFalse(RegistryRules.StaleUninstall("Steam App 1203190", folder, "", p => p != folder, p => true));
            Assert.IsFalse(RegistryRules.StaleUninstall("Steam App invalid", folder, "", p => true, p => true));
            Assert.IsFalse(RegistryRules.StaleUninstall("Steam App 1203190", @"D:\UnrelatedFolder", "", p => true, p => true));
        }

        [TestCase("WindowsInstaller", 1)]
        [TestCase("SystemComponent", 1)]
        [TestCase("NoRemove", 1)]
        [TestCase("ReleaseType", "Security Update")]
        [TestCase("ParentKeyName", "Parent")]
        [TestCase("WindowsInstaller", "unknown")]
        public void ProtectedInstallerMetadataIsAlwaysKept(string name, object value)
        {
            var values = new Dictionary<string, object> { { "DisplayName", "App" }, { name, value } };
            Assert.IsTrue(RegistryRules.ProtectedUninstall(n => values.ContainsKey(n) ? values[n] : null));
        }

        [Test]
        public void MissingProductIdentityIsNotEvidenceOfAnOrphan()
        {
            Assert.IsTrue(RegistryRules.ProtectedUninstall(n => null));
            Assert.IsFalse(RegistryRules.ProtectedUninstall(n => n == "DisplayName" ? "App" : null));
        }

        [Test]
        public void FailedBackupNeverDeletes()
        {
            var deleted = false;
            var state = RegistryRules.ApplyWithBackup("original", () => "original", () => true, () => false, () => { deleted = true; return true; });
            Assert.AreEqual(RegistryMutation.Failed, state);
            Assert.IsFalse(deleted);
        }

        [Test]
        public void ChangedRegistryDataAfterExportIsKept()
        {
            var data = "original";
            var deleted = false;
            var state = RegistryRules.ApplyWithBackup("original", () => data, () => true,
                () => { data = "new data"; return true; }, () => { deleted = true; return true; });
            Assert.AreEqual(RegistryMutation.Skipped, state);
            Assert.IsFalse(deleted);
        }

        [Test]
        public void ReinstalledApplicationAfterExportIsKept()
        {
            var absent = true;
            var deleted = false;
            var state = RegistryRules.ApplyWithBackup("original", () => "original", () => absent,
                () => { absent = false; return true; }, () => { deleted = true; return true; });
            Assert.AreEqual(RegistryMutation.Skipped, state);
            Assert.IsFalse(deleted);
        }

        [Test]
        public void CountsAsRemovedOnlyAfterVerification()
        {
            Assert.AreEqual(RegistryMutation.Removed, RegistryRules.ApplyWithBackup("old", () => "old", () => true, () => true, () => true));
            Assert.AreEqual(RegistryMutation.Failed, RegistryRules.ApplyWithBackup("old", () => "old", () => true, () => true, () => false));
            Assert.AreEqual(RegistryMutation.Skipped, RegistryRules.ApplyWithBackup(null, () => null, () => true, () => true, () => true));
        }

        [Test]
        public void ExportAndImportChooseTheOriginalRegistryView()
        {
            Assert.AreEqual(RegistryView.Registry32, RegistryCleanTask.BackupView("r32-test.reg"));
            Assert.AreEqual(RegistryView.Registry64, RegistryCleanTask.BackupView("r64-test.reg"));
            Assert.IsTrue(RegistryCleanTask.RegExecutable(RegistryView.Registry32, true, true).EndsWith(@"SysWOW64\reg.exe", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(RegistryCleanTask.RegExecutable(RegistryView.Registry64, true, true).EndsWith(@"System32\reg.exe", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(RegistryCleanTask.RegExecutable(RegistryView.Registry64, true, false).EndsWith(@"Sysnative\reg.exe", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(RegistryCleanTask.RegExecutable(RegistryView.Registry32, false, false).EndsWith(@"System32\reg.exe", StringComparison.OrdinalIgnoreCase));
        }

    }

    [TestFixture]
    public class RegistryRulesIntegrationTests
    {
        // Integration tests use only a GUID-named HKCU key and files created by this fixture.
        private string _key, _folder;
        private bool _created;
        private RegistryView NativeView { get { return Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32; } }

        [SetUp]
        public void CreateOwnedTestData()
        {
            _key = @"Software\ARCA\RegistryTests\" + Guid.NewGuid().ToString("N");
            _folder = Path.Combine(Path.GetTempPath(), "arca-registry-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            _created = false;
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(_key + @"\App"))
                {
                    _created = true;
                    key.SetValue("Path", Path.Combine(_folder, "missing.exe"));
                    key.SetValue("Number", 42, RegistryValueKind.DWord);
                    key.SetValue("Expand", @"%TEMP%\keep", RegistryValueKind.ExpandString);
                    using (var sub = key.CreateSubKey("Nested")) sub.SetValue("Data", new byte[] { 1, 2, 3 }, RegistryValueKind.Binary);
                }
            }
            catch (UnauthorizedAccessException) { Assert.Inconclusive("The test runner cannot create its own HKCU fixture."); }
        }

        [TearDown]
        public void RemoveOwnedTestData()
        {
            if (_created && _key.StartsWith(@"Software\ARCA\RegistryTests\", StringComparison.Ordinal))
                Registry.CurrentUser.DeleteSubKeyTree(_key, false);
            if (_folder != null && Path.GetFullPath(_folder).StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(_folder).StartsWith("arca-registry-tests-", StringComparison.Ordinal) && Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        private CleanTaskResult RunOwned(bool apply, string backup, string valueName)
        {
            var result = new CleanTaskResult(CleanCategory.Registry);
            RegistryRules.Process(Registry.CurrentUser, "HKCU", _key + @"\App", valueName, NativeView,
                k => MissingPath.IsMissing(k.GetValue("Path") as string), "App Paths", apply,
                backup, result, CancellationToken.None);
            return result;
        }

        [Test]
        public void AnalysisDoesNotExportOrModifyTheRegistry()
        {
            var backup = Path.Combine(_folder, "backups");
            var result = RunOwned(false, backup, null);
            Assert.AreEqual(1, result.Entries);
            Assert.IsFalse(Directory.Exists(backup));
            using (var key = Registry.CurrentUser.OpenSubKey(_key + @"\App")) Assert.IsNotNull(key);
        }


        [Test]
        public void FullAnalyzerFindsOwnedAppPathsAndUninstallEntriesWithoutApplyingChanges()
        {
            var name = "ARCA-Test-" + Guid.NewGuid().ToString("N");
            var app = @"Software\Microsoft\Windows\CurrentVersion\App Paths\" + name + ".exe";
            var uninstall = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + name;
            var created = new List<string>();
            try
            {
                using (var before = Registry.CurrentUser.OpenSubKey(app)) Assert.IsNull(before);
                using (var before = Registry.CurrentUser.OpenSubKey(uninstall)) Assert.IsNull(before);
                using (var key = Registry.CurrentUser.CreateSubKey(app))
                {
                    created.Add(app);
                    key.SetValue(null, Path.Combine(_folder, "missing.exe"));
                }
                using (var key = Registry.CurrentUser.CreateSubKey(uninstall))
                {
                    created.Add(uninstall);
                    key.SetValue("DisplayName", name);
                    key.SetValue("InstallLocation", Path.Combine(_folder, "missing-install"));
                    key.SetValue("UninstallString", "\"" + Path.Combine(_folder, "missing-install", "uninstall.exe") + "\"");
                }
                var result = new CleanTaskResult(CleanCategory.Registry);
                var backup = Path.Combine(_folder, "full-analysis-backup");
                RegistryRules.Run(false, backup, result, CancellationToken.None);
                Assert.IsTrue(result.Details.Any(d => d.IndexOf(app, StringComparison.OrdinalIgnoreCase) >= 0), "The full scanner must actually enumerate App Paths.");
                Assert.IsTrue(result.Details.Any(d => d.IndexOf(uninstall, StringComparison.OrdinalIgnoreCase) >= 0), "The full scanner must actually enumerate Uninstall.");
                Assert.IsFalse(Directory.Exists(backup));
                using (var key = Registry.CurrentUser.OpenSubKey(app)) Assert.IsNotNull(key);
                using (var key = Registry.CurrentUser.OpenSubKey(uninstall)) Assert.IsNotNull(key);
            }
            finally
            {
                foreach (var path in created)
                    if (path == app || path == uninstall) Registry.CurrentUser.DeleteSubKeyTree(path, false);
            }
        }

        [Test]
        public void ActualExportDeletionAndImportRestoreTypedData()
        {
            var backup = Path.Combine(_folder, "backups");
            var result = RunOwned(true, backup, null);
            Assert.AreEqual(1, result.Entries);
            Assert.AreEqual(0, result.Failed);
            using (var key = Registry.CurrentUser.OpenSubKey(_key + @"\App")) Assert.IsNull(key);
            var file = Directory.GetFiles(backup, "*.reg").Single();
            Assert.IsTrue(RegistryCleanTask.ImportFile(file));
            using (var key = Registry.CurrentUser.OpenSubKey(_key + @"\App"))
            {
                Assert.AreEqual(42, key.GetValue("Number"));
                Assert.AreEqual(RegistryValueKind.ExpandString, key.GetValueKind("Expand"));
                Assert.AreEqual(@"%TEMP%\keep", key.GetValue("Expand", null, RegistryValueOptions.DoNotExpandEnvironmentNames));
                using (var sub = key.OpenSubKey("Nested")) CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, (byte[])sub.GetValue("Data"));
            }
        }

        [Test]
        public void AValueRuleKeepsTheOtherValuesAndTheKey()
        {
            var result = RunOwned(true, Path.Combine(_folder, "backups"), "Expand");
            Assert.AreEqual(1, result.Entries);
            using (var key = Registry.CurrentUser.OpenSubKey(_key + @"\App"))
            {
                Assert.IsFalse(key.GetValueNames().Contains("Expand"));
                Assert.AreEqual(42, key.GetValue("Number"));
            }
        }

        [Test]
        public void ExportFailureKeepsTheWholeKey()
        {
            var blocked = Path.Combine(_folder, "not-a-folder");
            File.WriteAllText(blocked, "owned test file");
            var result = RunOwned(true, blocked, null);
            Assert.AreEqual(0, result.Entries);
            Assert.AreEqual(1, result.Failed);
            using (var key = Registry.CurrentUser.OpenSubKey(_key + @"\App")) Assert.IsNotNull(key);
        }

        [Test]
        public void SnapshotDetectsNestedChangesAndDataTypes()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(_key + @"\App", true))
            {
                var before = RegistryRules.Snapshot(key);
                key.SetValue("Number", "42", RegistryValueKind.String);
                Assert.AreNotEqual(before, RegistryRules.Snapshot(key));
                var second = RegistryRules.Snapshot(key);
                using (var sub = key.OpenSubKey("Nested", true)) sub.SetValue("Extra", "new");
                Assert.AreNotEqual(second, RegistryRules.Snapshot(key));
            }
        }

        [Test]
        public void CancellationBeforeApplyMakesNoChange()
        {
            var cancel = new CancellationTokenSource();
            cancel.Cancel();
            try
            {
                Assert.Throws<OperationCanceledException>(() => RegistryRules.Process(
                    Registry.CurrentUser, "HKCU", _key + @"\App", null, NativeView, k => true,
                    "App Paths", true, Path.Combine(_folder, "backups"), new CleanTaskResult(CleanCategory.Registry), cancel.Token));
                Assert.IsFalse(Directory.Exists(Path.Combine(_folder, "backups")));
            }
            finally { cancel.Dispose(); }
        }
    }
}
