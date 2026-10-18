using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Threading;

namespace XboxSeries.Extension
{
    /// <summary>
    /// A Steam launch held on a question for the user, such as the game's EULA, read from the client's console log,
    /// and the client's main window, where it asks, put in front of Playnite's: under <c>-silent</c> that window stays
    /// hidden, so the question is never on screen and the launch waits for good, a later Play only joining it (#60).
    /// </summary>
    internal sealed partial class SteamDownloads
    {
        // A launch's step in the console log: "[2026-10-17 19:11:24] GameAction [AppID 1091500, ActionID 1] : LaunchApp
        // waiting for user response to ShowEula """. Every later step of the action logs a line of its own, its
        // answer ("continues with user response") among them.
        private static readonly Regex GameActionLine = new Regex(
            @"^\[(?<time>\d{4}-\d\d-\d\d \d\d:\d\d:\d\d)\] GameAction \[AppID (?<app>\d+), ActionID \d+\] : (?<step>.*)$",
            RegexOptions.Compiled);

        private static readonly Regex WaitingForUser = new Regex(@"waiting for user response to (?<task>\w+)", RegexOptions.Compiled);

        // What the client logs as it starts, when every launch of its last run is gone.
        private const string ConsoleLogStart = "Console Log Start";

        // How long a launch must wait on the user before it counts: Steam also "waits" on itself for a moment between
        // its own steps, such as CreatingProcess and ShowInterstitials, answering within the same second.
        private static readonly TimeSpan QuestionHeldFor = TimeSpan.FromSeconds(3);

        // How long, and how often, Steam's window is looked for to put in front once it's been asked to show.
        private static readonly TimeSpan FrontWaitFor = TimeSpan.FromSeconds(6);
        private static readonly TimeSpan FrontWaitEvery = TimeSpan.FromMilliseconds(250);

        // Steam's main window's own script: shown from the tray and raised, without navigating, so its question stays.
        private const string ShowMainWindow = "SteamClient.Window.ShowWindow(); SteamClient.Window.BringToFront(); 'shown'";

        // The question each Steam launch waits on, by app id, and since when; read on from where the log was last read.
        private readonly Dictionary<uint, LaunchQuestion> launchQuestions = new Dictionary<uint, LaunchQuestion>();
        private long consoleLogRead = -1;
        private bool consoleLogWarned;
        private bool showWarned;

        public bool IsLaunchWaitingOnUser(Guid gameId)
        {
            var appId = AppIdOf(gameId);
            if (appId == null)
            {
                return false;
            }

            try
            {
                ReadConsoleLog();
            }
            catch (Exception e) when (IsReadFailure(e))
            {
                WarnOnce(ref consoleLogWarned, e, "Couldn't read Steam's console log for a launch waiting on the user");
                return false;
            }

            return launchQuestions.TryGetValue(appId.Value, out var question) && DateTime.Now - question.Since >= QuestionHeldFor;
        }

        public void ShowLaunchQuestion(Guid gameId)
        {
            var appId = AppIdOf(gameId);
            if (appId == null)
            {
                return;
            }

            launchQuestions.TryGetValue(appId.Value, out var question);
            logger.Info($"Steam's launch of app {appId} waits on the user at {question?.Task ?? "a question"}, asked {question?.Since:yyyy-MM-dd HH:mm:ss}; showing Steam's window, where it asks");

            // Playnite holds the foreground as a launch begins, so it can let Steam take it as its window shows.
            NativeMethods.AllowSetForegroundWindow(NativeMethods.ASFW_ANY);
            ShowSteamMainWindow(appId.Value);
        }

        /// <summary>
        /// Steam's main window, shown and raised by its own script over CEF when the user has the debugger on, or else
        /// through <c>steam://open/main</c>; then put in front of Playnite's. Returns at the first await, called on the
        /// UI thread, and carries on there, as the timer needs; nothing it does is let out of it.
        /// </summary>
        private async void ShowSteamMainWindow(uint appId)
        {
            try
            {
                string answer = null;
                if (IsCefDebuggingOn)
                {
                    try
                    {
                        answer = await EvaluateInSteam(ShowMainWindow, "Steam");
                    }
                    catch (Exception e) when (e is WebSocketException || e is OperationCanceledException)
                    {
                        logger.Warn(e, "Lost Steam's main window over CEF while showing it");
                    }
                }

                if (answer != "shown")
                {
                    var exe = SteamExe();
                    logger.Info($"Steam's main window over CEF answered '{answer ?? "nothing"}', so it's shown through steam://open/main");
                    if (exe != null)
                    {
                        AskSteam(exe, "steam://open/main", $"app {appId}'s launch question", ref showWarned);
                    }
                }

                PutSteamInFront(appId);
            }
            catch (Exception e)
            {
                logger.Error(e, $"Couldn't show Steam's window for app {appId}'s launch question");
            }
        }

