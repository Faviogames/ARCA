using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Threading;

namespace WinMemoryCleaner
{
    /// <summary>
    /// Updates ARCA from the latest GitHub release of Faviogames/ARCA when <see cref="Settings.AutoUpdate"/> is on.
    /// The release must publish ARCA.exe as a standalone asset.
    /// </summary>
    public static class Updater
    {
        private static WebClient _client;
        private static DateTimeOffset _lastCheck = DateTimeOffset.MinValue;
        private static DispatcherTimer _timer;

        internal static ProcessStartInfo Process;

        /// <summary>
        /// Normalizes a version to four parts so 1.0.2 equals 1.0.2.0.
        /// </summary>
        internal static Version Normalize(Version version)
        {
            return new Version(Math.Max(version.Major, 0), Math.Max(version.Minor, 0), Math.Max(version.Build, 0), Math.Max(version.Revision, 0));
        }

        /// <summary>
        /// Reads the release version from the GitHub API response ("tag_name": "v1.0.2").
        /// </summary>
        internal static Version ParseReleaseVersion(string json)
        {
            var match = Regex.Match(json ?? string.Empty, @"""tag_name""\s*:\s*""v?(\d+(?:\.\d+){1,3})""");

            return match.Success ? Normalize(Version.Parse(match.Groups[1].Value)) : null;
        }

        private static WebClient CreateClient()
        {
            var client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = Constants.App.Title; // Required by the GitHub API
            return client;
        }

        private static void OnFileDownloadCompleted(object sender, AsyncCompletedEventArgs e)
        {
            try
            {
                if (e.Error != null)
                    throw new Exception("File download failed.", e.Error);

                if (e.Cancelled)
                    return;

                var updateInfo = (Tuple<string, Version>)e.UserState;
                var temp = updateInfo.Item1;
                var newestVersion = updateInfo.Item2;

                if (!File.Exists(temp) || !Normalize(AssemblyName.GetAssemblyName(temp).Version).Equals(newestVersion))
                {
                    Helper.DeleteFile(temp);
                    throw new Exception("Downloaded file does not match version " + newestVersion + ".");
                }

                // Wait for this process to exit, replace the exe and reopen it. The version argument shows the "updated" notification.
                Process = new ProcessStartInfo
                {
                    Arguments = string.Format(CultureInfo.InvariantCulture, @"/c taskkill /f /pid {0} & ping -n 3 127.0.0.1 >nul & move /y ""{1}"" ""{2}"" & start """" ""{2}"" /{3}", System.Diagnostics.Process.GetCurrentProcess().Id, temp, App.Path, newestVersion),
                    CreateNoWindow = true,
                    FileName = "cmd",
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                Logger.Information("Updating to version " + newestVersion + ".");

                App.Shutdown();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);

                Reset();
            }
        }

        private static void OnVersionCheckCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                if (e.Error != null)
                    throw new Exception("Version check failed.", e.Error);

                if (e.Cancelled)
                    return;

                var newestVersion = ParseReleaseVersion(e.Result);

                if (newestVersion == null || Normalize(App.Version) >= newestVersion)
                {
                    Reset();
                    return;
                }

                var temp = Path.Combine(Path.GetTempPath(), Path.GetFileName(App.Path) + ".update");

                Helper.DeleteFile(temp);

                _client.Headers[HttpRequestHeader.UserAgent] = Constants.App.Title;
                _client.DownloadFileAsync(Constants.App.Repository.LatestExeUri, temp, Tuple.Create(temp, newestVersion));
            }
            catch (Exception ex)
            {
                Logger.Error(ex);

                Reset();
            }
        }

        private static void Reset()
        {
            try
            {
                if (_client != null)
                    _client.Dispose();
            }
            finally
            {
                _client = null;
                Process = null;
            }
        }

        /// <summary>
        /// Checks now and then every hour; each check is throttled to <see cref="Constants.App.AutoUpdateInterval"/>.
        /// Must be called from the UI thread.
        /// </summary>
        public static void Start()
        {
            if (_timer != null)
                return;

            _timer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
            _timer.Tick += (sender, e) => Update();
            _timer.Start();

            Update();
        }

        /// <summary>
        /// Check for new version and update if available
        /// </summary>
        /// <param name="force">Ignore the check interval (used when the user turns the option on).</param>
        public static void Update(bool force = false)
        {
            try
            {
                if (!Settings.AutoUpdate || !Helper.IsAutoUpdateSupported || _client != null || App.Path == null || App.Version == null)
                    return;

                if (!force && DateTimeOffset.Now.Subtract(_lastCheck).TotalHours < Constants.App.AutoUpdateInterval)
                    return;

                _lastCheck = DateTimeOffset.Now;

                _client = CreateClient();
                _client.DownloadFileCompleted += OnFileDownloadCompleted;
                _client.DownloadStringCompleted += OnVersionCheckCompleted;

                ServicePointManager.DefaultConnectionLimit = 10;
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072 | (SecurityProtocolType)12288; // TLS 1.2 | TLS 1.3

                _client.DownloadStringAsync(Constants.App.Repository.LatestReleaseApiUri);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);

                Reset();
            }
        }
    }
}
