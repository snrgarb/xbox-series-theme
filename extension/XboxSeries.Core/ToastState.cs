using System;
using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>What a Toast tells of, which picks the image on its tile.</summary>
    public enum ToastKind
    {
        /// <summary>One of Playnite's notifications: the bell.</summary>
        Notification,

        /// <summary>A Playnite notification about a screenshot: a camera.</summary>
        Screenshot,

        /// <summary>A known DM ringing: the caller's avatar, or Discord's mark.</summary>
        Call,

        /// <summary>"Hi X!" as a profile finishes signing in: the profile's picture, with no "Press ⓧ to view".</summary>
        Greeting,

        /// <summary>"Queued" as a game joins the queue: the game's cover; ⓧ goes to Manage Queue rather than the Guide.</summary>
        Queued,

        /// <summary>"Ready to play" as a queued game finishes installing: the game's cover; ⓧ starts the game.</summary>
        Installed,

        /// <summary>"X was uninstalled" as a game the extension uninstalled through its client is gone: the game's cover.</summary>
        Uninstalled,

        /// <summary>
        /// "Achievement unlocked" as the game running now, or the one that just exited, unlocks one: the console's pill, in
        /// green, with the achievement's icon, paging from its header, its name as <see cref="ToastState.Detail"/> and its
        /// <see cref="ToastState.Score"/> to its <see cref="ToastState.Description"/>, then the prompt. Several recorded
        /// together are one Toast, "&lt;n&gt; achievements unlocked", the same pill with no icon, name, Score or
        /// description. ⓧ opens the Guide at Profile &amp; system.
        /// </summary>
        AchievementUnlocked,

        /// <summary>
        /// "Rare Achievement unlocked - &lt;n&gt;%" as one unlock few players have is told of: the console's rare Toast, a
        /// blue pill with a spinning diamond, paging from its header, Score and name to its <see cref="ToastState.Description"/>,
        /// then the prompt, with a sound of its own. Several recorded together are an ordinary <see cref="AchievementUnlocked"/>.
        /// </summary>
        RareAchievementUnlocked,

        /// <summary>
        /// "Library updated" as the update after a game finished downloading is done: a glyph, no cover, with
        /// "&lt;n&gt; games added" as its <see cref="ToastState.Detail"/> when it added any.
        /// </summary>
        LibraryUpdated,
    }

    /// <summary>
    /// The Toast at the bottom of the screen: a new Playnite notification, or a known DM ringing, shown above every
    /// window without taking focus. Views bind to it and hold no logic of their own.
    /// </summary>
    public sealed class ToastState : INotifyPropertyChanged
    {
        private string text = "";
        private bool isShown;
        private ToastKind kind;
        private string image;
        private string detail;
        private int? score;
        private double? rarity;
        private string description;
        private TimeSpan shownFor;
        private bool hasPrompt;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>The notification's text. It stays as the Toast hides, so it doesn't blank while sliding away.</summary>
        public string Text
        {
            get => text;
            internal set
            {
                if (text != value)
                {
                    text = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
                }
            }
        }

        /// <summary>
        /// What the Toast tells of, which picks its tile's image. A call's <see cref="Text"/> is "X is calling", and the
        /// view fades it with "Press ⓧ to view", as it does every Toast's. Like <see cref="Text"/>, it stays as the Toast hides.
        /// </summary>
        public ToastKind Kind
        {
            get => kind;
            internal set
            {
                if (kind != value)
                {
                    kind = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Kind)));
                }
            }
        }

        /// <summary>
        /// The image the tile shows in place of its kind's own, as the caller's avatar for a call; null for the kind's
        /// own. Like <see cref="Text"/>, it stays as the Toast hides.
        /// </summary>
        public string Image
        {
            get => image;
            internal set
            {
                if (image != value)
                {
                    image = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image)));
                }
            }
        }

        /// <summary>
        /// A second line under <see cref="Text"/>: an Achievement unlocked Toast's achievement name; null for none. Like
        /// <see cref="Text"/>, it stays as the Toast hides.
        /// </summary>
        public string Detail
        {
            get => detail;
            internal set
            {
                if (detail != value)
                {
                    detail = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Detail)));
                }
            }
        }

        /// <summary>
        /// An Achievement unlocked Toast's achievement's Score, shown beside <see cref="Detail"/>; null for none. Like
        /// <see cref="Text"/>, it stays as the Toast hides.
        /// </summary>
        public int? Score
        {
            get => score;
            internal set
            {
                if (score != value)
                {
                    score = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Score)));
                }
            }
        }

        /// <summary>
        /// A rare achievement's rarity, the percentage of players who have it, for its header's "- &lt;n&gt;%"; null for any
        /// other Toast. Like <see cref="Text"/>, it stays as the Toast hides.
        /// </summary>
        public double? Rarity
        {
            get => rarity;
            internal set
            {
                if (rarity != value)
                {
                    rarity = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Rarity)));
                }
            }
        }

        /// <summary>
        /// An Achievement unlocked Toast's achievement's description, rare or not, its pill's second page; null for none,
        /// for "&lt;n&gt; achievements unlocked" and for any other Toast. Like
        /// <see cref="Text"/>, it stays as the Toast hides.
        /// </summary>
        public string Description
        {
            get => description;
            internal set
            {
                if (description != value)
                {
                    description = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
                }
            }
        }

        /// <summary>
        /// How long an achievement's Toast shows before its outro: the console's 8.5 s, and longer for a description of more
        /// than a page, so its view can hold every page as the console holds one; zero for any other Toast. Like
        /// <see cref="Text"/>, it stays as the Toast hides.
        /// </summary>
        public TimeSpan ShownFor
        {
            get => shownFor;
            internal set
            {
                if (shownFor != value)
                {
                    shownFor = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShownFor)));
                }
            }
        }

        /// <summary>
        /// Whether the Toast says what the Guide button does with it, "Press ⓧ to view" (or "to start"): not for a
        /// greeting, which has nothing to view, nor for any Toast with the Guide off, when the button does nothing. Like
        /// <see cref="Text"/>, it stays as the Toast hides.
        /// </summary>
        public bool HasPrompt
        {
            get => hasPrompt;
            internal set
            {
                if (hasPrompt != value)
                {
                    hasPrompt = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasPrompt)));
                }
            }
        }

        public bool IsShown
        {
            get => isShown;
            internal set
            {
                if (isShown != value)
                {
                    isShown = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsShown)));
                }
            }
        }

        /// <summary>
        /// The Toast shows as told: its line, kind and image, whether it has the Guide button's prompt, and the
        /// achievement's name, Score, rarity and description where it has them. Every field is set at once, so nothing of
        /// the Toast before lingers on this one.
        /// </summary>
        internal void Show(string text, ToastKind kind, string image, bool hasPrompt, string detail = null, int? score = null, double? rarity = null,
            string description = null, TimeSpan shownFor = default(TimeSpan))
        {
            ShownFor = shownFor;
            HasPrompt = hasPrompt;
            Text = text;
            Kind = kind;
            Image = image;
            Detail = detail;
            Score = score;
            Rarity = rarity;
            Description = description;
            IsShown = true;
        }
    }
}
