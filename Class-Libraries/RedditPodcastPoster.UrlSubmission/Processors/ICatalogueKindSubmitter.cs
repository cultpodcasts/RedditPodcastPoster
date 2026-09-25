using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Processors;

public interface ICatalogueKindSubmitter
{
    /// <summary>
    /// When the submit content-type flag is on and signals classify as Film, TV, or News,
    /// persists that kind and returns a result. Returns null to keep the podcast episode path.
    /// </summary>
    Task<SubmitResult?> TrySubmit(CategorisedItem categorisedItem, SubmitOptions submitOptions);
}
