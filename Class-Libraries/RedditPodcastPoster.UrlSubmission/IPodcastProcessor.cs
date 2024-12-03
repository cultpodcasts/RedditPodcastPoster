using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission;

public interface IPodcastProcessor
{
    Task<SubmitResult> AddEpisodeToExistingPodcast(CategorisedItem categorisedItem);
}