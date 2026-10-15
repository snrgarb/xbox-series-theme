using System.Collections.Generic;
using System.ComponentModel;

namespace XboxSeries.Core
{
    /// <summary>
    /// What Settings' Discord category shows: the Connect checklist, the user's own Discord application, and the
    /// connection's status line. Views bind to it and hold no logic of their own; the Client Secret itself is never in it.
    /// </summary>
    public sealed class DiscordSettingsState : INotifyPropertyChanged
    {
        private string clientId;
        private bool hasClientSecret;
        private bool hasTokens;
        private string statusText = "Not connected";
        private bool isConnected;
        private bool isConnecting;
        private bool needsReconnect;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>Discord's developer portal, where the user registers the application; the page links to it and shows its QR code.</summary>
        public string PortalUrl => "https://discord.com/developers/applications";

        /// <summary>The Connect checklist, in order.</summary>
        public IReadOnlyList<string> Steps { get; } = new[]
        {
            "Create an application at discord.com/developers/applications.",
            "Under OAuth2, add the redirect http://127.0.0.1.",
            "Under App Testers, add yourself.",
            "Paste its Client ID and Client Secret here, then select Connect.",
        };

        /// <summary>The application's Client ID, or null for none yet.</summary>
        public string ClientId
        {
            get => clientId;
            internal set => Set(ref clientId, value, nameof(ClientId));
        }

        /// <summary>Whether a Client Secret is saved. The Secret itself is never shown.</summary>
        public bool HasClientSecret
        {
            get => hasClientSecret;
            internal set => Set(ref hasClientSecret, value, nameof(HasClientSecret));
        }

        /// <summary>The status line: "Not connected", "Connected as X", or what went wrong.</summary>
        public string StatusText
        {
            get => statusText;
            internal set => Set(ref statusText, value, nameof(StatusText));
        }

        /// <summary>Whether Discord has signed the user in; the Guide's Discord tab shows while it has.</summary>
        public bool IsConnected
        {
            get => isConnected;
            internal set => Set(ref isConnected, value, nameof(IsConnected));
        }

        /// <summary>Whether a connect is under way: the handshake, the Authorize prompt, the token exchange or signing in.</summary>
        public bool IsConnecting
        {
            get => isConnecting;
            internal set => Set(ref isConnecting, value, nameof(IsConnecting));
        }

        /// <summary>
        /// Whether Discord stopped taking the saved tokens and wouldn't authorize again either, so the user has to Connect
        /// from Settings once more. Connect signing in, or Disconnect, clears it.
        /// </summary>
        public bool NeedsReconnect
        {
            get => needsReconnect;
            internal set => Set(ref needsReconnect, value, nameof(NeedsReconnect));
        }

        /// <summary>Whether Connect does anything: with a Client ID and Secret, and not connected or connecting already.</summary>
        public bool CanConnect => clientId != null && hasClientSecret && !isConnected && !isConnecting;

        /// <summary>Whether Disconnect does anything: while connected or connecting, or with tokens saved from before.</summary>
        public bool CanDisconnect => isConnected || isConnecting || hasTokens;

        /// <summary>Whether tokens are saved, as after a start-up that couldn't reach Discord.</summary>
        internal bool HasTokens
        {
            get => hasTokens;
            set => Set(ref hasTokens, value, null);
        }

        private void Set<T>(ref T field, T value, string name)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            if (name != null)
            {
                Changed(name);
            }

            Changed(nameof(CanConnect));
            Changed(nameof(CanDisconnect));
        }

        private void Changed(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }
}
