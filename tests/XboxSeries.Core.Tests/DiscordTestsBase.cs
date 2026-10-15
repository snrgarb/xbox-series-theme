using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using XboxSeries.Core.Tests.Fakes;

namespace XboxSeries.Core.Tests
{
    /// <summary>
    /// A core signed in to a played Discord: the pad, raw RPC frames back through the core, and the channel and voice
    /// state objects Discord sends, for the shortcut and server tests.
    /// </summary>
    public abstract class DiscordTestsBase
    {
        protected const string ClientId = "1290000000000000001";
        protected const string Secret = "s3cr3t-s3cr3t-s3cr3t-s3cr3t-0001";
        protected const string Me = "100000000000000001";
        protected const string Ana = "200000000000000002";
        protected const string Bo = "300000000000000003";

        protected readonly FakeClock clock = new FakeClock(new DateTime(2026, 10, 15, 20, 0, 0));
        protected readonly FakeSoundPlayer sounds = new FakeSoundPlayer();
        protected readonly FakeSecretStore secrets = new FakeSecretStore();
        protected FakeDiscordTransport transport;
        protected ExtensionCore core;

        protected DiscordTestsBase()
        {
            NewCore();
        }

        /// <summary>A core made afresh over the same secret store, as when Playnite starts again.</summary>
        protected void NewCore()
        {
            var windows = new FakeWindowSystem();
            transport = new FakeDiscordTransport();
            core = new ExtensionCore(clock, windows, new FakePlayniteHost(windows), sounds, new FakeDrives(), new FakeUserAccount(),
                new FakeAudio(), new FakeController(), transport, new FakeDiscordOAuth(), secrets);
        }

        protected GuideState Guide => core.Guide;

        protected void ConnectFully()
        {
            core.SetDiscordClientId(ClientId);
            core.SetDiscordClientSecret(Secret);
            core.ConnectDiscord();
            core.OnDiscordFrame(DiscordOpcode.Frame, "{\"cmd\":\"DISPATCH\",\"evt\":\"READY\",\"data\":{\"v\":1,\"config\":{}},\"nonce\":null}");
            Reply("AUTHORIZE", new JObject { ["code"] = "the-code" });
            core.OnDiscordTokenResponse(200, new JObject { ["access_token"] = "access-1", ["refresh_token"] = "refresh-1", ["expires_in"] = 604800 }.ToString());
            Reply("AUTHENTICATE", new JObject
            {
                ["user"] = new JObject { ["id"] = Me, ["username"] = "gabe", ["global_name"] = null },
                ["scopes"] = new JArray("rpc", "identify"),
            });
        }

        protected void NotInCall()
        {
            ConnectFully();
            Reply("GET_SELECTED_VOICE_CHANNEL", null);
            Reply("GET_VOICE_SETTINGS", new JObject { ["mute"] = false, ["deaf"] = false });
        }

        /// <summary>The user joins <paramref name="channel"/>, on this PC or another device: Discord says so, and the channel is fetched.</summary>
        protected void Join(JObject channel)
        {
            Dispatch("VOICE_CHANNEL_SELECT", new JObject { ["channel_id"] = channel["id"], ["guild_id"] = channel["guild_id"] });
            Reply("GET_CHANNEL", channel);
        }

        protected void Leave() => Dispatch("VOICE_CHANNEL_SELECT", new JObject { ["channel_id"] = null, ["guild_id"] = null });

        protected void OpenGuide()
        {
            Tap(ControllerButton.Guide);
            clock.Advance(TimeSpan.FromMilliseconds(300));
        }

        protected void OpenDiscordTab()
        {
            OpenGuide();
            Tap(ControllerButton.RightShoulder);
            if (Guide.Tab != GuideTabKind.Discord)
            {
                throw new InvalidOperationException("The Discord tab didn't open.");
            }
        }

        protected void Tap(ControllerButton button)
        {
            core.OnControllerButton(button, ButtonState.Pressed);
            core.OnControllerButton(button, ButtonState.Released);
        }

        protected IReadOnlyList<GuideItemKind> Kinds() => Guide.Items.Select(i => i.Kind).ToList();

        protected IReadOnlyList<GuideItem> Shortcuts() => Guide.Items.Where(i => i.Kind == GuideItemKind.DiscordShortcut).ToList();

        protected IReadOnlyList<string> ShortcutTitles() => Shortcuts().Select(i => i.Title).ToList();

        protected IReadOnlyList<JObject> Sent(string cmd) => transport.Commands.Where(c => (string)c["cmd"] == cmd).ToList();

        protected void Dispatch(string evt, JObject data) => core.OnDiscordFrame(DiscordOpcode.Frame, new JObject
        {
            ["cmd"] = "DISPATCH",
            ["evt"] = evt,
            ["data"] = data,
            ["nonce"] = null,
        }.ToString());

        /// <summary>Discord's reply to the last <paramref name="cmd"/> sent, matched to it by nonce.</summary>
        protected void Reply(string cmd, JObject data) => ReplyTo(transport.LastCommand(cmd), data);

        /// <summary>Discord's reply to <paramref name="command"/>, one of the commands sent.</summary>
        protected void ReplyTo(JObject command, JObject data) => core.OnDiscordFrame(DiscordOpcode.Frame, new JObject
        {
            ["cmd"] = command["cmd"],
            ["evt"] = null,
            ["data"] = data,
            ["nonce"] = command["nonce"],
        }.ToString());

        /// <summary>Discord's error reply to the last <paramref name="cmd"/> sent.</summary>
        protected void Error(string cmd, string message) => core.OnDiscordFrame(DiscordOpcode.Frame, new JObject
        {
            ["cmd"] = cmd,
            ["evt"] = "ERROR",
            ["data"] = new JObject { ["code"] = 5000, ["message"] = message },
            ["nonce"] = transport.LastCommand(cmd)["nonce"],
        }.ToString());

        /// <summary>A guild voice channel on server <paramref name="guildId"/>, as GET_CHANNEL answers.</summary>
        protected static JObject VoiceChannel(string id, string name, string guildId, params JObject[] states) => new JObject
        {
            ["id"] = id,
            ["name"] = name,
            ["type"] = 2,
            ["guild_id"] = guildId,
            ["voice_states"] = new JArray(states),
        };

        /// <summary>A DM (type 1) or group DM (type 3), which has no name of its own.</summary>
        protected static JObject Dm(string id, int type, params JObject[] states) => new JObject
        {
            ["id"] = id,
            ["name"] = "",
            ["type"] = type,
            ["guild_id"] = null,
            ["voice_states"] = new JArray(states),
        };

        protected static JObject State(string id, string username, string avatar = null) => new JObject
        {
            ["nick"] = null,
            ["mute"] = false,
            ["user"] = new JObject { ["id"] = id, ["username"] = username, ["global_name"] = null, ["avatar"] = avatar, ["bot"] = false },
            ["voice_state"] = new JObject { ["mute"] = false, ["deaf"] = false, ["self_mute"] = false, ["self_deaf"] = false },
        };

        protected static string AvatarOf(string id, string avatar) => "https://cdn.discordapp.com/avatars/" + id + "/" + avatar + ".png?size=64";
    }
}
