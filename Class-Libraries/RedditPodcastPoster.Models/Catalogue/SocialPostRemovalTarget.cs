using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Models.Catalogue;

/// <summary>
/// What the tweet and Bluesky removers need to find a post: the item id (for logs), the parent / series display
/// name, length and release used in the post text, and the Bluesky AT URI when one was stored.
/// Podcast episodes, catalogue playables (TV episodes, news reports) and films all map onto this.
/// </summary>
public sealed record SocialPostRemovalTarget(
    Guid Id,
    string ParentName,
    TimeSpan Length,
    DateTime ReleaseUtc,
    string? BlueskyPost)
{
    public static SocialPostRemovalTarget From(PodcastEpisode podcastEpisode) =>
        new(podcastEpisode.Episode.Id,
            podcastEpisode.Podcast.Name,
            podcastEpisode.Episode.Length,
            podcastEpisode.Episode.ReleaseUtc,
            podcastEpisode.Episode.BlueskyPost);
}
