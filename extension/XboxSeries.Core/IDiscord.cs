using System.Collections.Generic;

namespace XboxSeries.Core
{
    /// <summary>
    /// Port for Discord's local RPC pipe, <c>discord-ipc-0..9</c>. It only moves raw frames: the protocol (handshake,
    /// AUTHORIZE, AUTHENTICATE, SUBSCRIBE, PING) is the core's. Frames from Discord reach the core through
    /// <see cref="ExtensionCore.OnDiscordFrame"/>, and the pipe closing through <see cref="ExtensionCore.OnDiscordClosed"/>,
    /// both on the UI thread.
    /// </summary>
    public interface IDiscordTransport
    {
        /// <summary>
        /// Connects to the first of <c>discord-ipc-0..9</c> that answers, closing any connection already open. False when
        /// none does: Discord isn't running.
        /// </summary>
        bool Open();

        /// <summary>Sends one frame: <paramref name="opcode"/>, then <paramref name="json"/> as UTF-8. Ignored while closed.</summary>
        void Send(DiscordOpcode opcode, string json);

        /// <summary>Closes the connection, if open. <see cref="ExtensionCore.OnDiscordClosed"/> is not raised for it.</summary>
        void Close();
    }

    /// <summary>A frame's opcode, its header's first little-endian int32.</summary>
    public enum DiscordOpcode
    {
        Handshake = 0,
        Frame = 1,
        Close = 2,
        Ping = 3,
        Pong = 4,
    }

    /// <summary>
    /// Port for Discord's OAuth2 token endpoint, <c>https://discord.com/api/oauth2/token</c>, used for the token exchange
    /// after AUTHORIZE. The answer, its HTTP status and body, reaches the core through
    /// <see cref="ExtensionCore.OnDiscordTokenResponse"/> on the UI thread; status 0, with the error's message as the body,
    /// when no answer came.
    /// </summary>
    public interface IDiscordOAuth
    {
        /// <summary>
        /// POSTs <paramref name="form"/> form-encoded, authenticated as the user's own Discord application with HTTP Basic
        /// <paramref name="clientId"/>:<paramref name="clientSecret"/>.
        /// </summary>
        void RequestToken(string clientId, string clientSecret, IReadOnlyDictionary<string, string> form);
    }

    /// <summary>
    /// Port for values kept encrypted for the Windows user (DPAPI, CurrentUser), never in Playnite's plain-JSON settings:
    /// the Discord Client Secret and tokens. Named values; each write is saved at once.
    /// </summary>
    public interface ISecretStore
    {
        /// <summary>The value saved under <paramref name="name"/>, or null for none.</summary>
        string Read(string name);

        void Write(string name, string value);

        /// <summary>Forgets the value under <paramref name="name"/>, if any.</summary>
        void Delete(string name);
    }
}
