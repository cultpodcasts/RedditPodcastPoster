using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission;

public interface ICategorisedItemProcessor
{
    Task<SubmitResult> ProcessCategorisedItem(
        CategorisedItem categorisedItem, SubmitOptions submitOptions);
}