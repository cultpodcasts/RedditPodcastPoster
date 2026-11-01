using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.Twitter.Auth;

/// <summary>Shared by the refresher and the exchange console: saving a pair X has just issued.</summary>
public static class XOAuth2TokenPersistence
{
    public const int SaveAttempts = 3;

    /// <summary>
    /// Saves a pair X has just issued (the previous refresh token is already dead), retrying up to
    /// <see cref="SaveAttempts"/> times and never cancelled by the caller. Returns the snapshot now stored, which may
    /// hold a newer pair another host saved, or null when every attempt failed.
    /// </summary>
    public static async Task<XOAuth2TokenSnapshot?> SaveRotatedPair(
        IXOAuth2TokenStore tokenStore,
        XOAuth2TokenSnapshot leased,
        XOAuth2TokenGrant grant,
        ILogger logger)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await tokenStore.SaveTokens(leased, grant, CancellationToken.None);
            }
            catch (Exception ex) when (attempt < SaveAttempts)
            {
                logger.LogWarning(ex, "X OAuth 2.0: saving the new token pair failed (attempt {Attempt}); retrying.",
                    attempt);
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex,
                    "X OAuth 2.0: the new token pair could not be saved after {Attempts} attempts. The stored refresh token is now invalid; run XOAuth2Authorize with --execute again.",
                    SaveAttempts);
                return null;
            }
        }
    }
}
