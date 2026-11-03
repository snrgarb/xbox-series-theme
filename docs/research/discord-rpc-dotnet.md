# Discord RPC from a .NET 4.6.2 extension on Windows

Research for [#16](https://github.com/snrgarb/xbox-series-theme/issues/16), part of #1. Builds on the macOS spike in [snrgarb/discord-rpc-research](https://github.com/snrgarb/discord-rpc-research) ([PROTOCOL.md](https://github.com/snrgarb/discord-rpc-research/blob/main/docs/PROTOCOL.md), [FINDINGS.md](https://github.com/snrgarb/discord-rpc-research/blob/main/docs/FINDINGS.md)).

Sources were read on 13 October 2026. Each claim is tagged with its source; **[inference]** marks reasoning from those sources that nobody has tested yet.

## Answer

**Yes.** Every piece the spike used can be built on .NET Framework 4.6.2 with no third-party dependency. The protocol is the same on every OS: only the transport path differs. The plan:

- **Write our own small client.** Connect to the named pipe with `System.IO.Pipes.NamedPipeClientStream`, use the same 8-byte frame header, and send the token requests with `HttpClient`. That is roughly the size of the spike's `lib/discord-rpc.mjs`.
- **Don't use Lachee's `discord-rpc-csharp`.** It only does Rich Presence, and it marks `AUTHORIZE`, `AUTHENTICATE` and every voice command as unsupported.
- **Store the secret and tokens with DPAPI.** Encrypt the Client Secret, access token and refresh token with `ProtectedData` (`DataProtectionScope.CurrentUser`) before they reach Playnite's plain-JSON settings.
- **Refresh tokens early.** Use the `refresh_token` grant ahead of `expires_in` (604800 s), and fall back to `AUTHORIZE` only when the refresh fails.
- **Reconnect in a loop.** A background loop probes `discord-ipc-0…9` with backoff. After each reconnect it handshakes, authenticates and subscribes again.

**Still open:** a run against Windows Discord. No document says voice commands or events differ by OS, but the spike only ran on macOS. That needs a short Windows smoke test; see [what still needs testing](#what-still-needs-testing-on-windows).

## 1. Transport: named pipes from .NET 4.6.2

**Pipe path.** Discord documents the Windows IPC path as `\\?\pipe\discord-ipc-{n}`. [Discord RPC docs, "IPC Path"](https://github.com/discord/discord-api-docs/blob/main/developers/topics/rpc.mdx) The spike tried `n` = 0–9, because Stable, PTB and Canary each take the next index. [spike PROTOCOL.md]

**Connecting.** `NamedPipeClientStream` is available in .NET Framework 4.6.2 (System.Core.dll). It has `Connect(int timeout)`, and `ConnectAsync(int timeout, CancellationToken)` exists from 4.6 onward. [MS: NamedPipeClientStream (netframework-4.6.2)](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeclientstream?view=netframework-4.6.2), [MS: ConnectAsync](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeclientstream.connectasync?view=netframework-4.6.2)

Pass the server `"."` and the bare pipe name, `"discord-ipc-0"`. Windows forms the full path as `\\.\pipe\<name>`, where `.` means the local machine. [MS: Pipe Names](https://learn.microsoft.com/en-us/windows/win32/ipc/pipe-names) Lachee's library does exactly this: `new NamedPipeClientStream(".", pipename, PipeDirection.InOut, PipeOptions.Asynchronous)` with names `discord-ipc-{index}`. [Lachee `ManagedNamedPipeClient.cs`, `PipeLocation.cs`](https://github.com/Lachee/discord-rpc-csharp/tree/master/DiscordRPC/IO)

Use `PipeDirection.InOut` and `PipeOptions.Asynchronous`, so a background reader can block on the pipe without tying up a thread-pool thread for each read. [inference, matching Lachee]

**Framing.** The protocol is identical to the spike. Each frame is `int32 LE opcode`, then `int32 LE length`, then a UTF-8 JSON payload. The opcodes are 0 HANDSHAKE, 1 FRAME, 2 CLOSE, 3 PING and 4 PONG. The handshake payload is `{"v":1,"client_id":"…"}`, and Discord replies with a FRAME carrying `READY`. [Discord RPC docs, "Connecting to IPC"](https://github.com/discord/discord-api-docs/blob/main/developers/topics/rpc.mdx)

In .NET 4.6.2:

- Write the header with `BitConverter.GetBytes(int)`, which is little-endian on x86/x64 Windows.
- Read in a loop until `8 + length` bytes are buffered, because frames can arrive split or merged. [spike PROTOCOL.md]
- Answer PING with PONG and the same payload. [spike PROTOCOL.md; Lachee `RpcConnection.cs` does the same]
- Use Newtonsoft.Json, or Playnite's own `Playnite.SDK.Data.Serialization`, for the JSON.

**Alternate transport.** The WebSocket transport on ports 6463–6472 is deprecated and only available to "old participants of private beta". Use IPC only. [Discord RPC docs, "RPC over WebSocket (Deprecated)"](https://github.com/discord/discord-api-docs/blob/main/developers/topics/rpc.mdx)

## 2. Maintained .NET libraries

| Library | Fits 4.6.2? | Does what we need? |
| --- | --- | --- |
| [Lachee/discord-rpc-csharp](https://github.com/Lachee/discord-rpc-csharp) v1.6.2 (Aug 2025; last push Dec 2025; not archived) | Yes, in principle: targets `net45`, `netstandard2.0` and `net9.0`, and depends on Newtonsoft.Json 13.0.1 [`DiscordRPC.csproj`] | **No.** Rich Presence only. In its `Command` enum, `Authorize`, `Authenticate`, `GetGuild(s)`, `GetChannel(s)`, `SelectVoiceChannel`, `GetSelectedVoiceChannel`, `Get/SetVoiceSettings` and `SetUserVoiceSettings` are all `[Obsolete("…not supported by this library.", true)]`, so they don't compile. [`RPC/Payload/Command.cs`] Its `ServerEvent` enum also spells two events wrong: `VOICE_STATE_CREATED`/`UPDATED` instead of `CREATE`/`UPDATE`. [`ServerEvent.cs`] |

A web search turned up no maintained C# library that wraps authenticated RPC. Swift (aeddi/DiscordRPC) and Python (pypresence) clients exist but don't help us.

**Dependency clash if we used Lachee anyway.** Playnite references Newtonsoft.Json **10.0.3** [Playnite `source/Playnite/Playnite.csproj`](https://github.com/JosefNemec/Playnite/blob/master/source/Playnite/Playnite.csproj), while Lachee needs 13.0.1. The extension loads into Playnite's process, so the two versions could conflict. [inference]

A hand-rolled client avoids this. It should use the Newtonsoft version Playnite already loads, or `Playnite.SDK.Data.Serialization`. Playnite and its SDK both target `net462`. [Playnite `Playnite.SDK.csproj`](https://github.com/JosefNemec/Playnite/blob/master/source/PlayniteSDK/Playnite.SDK.csproj)

**Reusable patterns.** Lachee's `ManagedNamedPipeClient` and `RpcConnection` are still worth reading for these:

- pipe probing
- `BeginRead`/`EndRead` handling of `IOException` and `ObjectDisposedException` when Discord closes the pipe
- `BackoffDelay(500 ms → 60 s)` reconnect

## 3. OAuth with a user-supplied Client ID and Secret

**Who may connect.** "We currently do not allow access to RPC for unapproved apps without being on the game's list of testers". Each app gets 50 tester slots. [Discord RPC docs, "AUTHORIZE"](https://github.com/discord/discord-api-docs/blob/main/developers/topics/rpc.mdx)

The scopes `rpc`, `rpc.voice.read`, `rpc.voice.write` and `rpc.notifications.read` are "only available to approved partners". [Discord OAuth2 docs, "OAuth2 Scopes"](https://github.com/discord/discord-api-docs/blob/main/developers/topics/oauth2.mdx)

In the spike, the **app owner** was granted all five scopes without approval. [spike FINDINGS.md] That is why the Bundle has each user create their own Discord application: the user is its owner. Anyone else who wants to use that user's app must be added as a tester. [inference from the two above]

**Flow.** The flow is the same as the spike's, since none of it is OS-specific:

1. Send `AUTHORIZE` with `{client_id, scopes, redirect_uri}`. Discord shows its in-app consent prompt and returns a `code`.
2. POST to `https://discord.com/api/oauth2/token` with `grant_type=authorization_code`, `code` and `redirect_uri`. Authenticate with client ID and secret, either as HTTP Basic or as body fields, form-encoded.
3. Send `AUTHENTICATE` with `{access_token}`.

[Discord RPC docs, "Authenticating"; Discord OAuth2 docs, "Authorization Code Grant"](https://github.com/discord/discord-api-docs/blob/main/developers/topics/oauth2.mdx)

**`redirect_uri`.** The Discord docs don't list it among the `AUTHORIZE` arguments, but the spike got `5000: … Missing "redirect_uri"` without it. It must match a redirect registered on the app and the one sent in the token exchange. [spike PROTOCOL.md; Discord RPC docs, "RPC Server Ports" note on matching redirect URIs]

Nothing is ever redirected to that URI, because the code comes back over the pipe. So the setup instructions should tell users to register a dummy such as `http://127.0.0.1`. The extension does not need to run a local HTTP listener. [inference from spike]

**`rpc_token`.** The documented `rpc_token` bypass of the consent prompt is limited to approved apps and testers. It isn't needed, because the user approves once. [Discord RPC docs, "AUTHORIZE"]

**TLS from .NET 4.6.2.** For apps that target 4.6.2, `Switch.System.Net.DontEnableSchUseStrongCrypto` defaults to `false`, so `HttpClient` uses TLS 1.2+ by default. Microsoft advises not setting `ServicePointManager.SecurityProtocol` yourself. [MS: TLS best practices with .NET Framework](https://learn.microsoft.com/dotnet/framework/network-programming/tls)

These defaults follow the **host** process's target framework, and Playnite targets net462, so the defaults should hold. Do not override `SecurityProtocol` from the extension, because the setting is process-wide. [inference]

**Refresh before the one-week expiry.** The token response includes `expires_in: 604800` (7 days) and a `refresh_token`. Discord says `expires_in` is there so apps can "anticipate the expiration and refresh the token". To refresh, POST `grant_type=refresh_token` and `refresh_token` to the same token URL with client credentials. [Discord OAuth2 docs, "Refresh Token Exchange Example"](https://github.com/discord/discord-api-docs/blob/main/developers/topics/oauth2.mdx)

Recommended policy [inference]:

- Store the absolute expiry time.
- On connect, and on a daily timer, refresh when less than about 24 h remains.
- If `AUTHENTICATE` rejects the token, try one refresh. If that also fails, run `AUTHORIZE` again, which shows the prompt again.
- Save the new refresh token every time; the spike did not test whether Discord rotates it.

The spike only tried re-authorizing, never refreshing. [spike PROTOCOL.md]

**Where secrets live on Windows.** Playnite "serializes settings object to a JSON object and saves it as text file", with no encryption. [Playnite docs: Plugin settings](https://api.playnite.link/docs/tutorials/extensions/pluginSettings.html)

Use `System.Security.Cryptography.ProtectedData.Protect/Unprotect` with `DataProtectionScope.CurrentUser`. That is DPAPI: only the same Windows user can decrypt, usually only on the same machine, and no key has to be managed. [MS: ProtectedData](https://learn.microsoft.com/dotnet/api/system.security.cryptography.protecteddata), [MS: DataProtectionScope](https://learn.microsoft.com/dotnet/api/system.security.cryptography.dataprotectionscope), [MS: How to use data protection](https://learn.microsoft.com/dotnet/standard/security/how-to-use-data-protection) It ships in `System.Security.dll` in .NET Framework, so no NuGet package is needed. [MS: DpapiDataProtector (netframework)](https://learn.microsoft.com/dotnet/api/system.security.cryptography.dpapidataprotector?view=netframework-4.8.1)

Microsoft's caveat: CurrentUser scope protects the data from *other users*, not from other programs running as the same user. [MS: DpapiDataProtector.Scope]

Split the stored values like this:

- **Plain settings:** the Client ID, which is public.
- **DPAPI-encrypted (base64 in settings, or a separate file under the extension's data folder):** the Client Secret, access token, refresh token and expiry.

## 4. Reconnecting when Discord starts or quits

The Discord docs say nothing about reconnecting. What a client must handle:

- **Discord not running.** No `discord-ipc-N` pipe exists, and `Connect(timeout)` throws `TimeoutException`. Probe 0–9 with a short timeout. [MS: NamedPipeClientStream.Connect; Lachee `AttemptConnection` uses timeout 0 and catches]
- **Discord quits.** Discord may send a `CLOSE` frame. Otherwise the pending read returns 0 bytes or throws `IOException`/`ObjectDisposedException`. [Lachee `EndReadStream`, `RpcConnection` `Opcode.Close` handling] Fail every pending request; the spike rejects pending nonces when the socket closes. [spike `lib/discord-rpc.mjs`]
- **Back off between attempts.** Lachee uses 500 ms, growing to a 60 s cap. [Lachee `RpcConnection.cs`] For the Guide, a fixed 2–5 s poll while the Discord tab is enabled is cheap, because a failed local pipe open is just a failed file open. [inference]
- **State to rebuild after each reconnect** [inference from the protocol: subscriptions belong to the connection]:
  1. HANDSHAKE, then wait for READY.
  2. `AUTHENTICATE` with the stored token, refreshing or re-authorizing if needed.
  3. Re-`SUBSCRIBE` to `VOICE_CHANNEL_SELECT`, `VOICE_CONNECTION_STATUS` and `VOICE_SETTINGS_UPDATE`.
  4. `GET_SELECTED_VOICE_CHANNEL`, then re-subscribe the per-channel `VOICE_STATE_*` and `SPEAKING_*` events.
  5. Re-subscribe `VOICE_STATE_CREATE` on the favourite DM channels used for ring detection.
- **Whoever changes voice settings holds a lock.** If an app changes voice settings, other connected apps can't change them. The settings revert when that app disconnects, and are re-applied and re-locked when it reconnects. [Discord RPC docs, "SET_VOICE_SETTINGS" note] So mute/deafen through `SET_VOICE_SETTINGS` reverts whenever the extension disconnects, including when Playnite exits. Confirm on Windows whether this applies to `mute`/`deaf` or only to device and mode settings.

## 5. Do voice commands and events behave the same on Windows?

**From the documentation: yes.** Discord's RPC page has one command and event list for every platform. The only platform-specific parts are the IPC path table and one mention of a "Windows UUID" for certified hardware devices (`SET_CERTIFIED_DEVICES`, which we don't use). [Discord RPC docs] No voice command or event is documented as platform-specific.

**From testing: not yet.** The spike ran only on macOS Discord Stable. [spike FINDINGS.md] Desktop Discord is one Electron app across platforms, so differences are unlikely, but nobody has checked yet. [inference]

## What still needs testing on Windows

A smoke test of about 30 minutes, reusing the spike's flows, settles the rest:

| Item | Spike result (macOS) | Check on Windows |
| --- | --- | --- |
| Pipe found at `discord-ipc-0` (and `-1` with PTB/Canary open) | n/a | connect + READY |
| `AUTHORIZE` with `redirect_uri` → code → token → `AUTHENTICATE` | ✅ | same |
| `refresh_token` grant; does the refresh token rotate? | not tried | first run |
| `GET_GUILDS`, `GET_CHANNELS`, `GET_CHANNEL` | ✅ | same |
| `SELECT_VOICE_CHANNEL` (server + DM, no prompt) | ✅ | same |
| `VOICE_CHANNEL_SELECT`, `VOICE_CONNECTION_STATUS`, `VOICE_SETTINGS_UPDATE` | ✅ | same |
| `VOICE_STATE_CREATE/UPDATE/DELETE`, `SPEAKING_START/STOP` | ✅ | same, incl. DM ring detection |
| Mute/deafen via `SET_VOICE_SETTINGS`, and whether it reverts on disconnect | ✅ set; revert not tried | quit Playnite mid-call |
| `NOTIFICATION_CREATE`, `MESSAGE_CREATE` on DMs | ❌ | expect the same ❌ |
| Discord quit → pipe read ends → Discord restart → reconnect + re-subscribe | not tried | quit/relaunch Discord |

The harness could be the spike's Node scripts, which already pick `\\?\pipe\discord-ipc-N` on win32 [spike `lib/discord-rpc.mjs`], or a C# console prototype of the planned client. Using the C# prototype would also test the .NET pipe and framing code.

## Risks

- **Discord could change or restrict RPC.** It still sits behind tester/approval gates, and Discord now steers new projects to the Social SDK. ("We recommend using the Discord Social SDK for new projects".) [Discord RPC docs]
- **Setup is a lot to ask.** Each user must create a Discord application, register a redirect URI, and paste in the Client ID and Secret.
- **The secret sits on disk.** DPAPI protects it from other Windows users, not from malware running as the same user.
- **Playnite's process sets some constraints.** The pipe reader and the HTTP calls must stay off the UI thread, and the extension must not override process-wide `ServicePointManager` settings. [inference]
