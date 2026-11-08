using System.Text.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Twitter.Configuration;

namespace RedditPodcastPoster.Twitter.Auth;

/// <summary>
/// Calls <c>POST /2/oauth2/token</c> as a confidential client (HTTP Basic with the OAuth 2.0 Client ID and Secret).
/// Refresh: <c>grant_type=refresh_token</c>. One-off migration: the OAuth 1.0a token exchange grant.
/// See https://docs.x.com/fundamentals/authentication/oauth-2-0/oauth-1-0a-token-exchange. Never logs tokens.
/// </summary>
public interface IXOAuth2TokenClient
{
    Task<XOAuth2TokenResult> Refresh(string refreshToken, CancellationToken cancellationToken = default);

    Task<XOAuth2TokenResult> ExchangeOAuth1(string oauth1AccessToken, string oauth1AccessTokenSecret,
        CancellationToken cancellationToken = default);

    /// <summary>Authorization-code grant with PKCE (<c>code_verifier</c>). Confidential client: HTTP Basic as well.</summary>
    Task<XOAuth2TokenResult> ExchangeAuthorizationCode(string code, string codeVerifier, string redirectUri,
        CancellationToken cancellationToken = default);
}

public enum XOAuth2TokenOutcome
{
    Success = 1,
    /// <summary>HTTP 400 (invalid_grant and friends): the token sent is not accepted; retrying it will not help.</summary>
    Rejected,
    /// <summary>429, 5xx or a timeout: try again later with the same token.</summary>
    Transient,
    /// <summary>The OAuth 2.0 client credentials are missing or wrong (401, invalid_client, unauthorized_client).</summary>
    ClientCredentialsInvalid
}

public sealed record XOAuth2TokenResult(
    XOAuth2TokenOutcome Outcome,
    XOAuth2TokenGrant? Grant = null,
    HttpStatusCode? StatusCode = null,
    string? Error = null);

public sealed class XOAuth2TokenClient(
    HttpClient httpClient,
    IOptions<TwitterOptions> options,
    TimeProvider timeProvider,
    ILogger<XOAuth2TokenClient> logger) : IXOAuth2TokenClient
{
    public const string RefreshSource = "refresh";
    public const string ExchangeSource = "exchange";
    public const string AuthorizationSource = "authorization_code";
    private const string TokenPath = "2/oauth2/token";
    private readonly TwitterOptions _options = options.Value;

    public Task<XOAuth2TokenResult> Refresh(string refreshToken, CancellationToken cancellationToken = default) =>
        Post(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        }, RefreshSource, cancellationToken);

    public Task<XOAuth2TokenResult> ExchangeOAuth1(string oauth1AccessToken, string oauth1AccessTokenSecret,
        CancellationToken cancellationToken = default) =>
        Post(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:token-exchange",
            ["subject_token"] = oauth1AccessToken,
            ["subject_token_type"] = "urn:x:params:oauth:token-type:oauth1_token",
            ["oauth_token_secret"] = oauth1AccessTokenSecret
        }, ExchangeSource, cancellationToken);

    public Task<XOAuth2TokenResult> ExchangeAuthorizationCode(string code, string codeVerifier, string redirectUri,
        CancellationToken cancellationToken = default) =>
        Post(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier
        }, AuthorizationSource, cancellationToken);

    private async Task<XOAuth2TokenResult> Post(Dictionary<string, string> form, string source,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.OAuth2ClientId) || string.IsNullOrWhiteSpace(_options.OAuth2ClientSecret))
        {
            logger.LogError("X OAuth 2.0 {Source}: twitter:OAuth2ClientId / twitter:OAuth2ClientSecret are not configured.", source);
            return new XOAuth2TokenResult(XOAuth2TokenOutcome.ClientCredentialsInvalid, Error: "client_not_configured");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenPath)
        {
            Content = new FormUrlEncodedContent(form)
        };
        var basic = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.OAuth2ClientId}:{_options.OAuth2ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException &&
                                   !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("X OAuth 2.0 {Source} request failed or timed out: {ExceptionType}.", source, ex.GetType().Name);
            return new XOAuth2TokenResult(XOAuth2TokenOutcome.Transient, Error: ex.GetType().Name);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                TokenResponse? body;
                try
                {
                    body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
                }
                catch (Exception ex) when (ex is JsonException or NotSupportedException or HttpRequestException ||
                                           (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
                {
                    logger.LogError("X OAuth 2.0 {Source} returned success with an unreadable body: {ExceptionType}.",
                        source, ex.GetType().Name);
                    return new XOAuth2TokenResult(XOAuth2TokenOutcome.Transient, StatusCode: response.StatusCode,
                        Error: "invalid_response");
                }

                if (body is null || string.IsNullOrWhiteSpace(body.AccessToken) || string.IsNullOrWhiteSpace(body.RefreshToken))
                {
                    logger.LogError("X OAuth 2.0 {Source} returned success without an access and refresh token.", source);
                    return new XOAuth2TokenResult(XOAuth2TokenOutcome.Transient, StatusCode: response.StatusCode,
                        Error: "missing_tokens");
                }

                var now = timeProvider.GetUtcNow().UtcDateTime;
                var expiresIn = body.ExpiresIn is > 0 ? body.ExpiresIn.Value : 7200;
                logger.LogInformation(
                    "X OAuth 2.0 {Source} succeeded. Access token expires in {ExpiresIn}s. Scope: '{Scope}'.",
                    source, expiresIn, body.Scope);
                return new XOAuth2TokenResult(XOAuth2TokenOutcome.Success,
                    new XOAuth2TokenGrant(body.AccessToken, body.RefreshToken, now.AddSeconds(expiresIn), body.Scope,
                        now, source),
                    response.StatusCode);
            }

            var error = await ReadError(response, cancellationToken);
            var outcome = Classify(response.StatusCode, error);
            logger.LogError("X OAuth 2.0 {Source} failed. Status-code: '{StatusCode}'. Error: '{Error}'. Outcome: {Outcome}.",
                source, response.StatusCode, error, outcome);
            return new XOAuth2TokenResult(outcome, StatusCode: response.StatusCode, Error: error);
        }
    }

    internal static XOAuth2TokenOutcome Classify(HttpStatusCode status, string? error)
    {
        if (error is "invalid_client" or "unauthorized_client" || status == HttpStatusCode.Unauthorized)
        {
            return XOAuth2TokenOutcome.ClientCredentialsInvalid;
        }

        if (status == HttpStatusCode.TooManyRequests || (int) status >= 500)
        {
            return XOAuth2TokenOutcome.Transient;
        }

        return XOAuth2TokenOutcome.Rejected;
    }

    private static async Task<string?> ReadError(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<TokenErrorResponse>(cancellationToken);
            return body?.Error;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int? ExpiresIn { get; set; }
        [JsonPropertyName("scope")] public string? Scope { get; set; }
    }

    private sealed class TokenErrorResponse
    {
        [JsonPropertyName("error")] public string? Error { get; set; }
    }
}
