namespace RedditPodcastPoster.ContentPublisher.Publishers;

public interface ISearchSuggestionsPublisher
{
    /// <summary>
    /// Builds and uploads the typeahead index. Throws on failure so callers
    /// (timer / CLI) surface an unsuccessful result.
    /// </summary>
    Task PublishSearchSuggestions(CancellationToken cancellationToken = default);
}
