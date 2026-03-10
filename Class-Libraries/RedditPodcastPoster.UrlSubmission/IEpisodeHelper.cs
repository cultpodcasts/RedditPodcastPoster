using RedditPodcastPoster.Models.V2;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission;

public interface IEpisodeHelper
{
    bool IsMatchingEpisode(Episode episode, CategorisedItem categorisedItem);
}