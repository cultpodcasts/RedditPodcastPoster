using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Factories;

public interface IEpisodeFactory
{
    Episode CreateEpisode(CategorisedItem categorisedItem);
}