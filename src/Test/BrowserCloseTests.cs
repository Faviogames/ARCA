using System;
using NUnit.Framework;

#pragma warning disable CS1591
namespace WinMemoryCleaner.Test
{
    [TestFixture]
    public class BrowserCloseTests
    {
        private sealed class FakeProcess : IBrowserProcess
        {
            public string Name { get; set; }
            public int SessionId { get; set; }
            public bool HasExited { get; set; }
            public bool WasKilled;
            public bool WasDisposed;
            public bool DenyKill;
            public bool NeverExits;
            public void Kill()
            {
                WasKilled = true;
                if (DenyKill) throw new InvalidOperationException("Simulated access denied");
                if (!NeverExits) HasExited = true;
            }
            public bool WaitForExit(int milliseconds) { Assert.LessOrEqual(milliseconds, 3000); return HasExited; }
            public void Dispose() { WasDisposed = true; }
        }

        [TestCase("msedge")]
        [TestCase("CHROME.EXE")]
        [TestCase("opera")]
        [TestCase("opera_gx")]
        [TestCase("firefox")]
        [TestCase("brave")]
        [TestCase("vivaldi")]
        [TestCase("chromium")]
        public void KnownBrowser_IsSupported(string name) { Assert.IsTrue(BrowserCloseService.IsSupportedBrowser(name)); }

        [TestCase("msedgewebview2")]
        [TestCase("browser")]
        [TestCase("launcher")]
        [TestCase("explorer")]
        [TestCase("chatgpt")]
        [TestCase("chrome_proxy")]
        [TestCase("")]
        [TestCase((string)null)]
        public void OtherApplication_IsNeverMatched(string name) { Assert.IsFalse(BrowserCloseService.IsSupportedBrowser(name)); }

        [Test]
        public void Unchecked_DoesNotInvokeShutdown()
        {
            Assert.IsNull(BrowserCloseService.CloseIfRequested(false, () => { Assert.Fail("Must not close a browser"); return null; }));
        }

        [Test]
        public void Checked_RequestsShutdownExactlyOnce()
        {
            var count = 0;
            var expected = new BrowserCloseResult();
            Assert.AreSame(expected, BrowserCloseService.CloseIfRequested(true, () => { count++; return expected; }));
            Assert.AreEqual(1, count);
        }

        [Test]
        public void Close_OnlyTargetsKnownBrowsersInCurrentSession()
        {
            var edge = new FakeProcess { Name = "msedge", SessionId = 2 };
            var opera = new FakeProcess { Name = "opera", SessionId = 2 };
            var webView = new FakeProcess { Name = "msedgewebview2", SessionId = 2 };
            var otherUser = new FakeProcess { Name = "chrome", SessionId = 7 };
            var result = BrowserCloseService.Close(new[] { edge, opera, webView, otherUser }, 2);
            Assert.IsTrue(edge.WasKilled && opera.WasKilled);
            Assert.IsFalse(webView.WasKilled || otherUser.WasKilled);
            Assert.AreEqual(2, result.Closed);
            Assert.AreEqual(0, result.Failed);
            Assert.IsTrue(edge.WasDisposed && opera.WasDisposed && webView.WasDisposed && otherUser.WasDisposed);
        }

        [Test]
        public void Close_PartialFailure_IsReportedAndDoesNotBlockOthers()
        {
            var blocked = new FakeProcess { Name = "chrome", SessionId = 2, DenyKill = true };
            var firefox = new FakeProcess { Name = "firefox", SessionId = 2 };
            var result = BrowserCloseService.Close(new[] { blocked, firefox }, 2);
            Assert.AreEqual(1, result.Closed);
            Assert.AreEqual(1, result.Failed);
            Assert.IsTrue(blocked.WasDisposed && firefox.WasDisposed);
        }

        [Test]
        public void Close_ProcessDoesNotExit_IsNotReportedClosed()
        {
            var process = new FakeProcess { Name = "chrome", SessionId = 2, NeverExits = true };
            var result = BrowserCloseService.Close(new[] { process }, 2);
            Assert.AreEqual(0, result.Closed);
            Assert.AreEqual(1, result.Failed);
        }

        [Test]
        public void Close_AlreadyExited_DoesNotKillAgain()
        {
            var process = new FakeProcess { Name = "chrome", SessionId = 2, HasExited = true };
            var result = BrowserCloseService.Close(new[] { process }, 2);
            Assert.IsFalse(process.WasKilled);
            Assert.AreEqual(0, result.Failed);
        }
    }
}
#pragma warning restore CS1591
