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
    }
}
