using RedditPodcastPoster.Episodes.Curation;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Matching;

/// <summary>
/// #1484: how a submit may match a split-off that is awaiting curator approval
/// (<see cref="EpisodeServiceLinks.IsAwaitingApproval"/>).
/// </summary>
public static class PendingSplitOffMatching
{
    /// <summary>
    /// False only for a pending split-off that does not hold one of the submitted Spotify / Apple / YouTube ids.
    /// A title-only match must not enrich it in place of the stored episode it was split from; an exact id match is
    /// that episode, so it is enriched (it stays hidden) rather than duplicated as a new public episode.
    /// </summary>
    public static bool MayMatch(Episode episode, CategorisedItem categorisedItem)
    {
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(categorisedItem);
        return !EpisodeServiceLinks.IsAwaitingApproval(episode) || HoldsSubmittedServiceId(episode, categorisedItem);
    }

    private static bool HoldsSubmittedServiceId(Episode episode, CategorisedItem categorisedItem)
    {
        var spotify = EpisodeServicePresence.SpotifyEpisodeId(episode);
        var apple = EpisodeServicePresence.AppleEpisodeId(episode);
        var youTube = EpisodeServicePresence.YouTubeEpisodeId(episode);
        return (categorisedItem.ResolvedSpotifyItem is { } spotifyItem && !string.IsNullOrWhiteSpace(spotify) &&
                spotify == spotifyItem.EpisodeId) ||
               (categorisedItem.ResolvedAppleItem is { } appleItem && apple != null && apple == appleItem.EpisodeId) ||
               (categorisedItem.ResolvedYouTubeItem is { } youTubeItem && !string.IsNullOrWhiteSpace(youTube) &&
                youTube == youTubeItem.EpisodeId);
    }
}
