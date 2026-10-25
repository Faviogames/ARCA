using System;
using System.Net;
using System.Reflection;
using NUnit.Framework;

namespace WinMemoryCleaner.Test
{
    /// <summary>Update decisions without contacting GitHub or replacing the executable.</summary>
    [TestFixture]
    public class UpdaterTests
    {
        /// <summary>Equal or older versions never enable installation.</summary>
        [Test]
        public void OnlyNewerReleasesAreOffered()
        {
            Assert.IsFalse(Updater.IsNewerRelease("{\"tag_name\":\"v2.0\"}", new Version(2, 0, 0, 0)));
            Assert.IsFalse(Updater.IsNewerRelease("{\"tag_name\":\"v1.0.3\"}", new Version(2, 0)));
            Assert.IsTrue(Updater.IsNewerRelease("{\"tag_name\":\"v2.0.1\"}", new Version(2, 0)));
            Assert.IsFalse(Updater.IsNewerRelease("{\"message\":\"Not Found\"}", new Version(2, 0)));
        }

        /// <summary>Checks and downloads disable the button, even if a release was found.</summary>
        [Test]
        public void AvailabilityRequiresAnIdleClientAndANewerVersion()
        {
            var flags = BindingFlags.Static | BindingFlags.NonPublic;
            var version = typeof(Updater).GetField("_availableVersion", flags);
            var client = typeof(Updater).GetField("_client", flags);
            var installing = typeof(Updater).GetField("_installing", flags);
            var oldVersion = version.GetValue(null);
            var oldClient = client.GetValue(null);
            var oldInstalling = installing.GetValue(null);
            using (var busy = new WebClient())
            try
            {
                version.SetValue(null, null); client.SetValue(null, null); installing.SetValue(null, false);
                Assert.IsFalse(Updater.CanInstall);
                Updater.InstallAvailable(); // No candidate: must not start a download.
                Assert.IsNull(client.GetValue(null));
                version.SetValue(null, new Version(2, 1, 0, 0));
                Assert.IsTrue(Updater.CanInstall);
                client.SetValue(null, busy);
                Assert.IsFalse(Updater.CanInstall);
                client.SetValue(null, null); installing.SetValue(null, true);
                Assert.IsFalse(Updater.CanInstall);
            }
            finally
            {
                version.SetValue(null, oldVersion); client.SetValue(null, oldClient); installing.SetValue(null, oldInstalling);
            }
        }

        /// <summary>Windows sees ARCA as both file identity and product.</summary>
        [Test]
        public void BuildIdentityIsArca2()
        {
            var assembly = typeof(Updater).Assembly;
            Assert.AreEqual("ARCA", assembly.GetName().Name);
            Assert.AreEqual(new Version(2, 0, 0, 0), assembly.GetName().Version);
            Assert.AreEqual("ARCA", ((AssemblyTitleAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyTitleAttribute))).Title);
            Assert.AreEqual("ARCA", ((AssemblyProductAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyProductAttribute))).Product);
        }
    }
}
