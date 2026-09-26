using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.UrlSubmission.Services;

/// <summary>
/// One case-insensitive name query. Callers treat 0 as create, 1 as reuse, and many as 409.
/// An exact spelling still sees a case-variant sibling, so it cannot hide that sibling.
/// </summary>
public static class PublisherNameAttachLookup
{
    public static async Task<IReadOnlyList<T>> FindByName<T>(
        IFilterableRepository<T> repository,
        string name,
        CancellationToken cancellationToken = default)
        where T : Publisher
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return [];
        }

        // Invariant on the constant; ToLower on Name so Cosmos LOWER stays culture-stable.
        var lowerName = name.ToLowerInvariant();
        var matches = new List<T>();
        await foreach (var candidate in repository
                           .GetAllBy(x => x.Name.ToLower() == lowerName)
                           .WithCancellation(cancellationToken))
        {
            matches.Add(candidate);
        }

        return matches;
    }
}
