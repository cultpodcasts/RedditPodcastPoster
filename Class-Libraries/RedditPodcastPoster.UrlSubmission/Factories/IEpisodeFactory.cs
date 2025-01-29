using RedditPodcastPoster.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Factories;

public interface IEpisodeFactory
{
    Episode CreateEpisode(CategorisedItem categorisedItem);
}