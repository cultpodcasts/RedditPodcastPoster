using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Logging;

/// <summary>
/// Logs when Spotify marks an episode non-playable for the requested market.
/// Market unavailability that is dropped is Error (must not be silent); other restrictions stay Warning.
/// A direct episode-id lookup that still returns the episode uses <see cref="LogReturnedDespiteMarket"/>
/// so skip queries are not told the row was dropped.
/// </summary>
public static class SpotifyNonPlayableSkipLogger
{
    /// <summary>
    /// Spotify <c>restrictions.reason</c> when the item is not available in the requested market.
    /// </summary>
    public const string MarketRestrictionReason = "market";

    public const string MarketUnavailableMessagePrefix = "Spotify episode not available in market:";

    public const string ReturnedDespiteMarketMessagePrefix = "Spotify episode returned despite market:";

    public const string NonPlayableMessageTemplate =
        "Skipping Spotify episode '{EpisodeId}' ('{EpisodeName}') because it is not free/playable (IsPlayable=false, restrictions.reason={RestrictionReason}, market='{Market}').";

    public const string MarketUnavailableMessageTemplate =
        "Spotify episode not available in market: episode-id='{EpisodeId}' title='{EpisodeName}' market='{Market}' restrictions.reason='{RestrictionReason}'";

    public const string ReturnedDespiteMarketMessageTemplate =
        "Spotify episode returned despite market: episode-id='{EpisodeId}' title='{EpisodeName}' market='{Market}' restrictions.reason='{RestrictionReason}'";

    public static bool IsMarketUnavailable(string restrictionReason) =>
        string.Equals(restrictionReason, MarketRestrictionReason, StringComparison.OrdinalIgnoreCase);

    public static void Log(
        ILogger logger,
        SimpleEpisode episode,
        string? market = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(episode);
        Log(logger, episode.Id, episode.Name, episode.GetSpotifyRestrictionReason(), market);
    }

    public static void Log(
        ILogger logger,
        FullEpisode episode,
        string? market = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(episode);
        Log(logger, episode.Id, episode.Name, episode.GetSpotifyRestrictionReason(), market);
    }

    public static void Log(
        ILogger logger,
        string episodeId,
        string episodeName,
        string restrictionReason,
        string? market = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        var resolvedMarket = string.IsNullOrWhiteSpace(market) ? Market.CountryCode : market;

        if (IsMarketUnavailable(restrictionReason))
        {
            logger.LogError(
                MarketUnavailableMessageTemplate,
                episodeId,
                episodeName,
                resolvedMarket,
                restrictionReason);
            return;
        }

        logger.LogWarning(
            NonPlayableMessageTemplate,
            episodeId,
            episodeName,
            restrictionReason,
            resolvedMarket);
    }

    /// <summary>
    /// Direct episode-id lookups keep the episode. This is not a skip: the message prefix must stay
    /// distinct from <see cref="MarketUnavailableMessagePrefix"/> and <see cref="NonPlayableMessageTemplate"/>.
    /// </summary>
    public static void LogReturnedDespiteMarket(
        ILogger logger,
        FullEpisode episode,
        string? market = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(episode);
        var resolvedMarket = string.IsNullOrWhiteSpace(market) ? Market.CountryCode : market;
        logger.LogWarning(
            ReturnedDespiteMarketMessageTemplate,
            episode.Id,
            episode.Name,
            resolvedMarket,
            episode.GetSpotifyRestrictionReason());
    }
}
