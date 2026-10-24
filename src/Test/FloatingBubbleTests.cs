using System;
using System.Windows.Input;
using NUnit.Framework;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace WinMemoryCleaner.Test
{
    /// <summary>
    /// RamPrototype: floating bubble visibility decisions and state transitions (no real memory cleaning)
    /// </summary>
    [TestFixture]
    public sealed class FloatingBubbleTests
    {
        private const int OwnPid = 100;
        private const int ShellPid = 200;
        private const int AppPid = 300;

        #region Classify

        [TestFixture]
        public class ClassifyTests
        {
            [Test]
            public void Progman_FromShell_IsDesktop()
            {
                Assert.AreEqual(Enums.Desktop.Foreground.Desktop, DesktopVisibilityController.Classify("Progman", ShellPid, ShellPid, OwnPid, true));
            }

            [Test]
            public void WorkerW_WithDefView_FromShell_IsDesktop()
            {
                Assert.AreEqual(Enums.Desktop.Foreground.Desktop, DesktopVisibilityController.Classify("WorkerW", ShellPid, ShellPid, OwnPid, true));
            }

            [Test]
            public void WorkerW_WithoutDefView_IsOther()
            {
                Assert.AreEqual(Enums.Desktop.Foreground.Other, DesktopVisibilityController.Classify("WorkerW", ShellPid, ShellPid, OwnPid, false));
            }

            [TestCase("CabinetWClass")]                 // File Explorer folder
            [TestCase("Shell_TrayWnd")]                 // Taskbar
            [TestCase("XamlExplorerHostIslandWindow")]  // Alt+Tab / Task View
            [TestCase("MultitaskingViewFrame")]         // Alt+Tab (older builds)
            public void ShellProcess_NonDesktopWindow_IsOther(string className)
            {
                Assert.AreEqual(Enums.Desktop.Foreground.Other, DesktopVisibilityController.Classify(className, ShellPid, ShellPid, OwnPid, false));
            }

            [TestCase("Chrome_WidgetWin_1")]
            [TestCase("UnityWndClass")]
            [TestCase("Windows.UI.Core.CoreWindow")]    // Start menu
            public void OtherProcess_IsOther(string className)
            {
                Assert.AreEqual(Enums.Desktop.Foreground.Other, DesktopVisibilityController.Classify(className, AppPid, ShellPid, OwnPid, false));
            }

            [Test]
            public void Progman_FromNonShellProcess_IsOther()
            {
                Assert.AreEqual(Enums.Desktop.Foreground.Other, DesktopVisibilityController.Classify("Progman", AppPid, ShellPid, OwnPid, true));
            }

            [Test]
            public void OwnProcess_IsOwnProcess()
            {
                Assert.AreEqual(Enums.Desktop.Foreground.OwnProcess, DesktopVisibilityController.Classify("Popup", OwnPid, ShellPid, OwnPid, false));
            }

            [Test]
            public void NoShell_IsUnknown()
            {
                Assert.AreEqual(Enums.Desktop.Foreground.Unknown, DesktopVisibilityController.Classify("Progman", ShellPid, 0, OwnPid, true));
            }

            [TestCase(null, ShellPid)]
            [TestCase("", ShellPid)]
            [TestCase("Progman", 0)]
            public void MissingWindowData_IsUnknown(string className, int processId)
            {
                Assert.AreEqual(Enums.Desktop.Foreground.Unknown, DesktopVisibilityController.Classify(className, processId, ShellPid, OwnPid, true));
            }
        }

        #endregion

        [TestFixture]
        [RequiresSTA]
        public class WindowLifecycleTests
        {
            [Test]
            public void ShowBubble_RestoresAfterShellMinimization_WithoutActivation()
            {
                var window = new FloatingBubbleWindow(new FloatingBubbleViewModel(new RelayCommand(() => { })));
                try
                {
                    window.ShowBubble();
                    window.WindowState = System.Windows.WindowState.Minimized;
                    window.ShowBubble();
                    Assert.AreEqual(System.Windows.WindowState.Normal, window.WindowState);
                    Assert.IsTrue(window.IsVisible);
                    Assert.IsFalse(window.ShowActivated);
                    Assert.IsFalse(window.ShowInTaskbar);
                    Assert.IsFalse(window.Topmost);
                    window.HideBubble();
                    Assert.IsFalse(window.IsVisible);
                }
                finally { window.Close(); }
            }

            [Test]
            public void ClosingDuringCleaning_ReleasesAnimationAndSubscriptions()
            {
                var model = new FloatingBubbleViewModel(new RelayCommand(() => { }));
                var window = new FloatingBubbleWindow(model);
                window.ShowBubble();
                model.OnOptimizationStarted();
                window.Close();
                Assert.DoesNotThrow(() => model.OnOptimizationFinished(new OptimizationResult(true, 1, 2)));
            }
        }

        #region ShouldShow

        [TestFixture]
        public class ShouldShowTests
        {
            [Test]
            public void MinimizedWindow_DesktopForeground_Shows()
            {
                Assert.IsTrue(DesktopVisibilityController.ShouldShow(true, false, false, Enums.Desktop.Foreground.Desktop, false));
            }

            [Test]
            public void MainWindowOpen_Hides()
            {
                Assert.IsFalse(DesktopVisibilityController.ShouldShow(true, true, false, Enums.Desktop.Foreground.Desktop, true));
            }

            [Test]
            public void Disabled_Hides()
            {
                Assert.IsFalse(DesktopVisibilityController.ShouldShow(false, false, false, Enums.Desktop.Foreground.Desktop, true));
            }

            [Test]
            public void SessionLocked_Hides()
            {
                Assert.IsFalse(DesktopVisibilityController.ShouldShow(true, false, true, Enums.Desktop.Foreground.Desktop, true));
            }

            [TestCase(Enums.Desktop.Foreground.Other)]
            [TestCase(Enums.Desktop.Foreground.Unknown)]
            public void NonDesktopForeground_Hides(Enums.Desktop.Foreground foreground)
            {
                Assert.IsFalse(DesktopVisibilityController.ShouldShow(true, false, false, foreground, true));
            }

            [TestCase(true)]
            [TestCase(false)]
            public void OwnProcessForeground_KeepsCurrentState(bool isCurrentlyVisible)
            {
                Assert.AreEqual(isCurrentlyVisible, DesktopVisibilityController.ShouldShow(true, false, false, Enums.Desktop.Foreground.OwnProcess, isCurrentlyVisible));
            }
        }

        #endregion

        #region FloatingBubbleViewModel

        [TestFixture]
        public class ViewModelTests
        {
            private sealed class FakeCommand : ICommand
            {
                public bool CanRun = true;
                public int Executions;

                public event EventHandler CanExecuteChanged
                {
                    add { }
                    remove { }
                }

                public bool CanExecute(object parameter)
                {
                    return CanRun;
                }

                public void Execute(object parameter)
                {
                    Executions++;
                }
            }

            [Test]
            public void Constructor_WithNullCommand_Throws()
            {
                Assert.Throws<ArgumentNullException>(() => new FloatingBubbleViewModel(null));
            }

            [Test]
            public void Clean_WhenReady_ExecutesOnce()
            {
                var command = new FakeCommand();
                var viewModel = new FloatingBubbleViewModel(command);

                Assert.IsTrue(viewModel.Clean());
                Assert.AreEqual(1, command.Executions);
            }

            [Test]
            public void Clean_WhileCleaning_IsIgnored()
            {
                var command = new FakeCommand();
                var viewModel = new FloatingBubbleViewModel(command);

                viewModel.OnOptimizationStarted();

                Assert.IsFalse(viewModel.Clean());
                Assert.IsFalse(viewModel.Clean());
                Assert.AreEqual(0, command.Executions);
            }

            [Test]
            public void Clean_WhenCommandCannotExecute_IsIgnored()
            {
                var command = new FakeCommand { CanRun = false };
                var viewModel = new FloatingBubbleViewModel(command);

                Assert.IsFalse(viewModel.Clean());
                Assert.AreEqual(0, command.Executions);
            }

            [Test]
            public void Finished_WithSuccess_ShowsCompletedThenReady()
            {
                var viewModel = new FloatingBubbleViewModel(new FakeCommand());

                viewModel.OnOptimizationStarted();
                Assert.AreEqual(Enums.FloatingBubble.State.Cleaning, viewModel.State);

                viewModel.OnOptimizationFinished(new OptimizationResult(true, 1000, 2000));
                Assert.AreEqual(Enums.FloatingBubble.State.Completed, viewModel.State);

                viewModel.ResetToReady();
                Assert.AreEqual(Enums.FloatingBubble.State.Ready, viewModel.State);
            }

            [Test]
            public void Finished_WithErrors_ShowsErrorAndAllowsRetry()
            {
                var command = new FakeCommand();
                var viewModel = new FloatingBubbleViewModel(command);

                viewModel.OnOptimizationStarted();
                viewModel.OnOptimizationFinished(new OptimizationResult(false, 1000, 5000));

                Assert.AreEqual(Enums.FloatingBubble.State.Error, viewModel.State);
                Assert.IsTrue(viewModel.Clean());
                Assert.AreEqual(1, command.Executions);
            }

            [Test]
            public void ResetToReady_WhileCleaning_KeepsCleaning()
            {
                var viewModel = new FloatingBubbleViewModel(new FakeCommand());

                viewModel.OnOptimizationStarted();
                viewModel.ResetToReady();

                Assert.AreEqual(Enums.FloatingBubble.State.Cleaning, viewModel.State);
            }

            [Test]
            public void ResultText_NegativeDelta_IsReportedAsNegative()
            {
                var viewModel = new FloatingBubbleViewModel(new FakeCommand());

                viewModel.OnOptimizationFinished(new OptimizationResult(true, 2L * 1024 * 1024 * 1024, 1L * 1024 * 1024 * 1024));

                StringAssert.Contains("−", viewModel.ResultText);
            }

            [Test]
            public void ResultText_NoChange_DoesNotInventAmount()
            {
                var viewModel = new FloatingBubbleViewModel(new FakeCommand());

                viewModel.OnOptimizationFinished(new OptimizationResult(true, 1000, 1000));

                StringAssert.DoesNotContain("+", viewModel.ResultText);
                StringAssert.DoesNotContain("−", viewModel.ResultText);
            }

            [Test]
            public void Update_SetsUsedPercentage()
            {
                var viewModel = new FloatingBubbleViewModel(new FakeCommand());

                viewModel.Update(Mocker.CreateHighMemoryUsage());

                Assert.AreEqual(80, viewModel.UsedPercentage);
            }

            [Test]
            public void OptimizationResult_Delta_CanBeNegative()
            {
                Assert.AreEqual(-500, new OptimizationResult(true, 1500, 1000).PhysicalFreeDelta);
            }
        }

        #endregion
    }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
