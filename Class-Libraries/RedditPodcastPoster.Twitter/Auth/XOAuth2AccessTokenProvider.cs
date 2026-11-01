using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Twitter.Configuration;

namespace RedditPodcastPoster.Twitter.Auth;

/// <summary>
/// Hands out the OAuth 2.0 user-context bearer token for the X posting account. Refreshes it when it is close to
/// expiry, or when X rejected it with a 401, under a LookUps lease so only one host refreshes at a time.
/// </summary>
public interface IXOAuth2AccessTokenProvider
{
    /// <summary>
    /// A usable access token, or null when none can be had (no exchange saved yet, refresh refused, or another
    /// refresher did not finish in time). Pass the token X just rejected as <paramref name="rejectedAccessToken"/>
    /// to force a refresh; if another host already replaced it, the newer token is returned without refreshing.
    /// </summary>
    Task<string?> GetAccessToken(string? rejectedAccessToken = null, CancellationToken cancellationToken = default);
}

public sealed class XOAuth2AccessTokenProvider(
    IXOAuth2TokenStore tokenStore,
    IXOAuth2TokenClient tokenClient,
    IOptions<TwitterOptions> options,
    TimeProvider timeProvider,
    ILogger<XOAuth2AccessTokenProvider> logger) : IXOAuth2AccessTokenProvider
{
    /// <summary>Refresh this long before X's stated expiry.</summary>
    public static readonly TimeSpan ExpirySkew = TimeSpan.FromMinutes(5);

    /// <summary>Longer than a token call's timeout, so a live refresher keeps its lease; a dead one's lapses.</summary>
    public static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(60);

    /// <summary>The only token-endpoint error that pauses posting: other 400s are request bugs a deploy can fix.</summary>
    public const string InvalidGrant = "invalid_grant";

    public static readonly TimeSpan WaitInterval = TimeSpan.FromSeconds(1);

    private readonly TwitterOptions _options = options.Value;

    /// <summary>
    /// How long to wait for another host's refresh: half the X request timeout (at least one interval), so a waiting
    /// request fails cleanly before the outer <c>HttpClient.Timeout</c> cancels it.
    /// </summary>
    public static int MaxWaits(TwitterOptions options)
    {
        var timeoutSeconds = options.RequestTimeoutSeconds > 0 ? options.RequestTimeoutSeconds : 15;
        return Math.Max(1, (int) (TimeSpan.FromSeconds(timeoutSeconds / 2.0) / WaitInterval));
    }

    public async Task<string?> GetAccessToken(string? rejectedAccessToken = null,
        CancellationToken cancellationToken = default)
    {
        var attemptId = Guid.NewGuid();
        var maxWaits = MaxWaits(_options);
        for (var wait = 0; wait <= maxWaits; wait++)
        {
            XOAuth2TokenSnapshot? snapshot;
            try
            {
                snapshot = await tokenStore.Get(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("X OAuth 2.0: the request was cancelled while reading the saved token pair.");
                return null;
            }

            if (snapshot is null || !snapshot.State.HasTokens)
            {
                logger.LogError(
                    "X OAuth 2.0 is selected (twitter:AuthMode=OAuth2) but no token pair is saved. Run the XOAuth2TokenExchange console.");
                return null;
            }

            var state = snapshot.State;
            if (state.NeedsReauth)
            {
                logger.LogWarning(
                    "X OAuth 2.0: the saved grant needs re-authorisation since {Since:O} ({Reason}); not calling X. Run the XOAuth2TokenExchange console.",
                    state.NeedsReauthSinceUtc, state.NeedsReauthReason);
                return null;
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var rejected = rejectedAccessToken is not null && rejectedAccessToken == state.AccessToken;
            if (!rejected && state.IsAccessTokenValid(now, ExpirySkew))
            {
                return state.AccessToken;
            }

            if (!state.IsRefreshLeaseHeld(now))
            {
                XOAuth2TokenSnapshot? leased;
                try
                {
                    leased = await tokenStore.TryTakeRefreshLease(snapshot, attemptId, now + LeaseDuration,
                        cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // No refresh token has been spent; a lease that did commit lapses after LeaseDuration.
                    logger.LogWarning("X OAuth 2.0: the request was cancelled while taking the token refresh lease.");
                    return null;
                }

                if (leased is not null)
                {
                    // From here the single-use refresh token is being spent: never let the caller's (request
                    // timeout) cancellation cut it short. The token client has its own timeout, inside the lease.
                    return await RefreshHoldingLease(leased);
                }
            }

            if (wait < maxWaits)
            {
                // Another host is refreshing; its new pair appears in LookUps when it finishes.
                try
                {
                    await Task.Delay(WaitInterval, timeProvider, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    logger.LogWarning("X OAuth 2.0: the request was cancelled while waiting for another host's token refresh.");
                    return null;
                }
            }
        }

        logger.LogWarning("X OAuth 2.0: another host's token refresh did not finish within {Seconds}s.",
            (int) (WaitInterval * maxWaits).TotalSeconds);
        return null;
    }

    private async Task<string?> RefreshHoldingLease(XOAuth2TokenSnapshot leased)
    {
        var result = await tokenClient.Refresh(leased.State.RefreshToken!, CancellationToken.None);
        if (result is {Outcome: XOAuth2TokenOutcome.Rejected} && HasOAuth1Credentials())
        {
            // X's guidance: a lost or expired refresh token is recovered by exchanging the OAuth 1.0a token again,
            // which works until OAuth 1.0a is retired on 1 Jan 2027.
            logger.LogWarning("X OAuth 2.0 refresh token was refused ({Error}); exchanging the OAuth 1.0a token again.",
                result.Error);
            result = await tokenClient.ExchangeOAuth1(_options.AccessToken!, _options.AccessTokenSecret!,
                CancellationToken.None);
        }

        if (result is {Outcome: XOAuth2TokenOutcome.Rejected, Error: InvalidGrant})
        {
            // The grant itself is refused and nothing automatic can fix it: stop every host calling the token
            // endpoint on each request until a new pair is saved.
            var marked = await tokenStore.MarkNeedsReauth(leased, result.Error,
                timeProvider.GetUtcNow().UtcDateTime, CancellationToken.None);
            if (marked)
            {
                logger.LogCritical(
                    "X OAuth 2.0: X refused the saved grant ({Error}) and it could not be recovered automatically. Posting to X is paused until the XOAuth2TokenExchange console saves a new pair.",
                    result.Error);
            }

            return null;
        }

        if (result is not {Outcome: XOAuth2TokenOutcome.Success, Grant: not null})
        {
            logger.LogError("X OAuth 2.0 token refresh failed: {Outcome} ({Error}).", result.Outcome, result.Error);
            await tokenStore.ReleaseRefreshLease(leased, CancellationToken.None);
            return null;
        }

        // The refresh token we sent is now dead; the new pair must be saved before the access token is used.
        var saved = await XOAuth2TokenPersistence.SaveRotatedPair(tokenStore, leased, result.Grant, logger);
        return saved?.State.AccessToken;
    }

    private bool HasOAuth1Credentials() =>
        !string.IsNullOrWhiteSpace(_options.AccessToken) && !string.IsNullOrWhiteSpace(_options.AccessTokenSecret);
}
