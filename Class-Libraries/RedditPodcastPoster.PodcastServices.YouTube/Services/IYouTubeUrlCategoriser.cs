using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public interface IYouTubeUrlCategoriser
{
    Task<ResolvedYouTubeItem?> Resolve(Podcast? podcasts, IEnumerable<RedditPodcastPoster.Models.V2.Episode> podcastEpisodes,
        Uri url, IndexingContext indexingContext);

    Task<ResolvedYouTubeItem?> Resolve(PodcastServiceSearchCriteria criteria, Podcast? matchingPodcast,
        IEnumerable<RedditPodcastPoster.Models.V2.Episode> episodes,
        IndexingContext indexingContext);
}