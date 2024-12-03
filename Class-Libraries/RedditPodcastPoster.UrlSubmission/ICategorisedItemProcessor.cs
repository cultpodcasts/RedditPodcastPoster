using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission;

public interface ICategorisedItemProcessor
{
    Task<SubmitResult> ProcessCategorisedItem(
        CategorisedItem categorisedItem, SubmitOptions submitOptions);
}