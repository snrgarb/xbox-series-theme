using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Playnite.SDK.Data;

namespace XboxSeries.Extension
{
    /// <summary>
    /// The client's CEF debugger, which Steam opens on 127.0.0.1 at each start while the file it looks for is in its
    /// folder: running script in its SharedJSContext, the script that runs without a window, so the client can be
    /// asked things its command line doesn't take without putting any of its windows on screen.
    /// </summary>
    internal sealed partial class SteamDownloads
    {
        // The empty file in Steam's folder that has the client open its CEF debugger on 127.0.0.1 at each start.
        private const string CefDebuggingFile = ".cef-enable-remote-debugging";

        // How long each port's list of debugger targets is waited for: the debugger answers at once on the PC's own
        // loopback when it's open, so a port that hasn't answered by then is taken as closed.
        private static readonly TimeSpan CefProbeTimeout = TimeSpan.FromSeconds(2);

        /// <summary>Whether the client opens its CEF debugger as it starts, as the file in Steam's folder last left it.</summary>
        public bool IsCefDebuggingOn
        {
            get
            {
                var steam = SteamPath();
                return steam != null && File.Exists(Path.Combine(steam, CefDebuggingFile));
            }
        }

        /// <summary>
        /// Runs <paramref name="expression"/> in the client's SharedJSContext, its script that runs without a window,
        /// through its CEF debugger, and returns what it came to: its value as a string, or that it threw or returned
        /// nothing. Null when no debugger answers on 127.0.0.1's ports 8080 or 8081 within a couple of seconds each,
        /// when one does but Steam has no SharedJSContext yet, or when that context won't take the connection: each
        /// before anything is sent, so the caller can safely ask another way, and says why in its own words. Throws
        /// should the connection fail once the script is sent, when it may have run.
        /// </summary>
        private static async Task<string> EvaluateInSteam(string expression)
        {
            string targets = null;
            using (var http = new HttpClient { Timeout = CefProbeTimeout })
            {
                foreach (var port in new[] { 8080, 8081 })
                {
                    try
                    {
                        targets = await http.GetStringAsync($"http://127.0.0.1:{port}/json");
                        break;
                    }
                    catch (HttpRequestException)
                    {
                    }
                    catch (TaskCanceledException)
                    {
                    }
                }
            }

            if (targets == null)
            {
                return null;
            }

            var shared = Serialization.FromJson<List<CefTarget>>(targets)?.FirstOrDefault(t => t.Title == "SharedJSContext");
            if (shared?.WebSocketDebuggerUrl == null)
            {
                return null;
            }

            using (var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            using (var socket = new ClientWebSocket())
            {
                // A debugger that won't take the connection is as good as none, and nothing has reached Steam yet, so
                // that too is null, after no longer than the probe. Past this point the script may have run, so what
                // goes wrong is thrown for the caller to weigh.
                try
                {
                    using (var connecting = new CancellationTokenSource(CefProbeTimeout))
                    {
                        await socket.ConnectAsync(new Uri(shared.WebSocketDebuggerUrl), connecting.Token);
                    }
                }
                catch (Exception e) when (e is WebSocketException || e is OperationCanceledException)
                {
                    return null;
                }

                var request = Serialization.ToJson(new Dictionary<string, object>
                {
                    ["id"] = 1,
                    ["method"] = "Runtime.evaluate",
                    ["params"] = new Dictionary<string, object> { ["expression"] = expression, ["awaitPromise"] = true, ["returnByValue"] = true },
                });
                await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(request)), WebSocketMessageType.Text, true, cancel.Token);

                var buffer = new byte[16384];
                while (true)
                {
                    var message = new StringBuilder();
                    WebSocketReceiveResult received;
                    do
                    {
                        received = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancel.Token);
                        message.Append(Encoding.UTF8.GetString(buffer, 0, received.Count));
                    }
                    while (!received.EndOfMessage);

                    var reply = Serialization.FromJson<CefReply>(message.ToString());
                    if (reply?.Id != 1)
                    {
                        continue;
                    }

                    var thrown = reply.Result?.ExceptionDetails;
                    return thrown != null
                        ? $"script threw {thrown.Exception?.Description ?? thrown.Text}"
                        : reply.Result?.Result?.Value?.ToString() ?? "script returned nothing";
                }
            }
        }

        private sealed class CefTarget
        {
            public string Title { get; set; }

            public string WebSocketDebuggerUrl { get; set; }
        }

        private sealed class CefReply
        {
            public int? Id { get; set; }

            public CefEvaluation Result { get; set; }
        }

        private sealed class CefEvaluation
        {
            public CefObject Result { get; set; }

            public CefException ExceptionDetails { get; set; }
        }

        private sealed class CefObject
        {
            public object Value { get; set; }

            public string Description { get; set; }
        }

        private sealed class CefException
        {
            public string Text { get; set; }

            public CefObject Exception { get; set; }
        }
    }
}
