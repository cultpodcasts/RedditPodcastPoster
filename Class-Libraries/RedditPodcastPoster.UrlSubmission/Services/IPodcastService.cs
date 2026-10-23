using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.UrlSubmission.Services;

public interface IPodcastService
{
    Task<Podcast?> GetPodcastFromEpisodeUrl(Uri url, IndexingContext indexingContext);

    /// <summary>
    /// The owning podcast (same rules as <see cref="GetPodcastFromEpisodeUrl"/>) plus the channel / show ids it
    /// was matched on, so a caller can look for a news organisation or TV show that owns the URL.
    /// </summary>
    Task<RedditPodcastPoster.UrlSubmission.Models.EpisodeUrlOwner> GetEpisodeUrlOwner(Uri url, IndexingContext indexingContext);

    /// <summary>
    /// The channel / show ids on an episode URL, without choosing one podcast. An ambiguous catalogue match is
    /// the caller's conflict to resolve; this method still returns the ids.
    /// </summary>
    Task<RedditPodcastPoster.UrlSubmission.Models.EpisodeUrlOwnerIds> GetOwnerIds(Uri url, IndexingContext indexingContext);

    /// <summary>
    /// True when the podcast is not playlist-scoped, the URL's <c>list=</c> is its playlist, or YouTube lists the
    /// video in its playlist (one <c>playlistItems.list</c> call). False when it is not in it; null when unknown.
    /// </summary>
    Task<bool?> IsVideoInPodcastPlaylist(Podcast podcast, Uri url, IndexingContext indexingContext);
}
