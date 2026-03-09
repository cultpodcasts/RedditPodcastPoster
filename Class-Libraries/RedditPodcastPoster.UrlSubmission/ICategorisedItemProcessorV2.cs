using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission;

/// <summary>
/// V2 version of ICategorisedItemProcessor that processes categorised items
/// using detached episode repositories instead of embedded collections.
/// </summary>
public interface ICategorisedItemProcessorV2
{
    /// <summary>
    /// Processes a categorised item (either adds episode to existing podcast or creates new podcast).
    /// </summary>
    Task<SubmitResult> ProcessCategorisedItem(
        CategorisedItem categorisedItem, 
        SubmitOptions submitOptions);
}
