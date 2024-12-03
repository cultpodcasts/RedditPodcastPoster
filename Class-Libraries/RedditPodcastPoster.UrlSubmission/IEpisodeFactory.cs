using RedditPodcastPoster.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission;

public interface IEpisodeFactory
{
    Episode CreateEpisode(CategorisedItem categorisedItem);
}