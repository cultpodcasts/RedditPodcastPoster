using RedditPodcastPoster.ContentPublisher.Models;

namespace RedditPodcastPoster.ContentPublisher.Builders;

public interface ISearchSuggestionsIndexBuilder
{
    Task<SearchSuggestionsCorpus> BuildAsync(CancellationToken cancellationToken = default);
}
