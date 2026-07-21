using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Processors;

public interface ICategorisedItemProcessor
{
    Task<SubmitResult> ProcessCategorisedItem(
        CategorisedItem categorisedItem, SubmitOptions submitOptions);
}