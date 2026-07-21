using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Processors;

public interface IPodcastProcessor
{
    Task<SubmitResult> AddEpisodeToExistingPodcast(CategorisedItem categorisedItem);
}