using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public interface IYouTubeUrlCategoriser
{
    Task<ResolvedYouTubeItem?> Resolve(Podcast? podcasts, 
        IList<RedditPodcastPoster.Models.Episode> podcastEpisodes,
        Uri url, 
        IndexingContext indexingContext);

    Task<ResolvedYouTubeItem?> Resolve(PodcastServiceSearchCriteria criteria, 
        Podcast? matchingPodcast,
        IList<RedditPodcastPoster.Models.Episode> episodes,
        IndexingContext indexingContext);
}