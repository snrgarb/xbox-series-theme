using System.Collections.Generic;
using System.Linq;
using XboxSeries.Core;

namespace XboxSeries.Core.Tests.Fakes
{
    /// <summary>
    /// Discord's token endpoint, recording each request. A test answers through <see cref="ExtensionCore.OnDiscordTokenResponse"/>.
    /// </summary>
    public sealed class FakeDiscordOAuth : IDiscordOAuth
    {
        public List<(string ClientId, string ClientSecret, IReadOnlyDictionary<string, string> Form)> Requests { get; } =
            new List<(string, string, IReadOnlyDictionary<string, string>)>();

        public void RequestToken(string clientId, string clientSecret, IReadOnlyDictionary<string, string> form) =>
            Requests.Add((clientId, clientSecret, form.ToDictionary(p => p.Key, p => p.Value)));
    }
}
