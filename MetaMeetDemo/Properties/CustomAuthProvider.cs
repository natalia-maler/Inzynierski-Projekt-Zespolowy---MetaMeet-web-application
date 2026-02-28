using Microsoft.Graph;
using Microsoft.Identity.Web;
using Microsoft.Kiota.Abstractions;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Kiota.Abstractions.Authentication;

public class CustomAuthProvider : IAuthenticationProvider
{
    private readonly ITokenAcquisition _tokenAcquisition;

    public CustomAuthProvider(ITokenAcquisition tokenAcquisition)
    {
        _tokenAcquisition = tokenAcquisition;
    }

    public async Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object>? additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
    {
        var token = await _tokenAcquisition.GetAccessTokenForUserAsync(new[] { "User.Read.All" });
        request.Headers.Add("Authorization", $"Bearer {token}");
    }
}
