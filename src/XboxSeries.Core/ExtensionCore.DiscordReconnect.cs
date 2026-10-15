using System;

namespace XboxSeries.Core
{
    /// <summary>
    /// Discord out of reach with tokens saved: not running at start-up, quit, or closing the connection. The pipe is tried
    /// again, backing off, until Discord is back and signed in with the saved tokens, the handshake, AUTHENTICATE and every
    /// SUBSCRIBE done afresh. A pipe that opens but never says READY counts as out of reach. Disconnect, Connect and the
    /// user needing to reconnect stop it.
    /// </summary>
    public sealed partial class ExtensionCore
    {
        /// <summary>
        /// How long after Discord is found out of reach the pipe is tried again: soon at first, as when Discord is only
        /// restarting, then less often, holding at a minute for as long as it stays away. Signing in starts it afresh.
        /// </summary>
        private static readonly TimeSpan[] ProbeDelays =
        {
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(4),
            TimeSpan.FromSeconds(8),
            TimeSpan.FromSeconds(16),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(60),
        };

        /// <summary>
        /// How long, signing in with the saved tokens, Discord has to say READY once the pipe opens, before it is closed
        /// and tried again later, as Discord still starting, or hung, may never say it. Connect waits for as long as it takes.
        /// </summary>
        private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(10);

        /// <summary>The next try of the pipe, while Discord is out of reach; never more than one.</summary>
        private IDisposable probeTimer;

        /// <summary>The end of the wait for READY, signing in with the saved tokens.</summary>
        private IDisposable readyTimer;

        /// <summary>The tries since Discord last signed in, which pick the next one's delay.</summary>
        private int probes;

        /// <summary>
        /// Whether Discord out of reach is tried again: with tokens saved, so not after Disconnect nor before the first
        /// Connect, and while they may still do, so not once the user has to reconnect.
        /// </summary>
        private bool ReconnectsByItself => Settings.Discord.HasTokens && !Settings.Discord.NeedsReconnect;

        /// <summary>Discord is out of reach: the pipe is tried again after the next delay, signing in with the saved tokens.</summary>
        private void ProbeLater()
        {
            ForgetProbe();
            if (!ReconnectsByItself)
            {
                return;
            }

            var delay = ProbeDelays[Math.Min(probes, ProbeDelays.Length - 1)];
            probes++;
            probeTimer = clock.After(delay, () =>
            {
                probeTimer = null;
                SignInWithSavedTokens();
            });
        }

        /// <summary>The pipe open for the saved tokens: without READY within <see cref="ReadyTimeout"/>, Discord counts as out of reach.</summary>
        private void AwaitReady()
        {
            readyTimer = clock.After(ReadyTimeout, () =>
            {
                readyTimer = null;
                CloseDiscord(DiscordNotRunning, tryAgain: true);
            });
        }

        /// <summary>Forgets the wait for READY, as READY comes or the pipe closes.</summary>
        private void ForgetReadyWait()
        {
            readyTimer?.Dispose();
            readyTimer = null;
        }

        /// <summary>Signed in: the next time Discord goes, it is tried again soon.</summary>
        private void ResetProbes()
        {
            ForgetProbe();
            probes = 0;
        }

        /// <summary>Forgets the try waiting to come, as another connect starts or trying again stops.</summary>
        private void ForgetProbe()
        {
            probeTimer?.Dispose();
            probeTimer = null;
        }
    }
}
