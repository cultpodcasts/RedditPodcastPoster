using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Enrichers;

public interface IEpisodeEnricher
{
    ApplyResolvePodcastServicePropertiesResponse ApplyResolvedPodcastServiceProperties(
        Podcast matchingPodcast,
        CategorisedItem categorisedItem,
        Episode? matchingEpisode);
}
