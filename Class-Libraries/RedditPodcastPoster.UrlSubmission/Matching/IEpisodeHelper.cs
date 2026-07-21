using RedditPodcastPoster.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Matching;

public interface IEpisodeHelper
{
    bool IsMatchingEpisode(Episode episode, CategorisedItem categorisedItem);
}