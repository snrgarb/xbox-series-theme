using System;
using System.IO;
using System.Text;

namespace XboxSeries.BootShell
{
    /// <summary>
    /// Reads what Playnite adds to its log, from where it ended when this was made, to hear its crash handler: Playnite
    /// logs "Unhandled exception occured." just before it shows its crash window or an error box.
    /// </summary>
    internal sealed class PlayniteLog
    {
        public const string CrashLine = "Unhandled exception occured.";

        private readonly string path;
        private long read;
        private string partial = "";

        public PlayniteLog(string path)
        {
            this.path = path;
            read = File.Exists(path) ? new FileInfo(path).Length : 0;
        }

        public static string DefaultPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Playnite", "playnite.log");

        /// <summary>True when the lines Playnite has added since the last call tell of a crash.</summary>
        public bool CrashLogged()
        {
            if (!File.Exists(path))
            {
                return false;
            }

            var length = new FileInfo(path).Length;
            if (length < read)
            {
                // A new log: Playnite rolled the old one over.
                read = 0;
                partial = "";
            }

            if (length == read)
            {
                return false;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            stream.Seek(read, SeekOrigin.Begin);
            var bytes = new byte[length - read];
            var count = stream.ReadAtLeast(bytes, bytes.Length, throwOnEndOfStream: false);
            read += count;

            // A line cut short at the end waits for the next call.
            var text = partial + Encoding.UTF8.GetString(bytes, 0, count);
            var end = text.LastIndexOf('\n') + 1;
            partial = text.Substring(end);
            return text.AsSpan(0, end).Contains(CrashLine, StringComparison.Ordinal);
        }
    }
}
