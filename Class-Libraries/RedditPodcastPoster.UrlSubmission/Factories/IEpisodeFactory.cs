using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Factories;

public interface IEpisodeFactory
{
    Episode CreateEpisode(CategorisedItem categorisedItem);
}