        /// <summary>
        /// Once Steam's window is on screen, hands it the foreground, until Steam holds it or a few seconds have gone,
        /// logging which: the question can only be answered from in front.
        /// </summary>
        private void PutSteamInFront(uint appId)
        {
            var until = DateTime.UtcNow + FrontWaitFor;
            var timer = new DispatcherTimer { Interval = FrontWaitEvery };
            timer.Tick += (_, __) =>
            {
                var front = NativeMethods.GetForegroundWindow();
                if (front != IntPtr.Zero && IsSteamWindow(front))
                {
                    timer.Stop();
                    logger.Info($"Steam's window is in front with app {appId}'s launch question");
                    return;
                }

                if (DateTime.UtcNow >= until)
                {
                    timer.Stop();
                    logger.Warn($"Steam's window didn't come in front with app {appId}'s launch question; {SteamWindows().Count} of its windows are on screen");
                    return;
                }

                var open = SteamWindows();
                if (open.Count > 0)
                {
                    NativeMethods.BringToForeground(open[0]);
                }
            };
            timer.Start();
        }

        /// <summary>
        /// Reads what Steam has added to its console log since the last read, whole lines only, and follows each
        /// launch's question; the first read takes the log from the client's start, its previous log first, should
        /// the client have rolled it over since. Steam rolls the log over by renaming it, so one shorter than what was
        /// read is a new one, read from its start; the launches it was following go on.
        /// </summary>
        private void ReadConsoleLog()
        {
            var steam = SteamPath();
            if (steam == null)
            {
                return;
            }

            var log = Path.Combine(steam, "logs", "console_log.txt");
            if (consoleLogRead < 0)
            {
                ReadLogFrom(Path.Combine(steam, "logs", "console_log.previous.txt"), 0);
                consoleLogRead = 0;
            }

            if (!File.Exists(log))
            {
                return;
            }

            var length = new FileInfo(log).Length;
            if (length < consoleLogRead)
            {
                consoleLogRead = 0;
            }

            if (length > consoleLogRead)
            {
                consoleLogRead = ReadLogFrom(log, consoleLogRead);
            }
        }

        /// <summary>Reads the whole lines of <paramref name="path"/> from <paramref name="offset"/>; where they end.</summary>
        private long ReadLogFrom(string path, long offset)
        {
            if (!File.Exists(path))
            {
                return offset;
            }

            byte[] bytes;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                stream.Seek(offset, SeekOrigin.Begin);
                bytes = new byte[stream.Length - offset];
                var read = 0;
                while (read < bytes.Length)
                {
                    var n = stream.Read(bytes, read, bytes.Length - read);
                    if (n == 0)
                    {
                        break;
                    }

                    read += n;
                }

                if (read < bytes.Length)
                {
                    Array.Resize(ref bytes, read);
                }
            }

            // A line Steam is still writing is left for the next read.
            var whole = Array.LastIndexOf(bytes, (byte)'\n') + 1;
            foreach (var line in Encoding.UTF8.GetString(bytes, 0, whole).Split('\n'))
            {
                FollowLaunch(line.TrimEnd('\r'));
            }

            return offset + whole;
        }

        /// <summary>One line of the console log: a launch's step, or the client starting afresh.</summary>
        private void FollowLaunch(string line)
        {
            if (line.EndsWith(ConsoleLogStart, StringComparison.Ordinal))
            {
                launchQuestions.Clear();
                return;
            }

            var step = GameActionLine.Match(line);
            if (!step.Success || !uint.TryParse(step.Groups["app"].Value, out var appId))
            {
                return;
            }

            var waiting = WaitingForUser.Match(step.Groups["step"].Value);
            if (waiting.Success
                && DateTime.TryParseExact(step.Groups["time"].Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var since))
            {
                launchQuestions[appId] = new LaunchQuestion(waiting.Groups["task"].Value, since);
            }
            else
            {
                launchQuestions.Remove(appId);
            }
        }

        /// <summary>The question a launch waits on, by Steam's name for its step, such as ShowEula, and since when.</summary>
        private sealed class LaunchQuestion
        {
            public LaunchQuestion(string task, DateTime since)
            {
                Task = task;
                Since = since;
            }

            public string Task { get; }

            public DateTime Since { get; }
        }
    }
}
