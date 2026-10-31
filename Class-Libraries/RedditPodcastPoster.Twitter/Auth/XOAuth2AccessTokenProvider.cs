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

    public static readonly TimeSpan WaitInterval = TimeSpan.FromSeconds(1);
    public const int MaxWaits = 20;
    private const int SaveAttempts = 3;

    private readonly TwitterOptions _options = options.Value;

    public async Task<string?> GetAccessToken(string? rejectedAccessToken = null,
        CancellationToken cancellationToken = default)
    {
        var attemptId = Guid.NewGuid();
        for (var wait = 0; wait <= MaxWaits; wait++)
        {
            var snapshot = await tokenStore.Get(cancellationToken);
            if (snapshot is null || !snapshot.State.HasTokens)
            {
                logger.LogError(
                    "X OAuth 2.0 is selected (twitter:AuthMode=OAuth2) but no token pair is saved. Run the XOAuth2TokenExchange console.");
                return null;
            }

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var state = snapshot.State;
            var rejected = rejectedAccessToken is not null && rejectedAccessToken == state.AccessToken;
            if (!rejected && state.IsAccessTokenValid(now, ExpirySkew))
            {
                return state.AccessToken;
            }

            if (!state.IsRefreshLeaseHeld(now))
            {
                var leased = await tokenStore.TryTakeRefreshLease(snapshot, attemptId, now + LeaseDuration,
                    cancellationToken);
                if (leased is not null)
                {
                    return await RefreshHoldingLease(leased, cancellationToken);
                }
            }

            if (wait < MaxWaits)
            {
                // Another host is refreshing; its new pair appears in LookUps when it finishes.
                await Task.Delay(WaitInterval, timeProvider, cancellationToken);
            }
        }

        logger.LogWarning("X OAuth 2.0: another host's token refresh did not finish within {Seconds}s.",
            (int) (WaitInterval * MaxWaits).TotalSeconds);
        return null;
    }

    private async Task<string?> RefreshHoldingLease(XOAuth2TokenSnapshot leased, CancellationToken cancellationToken)
    {
        var result = await tokenClient.Refresh(leased.State.RefreshToken!, cancellationToken);
        if (result is {Outcome: XOAuth2TokenOutcome.Rejected} && HasOAuth1Credentials())
        {
            // X's guidance: a lost or expired refresh token is recovered by exchanging the OAuth 1.0a token again,
            // which works until OAuth 1.0a is retired on 1 Jan 2027.
            logger.LogWarning("X OAuth 2.0 refresh token was refused ({Error}); exchanging the OAuth 1.0a token again.",
                result.Error);
            result = await tokenClient.ExchangeOAuth1(_options.AccessToken!, _options.AccessTokenSecret!,
                cancellationToken);
        }

        if (result is not {Outcome: XOAuth2TokenOutcome.Success, Grant: not null})
        {
            logger.LogError("X OAuth 2.0 token refresh failed: {Outcome} ({Error}).", result.Outcome, result.Error);
            await tokenStore.ReleaseRefreshLease(leased, CancellationToken.None);
            return null;
        }

        // The refresh token we sent is now dead; the new pair must be saved before the access token is used.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await tokenStore.SaveTokens(leased, result.Grant, CancellationToken.None);
                return result.Grant.AccessToken;
            }
            catch (Exception ex) when (attempt < SaveAttempts)
            {
                logger.LogWarning(ex, "X OAuth 2.0: saving the rotated token pair failed (attempt {Attempt}); retrying.",
                    attempt);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex,
                    "X OAuth 2.0: the rotated token pair could not be saved. The stored refresh token is now invalid; run the XOAuth2TokenExchange console again.");
                return null;
            }
        }
    }

    private bool HasOAuth1Credentials() =>
        !string.IsNullOrWhiteSpace(_options.AccessToken) && !string.IsNullOrWhiteSpace(_options.AccessTokenSecret);
}
