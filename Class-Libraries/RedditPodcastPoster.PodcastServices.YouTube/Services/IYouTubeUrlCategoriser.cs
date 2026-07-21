using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using EpisodeModel = RedditPodcastPoster.Models.Episodes.Episode;

namespace RedditPodcastPoster.PodcastServices.YouTube.Services;

public interface IYouTubeUrlCategoriser
{
    Task<ResolvedYouTubeItem?> Resolve(Podcast? podcasts, 
        IList<EpisodeModel> podcastEpisodes,
        Uri url, 
        IndexingContext indexingContext);

    Task<ResolvedYouTubeItem?> Resolve(PodcastServiceSearchCriteria criteria, 
        Podcast? matchingPodcast,
        IList<EpisodeModel> episodes,
        IndexingContext indexingContext);
}
