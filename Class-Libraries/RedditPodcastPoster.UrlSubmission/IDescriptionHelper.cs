using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission;

public interface IDescriptionHelper
{
    string EnrichMissingDescription(CategorisedItem categorisedItem);
    string? CollapseDescription(string? description);
}