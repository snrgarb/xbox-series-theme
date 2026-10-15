using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>
    /// Discord's RPC pipe, recording the raw frames the core sends. A test plays Discord by pushing frames back through
    /// <see cref="ExtensionCore.OnDiscordFrame"/>.
    /// </summary>
    public sealed class FakeDiscordTransport : IDiscordTransport
    {
        /// <summary>Whether Discord is running, so that <see cref="Open"/> finds a pipe.</summary>
        public bool IsRunning { get; set; } = true;

        public bool IsOpen { get; private set; }

        public int OpenCount { get; private set; }

        /// <summary>Every frame sent, in order, while open.</summary>
        public List<(DiscordOpcode Opcode, string Json)> Sent { get; } = new List<(DiscordOpcode, string)>();

        public bool Open()
        {
            OpenCount++;
            IsOpen = IsRunning;
            return IsOpen;
        }

        public void Send(DiscordOpcode opcode, string json)
        {
            if (IsOpen)
            {
                Sent.Add((opcode, json));
            }
        }

        public void Close() => IsOpen = false;

        /// <summary>The Frame payloads sent, parsed.</summary>
        public IReadOnlyList<JObject> Commands => Sent.Where(f => f.Opcode == DiscordOpcode.Frame).Select(f => JObject.Parse(f.Json)).ToList();

        /// <summary>The last command sent with <paramref name="cmd"/>, or null for none.</summary>
        public JObject LastCommand(string cmd) => Commands.LastOrDefault(c => (string)c["cmd"] == cmd);
    }
}
