using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.UrlSubmission.Services;

/// <summary>
/// Exact name, then case-insensitive. Callers treat 0 as create, 1 as reuse, and many as 409.
/// </summary>
public static class PublisherNameAttachLookup
{
    public static async Task<IReadOnlyList<T>> FindByName<T>(
        IFilterableRepository<T> repository,
        string name,
        CancellationToken cancellationToken = default)
        where T : Publisher
    {
        var matches = new List<T>();
        await foreach (var candidate in repository
                           .GetAllBy(x => x.Name == name)
                           .WithCancellation(cancellationToken))
        {
            matches.Add(candidate);
        }

        if (matches.Count == 0 && !string.IsNullOrWhiteSpace(name))
        {
            var lowerName = name.ToLower();
            await foreach (var candidate in repository
                               .GetAllBy(x => x.Name.ToLower() == lowerName)
                               .WithCancellation(cancellationToken))
            {
                matches.Add(candidate);
            }
        }

        return matches;
    }
}
