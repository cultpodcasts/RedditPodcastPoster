using System.Collections.Concurrent;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.PodcastServices.Abstractions.Caches;
using RedditPodcastPoster.PodcastServices.Apple.Models;

namespace RedditPodcastPoster.PodcastServices.Apple.Providers;

/// <inheritdoc cref="IAppleShowEpisodePages" />
public sealed class AppleShowEpisodePages(IApplePodcastService applePodcastService)
    : IAppleShowEpisodePages, IPodcastPassApiCacheSource
{
    private readonly ConcurrentDictionary<long, List<AppleEpisodePage>> _pages = new();

    public async Task<CatalogueShowPage<AppleEpisode>?> GetNewestPage(
        long podcastId, int pageIndex, CancellationToken cancellationToken = default)
    {
        if (pageIndex < 0)
        {
            return null;
        }

        var pages = _pages.GetOrAdd(podcastId, _ => []);
        while (pages.Count <= pageIndex)
        {
            string? next = null;
            if (pages.Count > 0)
            {
                next = pages[^1].Next;
                if (next == null)
                {
                    return null;
                }
            }

            var page = await applePodcastService.GetEpisodePage(new ApplePodcastId(podcastId), next, cancellationToken);
            if (page == null)
            {
                return null;
            }

            pages.Add(page);
        }

        var newestFirst = AppleCataloguePagination.IsNewestFirst(pages[0].Episodes.Select(x => x.Release).ToList());
        var current = pages[pageIndex];
        return new CatalogueShowPage<AppleEpisode>(current.Episodes, current.Next != null, null, newestFirst);
    }

    public void ClearPassCache() => _pages.Clear();
}
