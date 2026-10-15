using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Playnite.SDK;
using XboxSeries.Core;

namespace XboxSeries.Extension
{
    /// <summary>
    /// Adapter: Discord's OAuth2 token endpoint. TLS is left to .NET's defaults, which give TLS 1.2 and later to Playnite's
    /// net462 process; <c>ServicePointManager.SecurityProtocol</c> is process-wide, so it isn't touched. Neither the
    /// secret nor the tokens are ever logged.
    /// </summary>
    internal sealed class DiscordOAuth : IDiscordOAuth
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private static readonly Uri TokenEndpoint = new Uri("https://discord.com/api/oauth2/token");

        private readonly HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        /// <summary>
        /// Raised, on a thread-pool thread, with the token endpoint's HTTP status and body; status 0, with the error's
        /// message as the body, when no answer came.
        /// </summary>
        public event Action<int, string> TokenResponded;

        public void RequestToken(string clientId, string clientSecret, IReadOnlyDictionary<string, string> form)
        {
            _ = PostAsync(clientId, clientSecret, form);
        }

        private async Task PostAsync(string clientId, string clientSecret, IReadOnlyDictionary<string, string> form)
        {
            int status;
            string body;
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, TokenEndpoint)
                {
                    Content = new FormUrlEncodedContent(form),
                };
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + clientSecret));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

                using (request)
                using (var response = await client.SendAsync(request).ConfigureAwait(false))
                {
                    status = (int)response.StatusCode;
                    body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }

                if (status != 200)
                {
                    // The body is Discord's error, e.g. invalid_grant; a 200's body holds the tokens.
                    logger.Warn($"Discord's token endpoint answered {status}: {body}");
                }
            }
            catch (Exception e)
            {
                status = 0;
                body = e.GetBaseException().Message;
                logger.Warn($"Discord's token endpoint didn't answer: {body}");
            }

            TokenResponded?.Invoke(status, body);
        }
    }
}
