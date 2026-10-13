using System;
using System.Collections.Generic;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// The extension's single facade. Controller button events, library snapshots and port events go in;
    /// view state comes out, and commands go out through the ports.
    /// </summary>
    public sealed class ExtensionCore
    {
        /// <summary>How many recent games fit on the landing tab, as on the console.</summary>
        private const int RecentGameCount = 4;

        private static readonly TimeSpan ReleaseFallback = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan RepeatDelay = TimeSpan.FromMilliseconds(700);
        private static readonly TimeSpan RepeatRate = TimeSpan.FromMilliseconds(80);

        private readonly IClock clock;
        private readonly IWindowSystem windowSystem;
        private readonly IPlayniteHost playnite;
        private LibrarySnapshot library = LibrarySnapshot.Empty;
        private Guid? runningGame;
        private IntPtr recordedForeground;
        private ControllerButton? dismissButton;
        private IDisposable releaseFallback;
        private Action pendingHandBack;
        private ControllerButton? heldDirection;
        private IDisposable repeatTimer;

        public ExtensionCore(IClock clock, IWindowSystem windowSystem, IPlayniteHost playnite)
        {
            this.clock = clock;
            this.windowSystem = windowSystem;
            this.playnite = playnite;
        }

        public GuideState Guide { get; } = new GuideState();

        public void OnLibraryChanged(LibrarySnapshot snapshot)
        {
            library = snapshot;
        }

        /// <summary>Playnite's game started event.</summary>
        public void OnGameStarted(Guid gameId)
        {
            runningGame = gameId;
        }

        /// <summary>Playnite's game stopped event.</summary>
        public void OnGameStopped(Guid gameId)
        {
            if (runningGame == gameId)
            {
                runningGame = null;
            }
        }

        public void OnControllerButton(ControllerButton button, ButtonState state)
        {
            if (state == ButtonState.Released)
            {
                if (button == dismissButton)
                {
                    HandFocusBack();
                }

                if (button == heldDirection)
                {
                    StopRepeat();
                }

                return;
            }

            if (button == ControllerButton.Guide && !Guide.IsOpen)
            {
                if (Guide.IsOverlayShown)
                {
                    // Reopened before focus was handed back: the Overlay is still in front, so keep the window recorded first.
                    CancelReleaseWait();
                }
                else
                {
                    recordedForeground = windowSystem.ForegroundWindow;
                }

                ShowLandingTab();
                Guide.IsOverlayShown = true;
                Guide.IsOpen = true;

                // Taking the foreground can fail; the Overlay is Topmost and the Guide follows Playnite's callback either way.
                windowSystem.TakeForeground();
            }
            else if ((button == ControllerButton.Guide || button == ControllerButton.B) && Guide.IsOpen)
            {
                Dismiss(button, ReturnToRecordedWindow);
            }
            else if (button == ControllerButton.A && Guide.IsOpen)
            {
                Select(Guide.FocusedItem);
            }
            else if ((button == ControllerButton.DPadUp || button == ControllerButton.DPadDown) && Guide.IsOpen)
            {
                StopRepeat();
                var by = button == ControllerButton.DPadUp ? -1 : 1;
                MoveFocus(by);
                heldDirection = button;
                repeatTimer = clock.After(RepeatDelay, () => Repeat(by));
            }
        }

        private void Select(GuideItem item)
        {
            switch (item.Kind)
            {
                case GuideItemKind.Home:
                    Dismiss(ControllerButton.A, () => playnite.BringToFront(DashboardPage.Home));
                    break;
                case GuideItemKind.MyGamesAndApps:
                    Dismiss(ControllerButton.A, () => playnite.BringToFront(DashboardPage.MyGamesAndApps));
                    break;
                case GuideItemKind.RunningGame:
                    Dismiss(ControllerButton.A, ReturnToRecordedWindow);
                    break;
                case GuideItemKind.RecentGame:
                    // With another game running, "Quit X and start Y?" comes first (#24); until then A does nothing.
                    if (runningGame == null)
                    {
                        var gameId = item.GameId.Value;
                        Dismiss(ControllerButton.A, () =>
                        {
                            ReturnToRecordedWindow();
                            playnite.StartGame(gameId);
                        });
                    }

                    break;
            }
        }

        /// <summary>
        /// Closes the Guide from the pad. Focus is handed back by <paramref name="handBack"/> only once
        /// <paramref name="button"/> is released (or after a 1 s fallback), so the window behind never sees it.
        /// </summary>
        private void Dismiss(ControllerButton button, Action handBack)
        {
            CloseGuide();
            dismissButton = button;
            pendingHandBack = handBack;
            releaseFallback = clock.After(ReleaseFallback, HandFocusBack);
        }

        /// <summary>D-pad hold-repeat, at Playnite's own speed.</summary>
        private void Repeat(int by)
        {
            MoveFocus(by);
            repeatTimer = clock.After(RepeatRate, () => Repeat(by));
        }

        private void StopRepeat()
        {
            heldDirection = null;
            repeatTimer?.Dispose();
            repeatTimer = null;
        }

        private void MoveFocus(int by)
        {
            var items = Guide.Items.ToList();
            var index = Math.Max(0, Math.Min(items.Count - 1, items.IndexOf(Guide.FocusedItem) + by));
            Guide.FocusedItem = items[index];
        }

        /// <summary>Esc, or a click on the dimmed area. No pad button is held, so focus goes back at once.</summary>
        public void DismissGuide()
        {
            if (!Guide.IsOpen)
            {
                return;
            }

            CloseGuide();
            pendingHandBack = ReturnToRecordedWindow;
            HandFocusBack();
        }

        private void CloseGuide()
        {
            StopRepeat();
            Guide.IsOpen = false;
        }

        /// <summary>The Guide always opens here: the landing tab, focus on Home.</summary>
        private void ShowLandingTab()
        {
            var running = library.Games.FirstOrDefault(g => g.Id == runningGame);
            var recent = library.Games
                .Where(g => g.LastActivity != null && !g.Hidden && g != running)
                .OrderByDescending(g => g.LastActivity)
                .Take(RecentGameCount);
            var games = running == null ? recent : new[] { running }.Concat(recent);

            // A divider separates the games from Home and My games & apps.
            var home = new GuideItem(GuideItemKind.Home, "Home");
            Guide.Items = new[] { home, new GuideItem(GuideItemKind.MyGamesAndApps, "My games & apps") }
                .Concat(games.Select((g, index) => new GuideItem(
                    g == running ? GuideItemKind.RunningGame : GuideItemKind.RecentGame, g.Name, g.Id, hasDividerAbove: index == 0)))
                .ToList();
            Guide.FocusedItem = home;
        }

        private void HandFocusBack()
        {
            var handBack = pendingHandBack;
            CancelReleaseWait();
            handBack();
            Guide.IsOverlayShown = false;
        }

        /// <summary>Back to the window that was in front when the Guide opened, or to Playnite if it has gone.</summary>
        private void ReturnToRecordedWindow()
        {
            if (windowSystem.Exists(recordedForeground))
            {
                windowSystem.SetForeground(recordedForeground);
            }
            else
            {
                playnite.BringToFront();
            }
        }

        private void CancelReleaseWait()
        {
            dismissButton = null;
            pendingHandBack = null;
            releaseFallback?.Dispose();
            releaseFallback = null;
        }
    }
}
