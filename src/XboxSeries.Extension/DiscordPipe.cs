using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: Discord's local RPC pipe, <c>\\.\pipe\discord-ipc-0..9</c>; Stable, PTB and Canary each take the next
    /// free index. It only moves frames, an int32 LE opcode, an int32 LE length and that many bytes of UTF-8 JSON: the
    /// protocol, PING included, is the core's.
    /// </summary>
    internal sealed class DiscordPipe : IDiscordTransport
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>A frame longer than this means the stream is out of step, not that Discord sent a huge payload.</summary>
        private const int MaxPayload = 16 * 1024 * 1024;

        private readonly object gate = new object();
        private Connection current;

        /// <summary>Raised, on the pipe's reader thread, for each whole frame from Discord.</summary>
        public event Action<DiscordOpcode, string> FrameReceived;

        /// <summary>
        /// Raised once, on any thread, when Discord closes the pipe or it breaks: a read ends or a send fails. Not raised
        /// for <see cref="Close"/>, nor for the connection a later <see cref="Open"/> replaces.
        /// </summary>
        public event Action Closed;

        public bool Open()
        {
            Close();

            for (var index = 0; index < 10; index++)
            {
                var stream = new NamedPipeClientStream(".", "discord-ipc-" + index, PipeDirection.InOut, PipeOptions.Asynchronous);
                try
                {
                    // One try per pipe, called on the UI thread: a missing pipe fails at once, and a busy one waits only
                    // for its server's default, a few tens of milliseconds.
                    stream.Connect(0);
                }
                catch (Exception e) when (e is TimeoutException || e is IOException || e is UnauthorizedAccessException)
                {
                    stream.Dispose();
                    continue;
                }

                var connection = new Connection(stream);
                lock (gate)
                {
                    current = connection;
                }

                new Thread(() => Read(connection)) { IsBackground = true, Name = "Discord RPC pipe" }.Start();
                logger.Info($"Connected to Discord's RPC pipe discord-ipc-{index}");
                return true;
            }

            return false;
        }

        public void Send(DiscordOpcode opcode, string json)
        {
            Connection broken;
            lock (gate)
            {
                if (current == null)
                {
                    return;
                }

                try
                {
                    LogFrame("Sent", opcode, json);
                    var payload = Encoding.UTF8.GetBytes(json ?? "");
                    var frame = new byte[8 + payload.Length];
                    BitConverter.GetBytes((int)opcode).CopyTo(frame, 0);
                    BitConverter.GetBytes(payload.Length).CopyTo(frame, 4);
                    payload.CopyTo(frame, 8);
                    current.Stream.Write(frame, 0, frame.Length);
                    current.Stream.Flush();
                    return;
                }
                catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is InvalidOperationException)
                {
                    // The pipe broke between Discord's last frame and this one; the reader may not have noticed yet.
                    logger.Warn(e, $"Couldn't send a {opcode} frame to Discord; the pipe is closed");
                    broken = current;
                }
            }

            End(broken);
        }

        public void Close()
        {
            Connection closing;
            lock (gate)
            {
                closing = current;
                current = null;
            }

            // Whoever ends a connection first decides: ended here, the reader's failure that follows raises nothing.
            closing?.End();
        }

        /// <summary>
        /// Debug builds log every frame, so what Discord really sends can be read in Playnite's extensions.log; tokens and
        /// authorization codes are masked.
        /// </summary>
        [System.Diagnostics.Conditional("DEBUG")]
        private static void LogFrame(string direction, DiscordOpcode opcode, string json) =>
            logger.Info($"Discord RPC {direction} {opcode}: "
                + System.Text.RegularExpressions.Regex.Replace(json ?? "", @"""(access_token|refresh_token|code)""\s*:\s*""[^""]*""", @"""$1"":""***"""));

        /// <summary>Reads frames until the pipe closes. Frames can arrive split across reads or several to a read.</summary>
        private void Read(Connection connection)
        {
            var header = new byte[8];
            try
            {
                while (true)
                {
                    if (!ReadFully(connection.Stream, header, 8))
                    {
                        break;
                    }

                    var opcode = (DiscordOpcode)BitConverter.ToInt32(header, 0);
                    var length = BitConverter.ToInt32(header, 4);
                    if (length < 0 || length > MaxPayload)
                    {
                        logger.Warn($"Discord sent a {opcode} frame {length} bytes long; dropping the pipe");
                        break;
                    }

                    var payload = new byte[length];
                    if (!ReadFully(connection.Stream, payload, length))
                    {
                        break;
                    }

                    var received = Encoding.UTF8.GetString(payload);
                    LogFrame("Received", opcode, received);
                    FrameReceived?.Invoke(opcode, received);
                }
            }
            catch (Exception e) when (e is IOException || e is ObjectDisposedException || e is InvalidOperationException)
            {
                // Discord quit, or Close disposed the stream under this read.
            }
            catch (Exception e)
            {
                logger.Error(e, "Discord's RPC pipe reader failed");
            }

            End(connection);
        }

        /// <summary>False when the pipe ends before <paramref name="count"/> bytes have come.</summary>
        private static bool ReadFully(Stream stream, byte[] buffer, int count)
        {
            var read = 0;
            while (read < count)
            {
                var got = stream.Read(buffer, read, count - read);
                if (got == 0)
                {
                    return false;
                }

                read += got;
            }

            return true;
        }

        /// <summary>Ends a connection that closed or broke under us, telling the core unless it was already ended.</summary>
        private void End(Connection connection)
        {
            if (!connection.End())
            {
                return;
            }

            lock (gate)
            {
                if (current == connection)
                {
                    current = null;
                }
            }

            logger.Info("Discord's RPC pipe closed");
            Closed?.Invoke();
        }

        /// <summary>One open pipe. A replaced connection's reader can still fail later; it ends only its own.</summary>
        private sealed class Connection
        {
            private int ended;

            public Connection(NamedPipeClientStream stream)
            {
                Stream = stream;
            }

            public NamedPipeClientStream Stream { get; }

            /// <summary>Disposes the pipe. True only for the first call.</summary>
            public bool End()
            {
                if (Interlocked.Exchange(ref ended, 1) != 0)
                {
                    return false;
                }

                Stream.Dispose();
                return true;
            }
        }
    }
}
