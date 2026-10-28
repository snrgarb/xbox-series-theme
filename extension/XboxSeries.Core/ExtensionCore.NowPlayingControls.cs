using System.Collections.Generic;
using System.Linq;

namespace XboxSeries.Core
{
    /// <summary>
    /// The Now Playing card's row of Previous, Play/Pause and Next under it, the card's own as a dropdown's devices are
    /// its own, so the card stays the flyout's last entry: down from the card reaches Play/Pause, up from the row returns
    /// to it, and left and right move along it. A on one asks the playing app, and nothing shows it done until the app's
    /// session says so, as Discord's Mute and Deafen wait for Discord; one the app has turned off is dimmed, and A does
    /// nothing on it.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>The app the card's controls ask, as Windows has it: that of the session they were last built for.</summary>
        private string controlledAppId;

        /// <summary>
        /// The card's controls for <paramref name="session"/>: Previous, Play/Pause, Pause while it plays, and Next, each
        /// enabled as the app's playback controls say now.
        /// </summary>
        private IReadOnlyList<GuideItem> MediaControls(MediaSession session)
        {
            controlledAppId = session.AppId;
            return new[]
            {
                new GuideItem(GuideItemKind.MediaPrevious, "Previous") { IsEnabled = session.CanGoPrevious },
                new GuideItem(GuideItemKind.MediaPlayPause, "Play/Pause") { IsEnabled = session.CanTogglePlayPause, IsPlaying = session.IsPlaying },
                new GuideItem(GuideItemKind.MediaNext, "Next") { IsEnabled = session.CanGoNext },
            };
        }

        /// <summary>Whether <paramref name="item"/> is one of the shown card's controls.</summary>
        private bool IsMediaControl(GuideItem item) => item != null && ShownNowPlayingCard()?.Controls?.Contains(item) == true;

        /// <summary>
        /// Moves focus onto, along or off the card's row: down from the card onto Play/Pause, up from the row back to the
        /// card, left and right along it, held at either end; down from the row stays, at the flyout's end. False when
        /// focus is neither on the card nor on its row, or the move is the card's own, up or sideways.
        /// </summary>
        private bool MoveAmongMediaControls(ControllerButton direction)
        {
            var card = ShownNowPlayingCard();
            var focused = Guide.FocusedItem;
            if (card?.Controls == null)
            {
                return false;
            }

            if (focused == card)
            {
                if (direction != ControllerButton.DPadDown)
                {
                    return false;
                }

                sounds.Play(UiSound.Navigate);
                Guide.FocusedItem = card.Controls.First(c => c.Kind == GuideItemKind.MediaPlayPause);
                return true;
            }

            if (!card.Controls.Contains(focused))
            {
                return false;
            }

            if (direction == ControllerButton.DPadUp)
            {
                sounds.Play(UiSound.Navigate);
                Guide.FocusedItem = card;
            }
            else if (direction == ControllerButton.DPadLeft || direction == ControllerButton.DPadRight)
            {
                Guide.FocusedItem = Step(card.Controls, focused, direction == ControllerButton.DPadLeft ? -1 : 1);
            }

            return true;
        }

        /// <summary>
        /// A on one of the card's controls: asks the app to go back, play or pause, or skip. What it does shows only as its
        /// session changes. False for any other entry.
        /// </summary>
        private bool SelectMediaControl(GuideItem item)
        {
            if (!IsMediaControl(item))
            {
                return false;
            }

            var command = item.Kind == GuideItemKind.MediaPrevious ? MediaCommand.Previous
                : item.Kind == GuideItemKind.MediaNext ? MediaCommand.Next
                : MediaCommand.TogglePlayPause;
            mediaSessions.Send(command, controlledAppId);
            return true;
        }

        /// <summary>
        /// Where focus on the card's control <paramref name="control"/> goes as the Audio flyout is rebuilt as
        /// <paramref name="wanted"/>: the same control of the new card, or "No music app is running" in the card's place.
        /// </summary>
        private static GuideItem MediaControlFocusAfterRebuild(GuideItem control, IReadOnlyList<GuideItem> wanted)
        {
            var entry = wanted.Last();
            return entry.Controls?.FirstOrDefault(c => c.Kind == control.Kind) ?? entry;
        }
    }
}
