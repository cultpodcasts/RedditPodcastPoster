namespace RedditPodcastPoster.ContentPublisher.Publishers;

public interface ISearchSuggestionsPublisher
{
    Task<bool> PublishSearchSuggestions(CancellationToken cancellationToken = default);
}
