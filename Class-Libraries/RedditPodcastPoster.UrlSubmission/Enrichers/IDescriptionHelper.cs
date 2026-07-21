using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Enrichers;

public interface IDescriptionHelper
{
    string EnrichMissingDescription(CategorisedItem categorisedItem);
    string? CollapseDescription(string? description);
}