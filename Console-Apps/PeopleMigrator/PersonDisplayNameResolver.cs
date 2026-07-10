using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace PeopleMigrator;

internal sealed class PersonDisplayNameResolver(
    HttpClient httpClient,
    ILogger<PersonDisplayNameResolver> logger) : IPersonDisplayNameResolver
{
    private readonly Dictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private bool? _torProxyAvailable;
    private DateTimeOffset _lastXLookupUtc = DateTimeOffset.MinValue;

    private static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan XLookupDelay = TimeSpan.FromMilliseconds(1500);

    private static readonly string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    private static readonly string[] XProfileUrls =
    [
        "https://x.com/{0}",
        "https://twitter.com/{0}"
    ];

    private static readonly string[] NitterHosts =
    [
        "nitter.poast.org",
        "nitter.privacydev.net",
        "nitter.net"
    ];

    public async Task<DisplayNameResolution> ResolveDisplayNameAsync(
        string? twitterHandle,
        string? blueskyHandle,
        CancellationToken cancellationToken)
    {
        string? twitterName = null;
        string? blueskyName = null;

        if (!string.IsNullOrWhiteSpace(twitterHandle))
        {
            var twitterResult = await LookupTwitterDisplayNameAsync(twitterHandle, useProxy: false, cancellationToken);
            twitterName = twitterResult.Name;
            if (twitterName == null && twitterResult.ShouldRetryViaProxy && IsProxyRetryAvailable())
            {
                var proxyResult = await LookupTwitterDisplayNameAsync(twitterHandle, useProxy: true, cancellationToken);
                twitterName = proxyResult.Name;
            }
        }

        if (!string.IsNullOrWhiteSpace(blueskyHandle))
        {
            blueskyName = await LookupBlueskyDisplayNameAsync(blueskyHandle, cancellationToken);
        }

        twitterName = NormalizeResolvedDisplayName(twitterName);
        blueskyName = NormalizeResolvedDisplayName(blueskyName);

        var chosen = ChooseBestDisplayName(twitterName, blueskyName, twitterHandle, blueskyHandle);
        var chosenSource = ResolveChosenSource(chosen, twitterName, blueskyName);

        return new DisplayNameResolution
        {
            ChosenName = chosen,
            TwitterName = twitterName,
            BlueskyName = blueskyName,
            ChosenSource = chosenSource
        };
    }

    internal static string? ResolveChosenSource(string? chosen, string? twitterName, string? blueskyName)
    {
        if (string.IsNullOrWhiteSpace(chosen))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(twitterName) &&
            chosen.Equals(twitterName, StringComparison.OrdinalIgnoreCase))
        {
            return "twitter";
        }

        if (!string.IsNullOrWhiteSpace(blueskyName) &&
            chosen.Equals(blueskyName, StringComparison.OrdinalIgnoreCase))
        {
            return "bluesky";
        }

        return null;
    }

    internal static string? ChooseBestDisplayName(
        string? twitterName,
        string? blueskyName,
        string? twitterHandle,
        string? blueskyHandle)
    {
        var candidates = new[] { twitterName, blueskyName }
            .Where(x => IsUsableDisplayName(x, twitterHandle, blueskyHandle))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates
            .OrderByDescending(x => x.Contains(' '))
            .ThenByDescending(x => x.Length)
            .First();
    }

    internal static bool IsUsableDisplayName(string? name, string? twitterHandle, string? blueskyHandle)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var trimmed = name.Trim();
        if (trimmed.StartsWith('@'))
        {
            return false;
        }

        var twitterToken = PersonHandleNormalizer.ToMatchToken(twitterHandle);
        var blueskyToken = PersonHandleNormalizer.ToMatchToken(blueskyHandle);

        var words = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
        {
            // Multi-word display names are treated as real names even when a first name
            // token matches a short handle (e.g. "John Smith" vs @john).
            return true;
        }

        var singleToken = NormalizeDisplayNameToken(trimmed);
        if (string.IsNullOrWhiteSpace(singleToken))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(twitterToken) &&
            singleToken.Equals(twitterToken, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(blueskyToken) &&
            singleToken.Equals(blueskyToken, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    internal static string NormalizeDisplayNameToken(string value)
    {
        return PersonHandleNormalizer.NormalizeLettersAndDigits(value);
    }

    internal static string? NormalizeResolvedDisplayName(string? name)
    {
        return EpisodeGuestNameExtractor.NormalizePersonName(name);
    }

    private async Task<string?> LookupBlueskyDisplayNameAsync(string handle, CancellationToken cancellationToken)
    {
        var actor = ToBlueskyActor(handle);
        var cacheKey = $"bsky:{actor}";
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        try
        {
            var url =
                $"https://public.api.bsky.app/xrpc/app.bsky.actor.getProfile?actor={Uri.EscapeDataString(actor)}";
            using var response = await httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("Bluesky profile lookup failed for {Handle}: {StatusCode}", actor, response.StatusCode);
                _cache[cacheKey] = null;
                return null;
            }

            var profile = await response.Content.ReadFromJsonAsync<BlueskyProfileResponse>(cancellationToken);
            var displayName = profile?.DisplayName?.Trim();
            _cache[cacheKey] = displayName;
            return displayName;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Bluesky profile lookup failed for {Handle}.", handle);
            _cache[cacheKey] = null;
            return null;
        }
    }

    private async Task<TwitterLookupResult> LookupTwitterDisplayNameAsync(
        string handle,
        bool useProxy,
        CancellationToken cancellationToken)
    {
        var username = PersonHandleNormalizer.ToMatchToken(handle);
        if (string.IsNullOrWhiteSpace(username))
        {
            return TwitterLookupResult.Empty;
        }

        var cacheKey = useProxy ? $"x-proxy:{username}" : $"x:{username}";
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return new TwitterLookupResult(cached, ShouldRetryViaProxy: false);
        }

        HttpClient? ownedClient = null;
        try
        {
            await ThrottleXLookupAsync(cancellationToken);

            var client = useProxy
                ? ownedClient = CreateProxiedProfileHttpClient()
                : httpClient;
            if (useProxy)
            {
                logger.LogInformation(
                    "X profile scrape for @{Handle} via proxy {Proxy}.",
                    username,
                    ResolveProxyUrlForRetry());
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(LookupTimeout);

            var displayName = await ScrapeXProfileAsync(client, username, cts.Token);
            if (displayName == null && !useProxy)
            {
                displayName = await ScrapeNitterFallbackAsync(client, username, cts.Token);
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                logger.LogDebug(
                    "X profile scrape returned no display name for @{Handle}{ProxySuffix}.",
                    username,
                    useProxy ? " (proxy)" : string.Empty);
                _cache[cacheKey] = null;
                return new TwitterLookupResult(null, ShouldRetryViaProxy: !useProxy);
            }

            _cache[cacheKey] = displayName;
            return new TwitterLookupResult(displayName, ShouldRetryViaProxy: false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "X profile scrape failed for {Handle}{ProxySuffix}.", handle, useProxy ? " (proxy)" : string.Empty);
            if (useProxy)
            {
                _torProxyAvailable = false;
            }

            _cache[cacheKey] = null;
            return new TwitterLookupResult(null, ShouldRetryViaProxy: !useProxy);
        }
        finally
        {
            ownedClient?.Dispose();
        }
    }

    private async Task<string?> ScrapeXProfileAsync(HttpClient client, string username, CancellationToken cancellationToken)
    {
        foreach (var template in XProfileUrls)
        {
            var url = string.Format(template, Uri.EscapeDataString(username));
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);
            request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogDebug("X profile page {Url} returned {StatusCode}.", url, response.StatusCode);
                continue;
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var displayName = XProfileDisplayNameParser.ParseDisplayName(html, username);
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                logger.LogDebug("X profile scrape resolved @{Handle} to {DisplayName} from {Url}.", username, displayName, url);
                return displayName;
            }
        }

        return null;
    }

    private async Task<string?> ScrapeNitterFallbackAsync(
        HttpClient client,
        string username,
        CancellationToken cancellationToken)
    {
        foreach (var host in NitterHosts)
        {
            var url = $"https://{host}/{Uri.EscapeDataString(username)}";
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("User-Agent", BrowserUserAgent);

                using var response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogDebug("Nitter profile page {Url} returned {StatusCode}.", url, response.StatusCode);
                    continue;
                }

                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                var displayName = XProfileDisplayNameParser.ParseDisplayName(html, username);
                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    logger.LogInformation(
                        "X profile scrape resolved @{Handle} to {DisplayName} via nitter ({Host}).",
                        username,
                        displayName,
                        host);
                    return displayName;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "Nitter fallback failed for {Url}.", url);
            }
        }

        return null;
    }

    private async Task ThrottleXLookupAsync(CancellationToken cancellationToken)
    {
        var elapsed = DateTimeOffset.UtcNow - _lastXLookupUtc;
        if (elapsed < XLookupDelay)
        {
            await Task.Delay(XLookupDelay - elapsed, cancellationToken);
        }

        _lastXLookupUtc = DateTimeOffset.UtcNow;
    }

    private bool IsProxyRetryAvailable()
    {
        if (_torProxyAvailable == false && ResolveConfiguredProxyUrl() == null)
        {
            return false;
        }

        return true;
    }

    private HttpClient CreateProxiedProfileHttpClient()
    {
        var proxyUrl = ResolveProxyUrlForRetry();
        var transport = new SocketsHttpHandler
        {
            Proxy = new WebProxy(proxyUrl),
            UseProxy = true
        };

        return new HttpClient(transport)
        {
            Timeout = LookupTimeout
        };
    }

    internal static string? ResolveConfiguredProxyUrl()
    {
        foreach (var key in new[] { "PEOPLE_MIGRATOR_PROXY", "HTTPS_PROXY", "HTTP_PROXY", "ALL_PROXY" })
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    internal static string ResolveProxyUrlForRetry()
    {
        return ResolveConfiguredProxyUrl() ?? "socks5://127.0.0.1:9150";
    }

    private static string ToBlueskyActor(string handle)
    {
        var trimmed = handle.Trim().TrimStart('@');
        if (trimmed.Contains('.', StringComparison.Ordinal))
        {
            return trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).First();
        }

        return $"{trimmed}.bsky.social";
    }

    private sealed class BlueskyProfileResponse
    {
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }
    }

    private readonly record struct TwitterLookupResult(string? Name, bool ShouldRetryViaProxy)
    {
        public static TwitterLookupResult Empty { get; } = new(null, false);
    }
}
