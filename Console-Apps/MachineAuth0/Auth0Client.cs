using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MachineAuth0;

public class Auth0Client(IOptions<Auth0Options> auth0Options, HttpClient httpClient, ILogger<Auth0Client> logger)
    : IAuth0Client
{
    private readonly Auth0Options _auth0Options = auth0Options.Value;

    public async Task<string> GetClientToken()
    {
        var auth0ApiTokenRequest = new Auth0ApiTokenRequest
        {
            ClientId = _auth0Options.ClientId,
            ClientSecret = _auth0Options.ClientSecret,
            Audience = _auth0Options.Audience,
            GrantType = "client_credentials"
        };

        var json = JsonSerializer.Serialize(auth0ApiTokenRequest);
        var httpContent = new StringContent(json, MediaTypeHeaderValue.Parse("application/json"));
        var auth0TokenResponse =
            await httpClient.PostAsync($"https://{_auth0Options.Domain}/oauth/token", httpContent);
        auth0TokenResponse.EnsureSuccessStatusCode();
        await using var contentStream = await auth0TokenResponse.Content.ReadAsStreamAsync();
        var auth0ApiToken = await JsonSerializer.DeserializeAsync<Auth0ApiToken>(contentStream);
        return auth0ApiToken!.AccessToken;
    }
}