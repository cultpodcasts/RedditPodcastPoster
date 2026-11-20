using RedditPodcastPoster.Episodes.Curation;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Matching;

/// <summary>
/// #1484: how a submit may match a split-off held for curator review (pending or rejected,
/// <see cref="EpisodeServiceLinks.IsHeldForReview"/>), and which of several matches holds the submitted id.
/// </summary>
public static class SplitOffMatching
{
    /// <summary>
    /// False only for a held split-off that does not hold one of the submitted Spotify / Apple / YouTube ids.
    /// A title-only match must not enrich it in place of the stored episode it was split from; an exact id match is
    /// that episode, so it is matched (and stays hidden) rather than duplicated as a new public episode.
    /// </summary>
    public static bool MayMatch(Episode episode, CategorisedItem categorisedItem)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(categorisedItem);
        return !EpisodeServiceLinks.IsHeldForReview(episode) || HoldsSubmittedServiceId(episode, categorisedItem);
    }

    /// <summary>
    /// True when <paramref name="episode"/> holds the id of the submitted (authority) service: the URL the
    /// submitter actually gave, not one resolved from it. Used to prefer that episode when several match, e.g. a
    /// split-off holding the submitted Spotify id over the stored episode that matches only by a YouTube id and title.
    /// </summary>
    public static bool HoldsAuthorityId(Episode episode, CategorisedItem categorisedItem)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(categorisedItem);
        return categorisedItem.Authority switch
        {
            Service.Spotify => SpotifyMatches(episode, categorisedItem),
            Service.Apple => AppleMatches(episode, categorisedItem),
            Service.YouTube => YouTubeMatches(episode, categorisedItem),
            _ => false
        };
    }

    private static bool HoldsSubmittedServiceId(Episode episode, CategorisedItem categorisedItem) =>
        SpotifyMatches(episode, categorisedItem) ||
        AppleMatches(episode, categorisedItem) ||
        YouTubeMatches(episode, categorisedItem);

    private static bool SpotifyMatches(Episode episode, CategorisedItem categorisedItem)
    {
        var id = EpisodeServicePresence.SpotifyEpisodeId(episode);
        return categorisedItem.ResolvedSpotifyItem is { } item && !string.IsNullOrWhiteSpace(id) && id == item.EpisodeId;
    }

    private static bool AppleMatches(Episode episode, CategorisedItem categorisedItem)
    {
        var id = EpisodeServicePresence.AppleEpisodeId(episode);
        return categorisedItem.ResolvedAppleItem is { } item && id != null && id == item.EpisodeId;
    }

    private static bool YouTubeMatches(Episode episode, CategorisedItem categorisedItem)
    {
        var id = EpisodeServicePresence.YouTubeEpisodeId(episode);
        return categorisedItem.ResolvedYouTubeItem is { } item && !string.IsNullOrWhiteSpace(id) && id == item.EpisodeId;
    }
}
