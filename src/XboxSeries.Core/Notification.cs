using System;

namespace XboxSeries.Core
{
    /// <summary>One of Playnite's own notifications.</summary>
    public sealed class Notification
    {
        public Notification(string id, string text)
        {
            Id = id;
            Text = text;
        }

        public string Id { get; }
        public string Text { get; }

        /// <summary>What its Toast tells of: a screenshot, drawn with a camera, when its id or text speaks of one.</summary>
        public ToastKind Kind => Mentions(Id, "screenshot") || Mentions(Text, "screenshot") ? ToastKind.Screenshot : ToastKind.Notification;

        private static bool Mentions(string said, string word) => said != null && said.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
