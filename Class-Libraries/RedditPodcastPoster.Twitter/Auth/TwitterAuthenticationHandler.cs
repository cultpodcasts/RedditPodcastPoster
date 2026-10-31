using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OAuth;
using RedditPodcastPoster.Twitter.Configuration;

namespace RedditPodcastPoster.Twitter.Auth;

/// <summary>
/// Authenticates every X API request from <see cref="TwitterOptions.AuthMode"/>.
/// <list type="bullet">
/// <item>OAuth1 (default): signs with the OAuth 1.0a keys, as before.</item>
/// <item>OAuth2: sends <c>Authorization: Bearer</c>. On a 401 it asks for one refresh and resends once.</item>
/// </list>
/// </summary>
public sealed class TwitterAuthenticationHandler(
    IOptions<TwitterOptions> options,
    IXOAuth2AccessTokenProvider accessTokenProvider,
    ILogger<TwitterAuthenticationHandler> logger) : DelegatingHandler
{
    private readonly TwitterOptions _options = options.Value;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        _options.AuthMode == TwitterAuthMode.OAuth2
            ? SendWithBearer(request, cancellationToken)
            : SendWithOAuth1(request, cancellationToken);

    private async Task<HttpResponseMessage> SendWithOAuth1(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var oauth = new OAuthMessageHandler(_options.ConsumerKey, _options.ConsumerSecret, _options.AccessToken,
            _options.AccessTokenSecret)
        {
            InnerHandler = new ForwardingHandler((r, ct) => base.SendAsync(r, ct))
        };
        using var invoker = new HttpMessageInvoker(oauth, false);
        return await invoker.SendAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendWithBearer(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var accessToken = await accessTokenProvider.GetAccessToken(null, cancellationToken);
        if (accessToken is null)
        {
            return Unavailable(request);
        }

        // Buffer the body so the request can be resent once after a refresh.
        var body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        logger.LogWarning("X rejected the OAuth 2.0 access token (401) for {Method} {Path}; refreshing once.",
            request.Method, request.RequestUri?.AbsolutePath);
        var refreshed = await accessTokenProvider.GetAccessToken(accessToken, cancellationToken);
        if (refreshed is null || refreshed == accessToken)
        {
            return response;
        }

        response.Dispose();
        using var retry = Clone(request, body);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);
        return await base.SendAsync(retry, cancellationToken);
    }

    private static HttpRequestMessage Clone(HttpRequestMessage request, byte[]? body)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) {Version = request.Version};
        foreach (var header in request.Headers.Where(h => h.Key != "Authorization"))
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (body is not null && request.Content is not null)
        {
            clone.Content = new ByteArrayContent(body);
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }

    private static HttpResponseMessage Unavailable(HttpRequestMessage request) =>
        new(HttpStatusCode.Unauthorized)
        {
            RequestMessage = request,
            ReasonPhrase = "X OAuth 2.0 access token unavailable",
            Content = new StringContent(
                "{\"title\":\"Unauthorized\",\"detail\":\"X OAuth 2.0 access token unavailable; see logs.\",\"status\":401}")
        };

    private sealed class ForwardingHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> next) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => next(request, cancellationToken);
    }
}
