using Microsoft.Identity.Web;
using System.Net.Http.Headers;

namespace MetaMeetDemo.Handlers
{
    public class UserAccessTokenHandler : DelegatingHandler
    {
        private readonly ITokenAcquisition _tokenAcquisition;
        private readonly string[] _scopes;
        private readonly ILogger<UserAccessTokenHandler>? _logger;

        public UserAccessTokenHandler(
            ITokenAcquisition tokenAcquisition,
            string[] scopes,
            ILogger<UserAccessTokenHandler>? logger = null)
            : base(new HttpClientHandler())
        {
            _tokenAcquisition = tokenAcquisition;
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger?.LogInformation("Próba pobrania tokena dla scopes: {Scopes}",
                    string.Join(", ", _scopes));

                var token = await _tokenAcquisition.GetAccessTokenForUserAsync(_scopes);

                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    _logger?.LogInformation("✅ Token dodany do żądania: {Method} {Uri} (długość: {Length})",
                        request.Method, request.RequestUri, token.Length);
                }
                else
                {
                    _logger?.LogError("❌ Token jest PUSTY dla scopes: {Scopes}",
                        string.Join(", ", _scopes));
                }
            }
            catch (Microsoft.Identity.Web.MicrosoftIdentityWebChallengeUserException ex)
            {
                _logger?.LogError("❌ Wymagana zgoda użytkownika: {Message}", ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "❌ Błąd pobierania tokena dla scopes: {Scopes}",
                    string.Join(", ", _scopes));
                throw;
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}