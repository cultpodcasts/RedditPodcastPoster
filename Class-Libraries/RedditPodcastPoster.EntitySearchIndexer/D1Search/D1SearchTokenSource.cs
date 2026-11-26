using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.EntitySearchIndexer.D1Search;

public interface ID1SearchTokenSource
{
    /// <summary>An Auth0 client_credentials token for the writer, reused until five minutes before it expires.</summary>
    Task<string> GetToken(CancellationToken cancellationToken);

    /// <summary>Drops the cached token (after a 401: rotated secret or a rebuilt client).</summary>
    void Invalidate();
}

/// <summary>
/// The Auth0 M2M token of the D1 search writer, cached (singleton). Auth0 meters M2M token issues, and a
/// token lasts a day by default, so one token serves every write until shortly before it expires.
/// The secret is sent only to the configured Auth0 domain and never logged.
/// </summary>
public sealed class D1SearchTokenSource(
    IHttpClientFactory httpClientFactory,
    D1SearchConnection connection,
    TimeProvider timeProvider) : ID1SearchTokenSource
{
    public const string HttpClientName = "d1-search-auth0";
    private static readonly TimeSpan RefreshBeforeExpiry = TimeSpan.FromMinutes(5);
    private readonly D1SearchConnection _connection = connection;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile CachedToken? _token;

    public async Task<string> GetToken(CancellationToken cancellationToken)
    {
        if (TryFresh(out var token))
        {
            return token;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (TryFresh(out token))
            {
                return token;
            }

            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.PostAsJsonAsync(
                new Uri($"https://{_connection.Auth0Domain}/oauth/token"),
                new TokenRequest(_connection.ClientId, _connection.ClientSecret, _connection.Audience),
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Auth0's error body names the problem (access_denied, invalid client); it never echoes the secret.
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new D1SearchWriteException(
                    $"Auth0 token request for the D1 search writer failed with {(int)response.StatusCode}: {Truncate(body)}");
            }

            var issued = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                         ?? throw new D1SearchWriteException("Auth0 returned no token for the D1 search writer.");
            if (string.IsNullOrWhiteSpace(issued.AccessToken))
            {
                throw new D1SearchWriteException("Auth0 returned an empty token for the D1 search writer.");
            }

            var expires = timeProvider.GetUtcNow().AddSeconds(Math.Max(0, issued.ExpiresIn));
            _token = new CachedToken(issued.AccessToken, expires);
            return issued.AccessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Invalidate() => _token = null;

    private bool TryFresh(out string token)
    {
        if (_token is { } cached && cached.ExpiresAt - RefreshBeforeExpiry > timeProvider.GetUtcNow())
        {
            token = cached.Token;
            return true;
        }

        token = string.Empty;
        return false;
    }

    private static string Truncate(string body) => body.Length <= 300 ? body : body[..300] + "…";

    private sealed record CachedToken(string Token, DateTimeOffset ExpiresAt);

    private sealed record TokenRequest(
        [property: JsonPropertyName("client_id")] string ClientId,
        [property: JsonPropertyName("client_secret")] string ClientSecret,
        [property: JsonPropertyName("audience")] string Audience)
    {
        [JsonPropertyName("grant_type")] public string GrantType => "client_credentials";
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] long ExpiresIn);
}

/// <summary>A D1 search write that could not be completed; always caught and logged by <see cref="D1SearchIndexWriter"/>.</summary>
public sealed class D1SearchWriteException(string message) : Exception(message);
