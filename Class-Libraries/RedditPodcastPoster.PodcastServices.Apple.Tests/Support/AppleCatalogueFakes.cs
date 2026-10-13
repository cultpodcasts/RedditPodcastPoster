using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Providers;
using RedditPodcastPoster.PodcastServices.Apple.Resolvers;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests.Support;

/// <summary>
/// In-memory amp-api: serves the given episodes newest-first in pages of <paramref name="pageSize"/>, and counts calls.
/// Implements both the raw and the cached service so the real <see cref="AppleShowEpisodePages"/> sits on top.
/// </summary>
public sealed class FakeApplePodcastService(
    IEnumerable<AppleEpisode> episodes,
    int pageSize = 20,
    Action<CancellationToken>? onPage = null)
    : IApplePodcastService, ICachedApplePodcastService
{
    private readonly List<AppleEpisode> _episodes = episodes.OrderByDescending(x => x.Release).ToList();

    public int PageCalls { get; private set; }
    public int GetEpisodeCalls { get; private set; }

    public Task<AppleEpisodePage?> GetEpisodePage(ApplePodcastId podcastId, string? next, CancellationToken cancellationToken = default)
    {
        PageCalls++;
        onPage?.Invoke(cancellationToken);
        var index = next == null ? 0 : int.Parse(next["page:".Length..]);
        var items = _episodes.Skip(index * pageSize).Take(pageSize).ToList();
        var hasMore = (index + 1) * pageSize < _episodes.Count;
        return Task.FromResult<AppleEpisodePage?>(new AppleEpisodePage(items, hasMore ? $"page:{index + 1}" : null));
    }

    public Task<AppleEpisode?> GetEpisode(long episodeId, IndexingContext indexingContext, CancellationToken cancellationToken = default)
    {
        GetEpisodeCalls++;
        return Task.FromResult(_episodes.FirstOrDefault(x => x.Id == episodeId));
    }

    public Task<AppleEpisode?> GetEpisode(ApplePodcastId podcastId, long episodeId, IndexingContext indexingContext, CancellationToken cancellationToken = default) =>
        GetEpisode(episodeId, indexingContext, cancellationToken);

    public Task<AppleEpisode?> SingleUseGetEpisode(ApplePodcastId podcastId, long episodeId, IndexingContext indexingContext) =>
        GetEpisode(episodeId, indexingContext);

    public Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext, CancellationToken cancellationToken = default) =>
        Task.FromResult<IEnumerable<AppleEpisode>?>(_episodes);
}

/// <summary>In-memory iTunes lookup/search with call counters.</summary>
public sealed class FakeAppleCatalogueLookup(
    int? episodeCount = null,
    IReadOnlyList<AppleEpisode>? searchHits = null,
    Action<CancellationToken>? onCall = null)
    : IAppleCatalogueLookup
{
    public int CountCalls { get; private set; }
    public int SearchCalls { get; private set; }

    public Task<int?> GetEpisodeCount(long podcastId, CancellationToken cancellationToken = default)
    {
        CountCalls++;
        onCall?.Invoke(cancellationToken);
        return Task.FromResult(episodeCount);
    }

    public Task<IReadOnlyList<AppleEpisode>> SearchShowEpisodes(long podcastId, string title, CancellationToken cancellationToken = default)
    {
        SearchCalls++;
        onCall?.Invoke(cancellationToken);
        return Task.FromResult(searchHits ?? []);
    }
}

/// <summary>Real Apple resolver, pages and shared executor over the fakes.</summary>
public sealed record AppleResolverHarness(
    AppleEpisodeResolver Resolver,
    FakeApplePodcastService Service,
    FakeAppleCatalogueLookup Lookup)
{
    public static AppleResolverHarness Create(
        IEnumerable<AppleEpisode> episodes,
        ICatalogueSubjectClassifier? subjectClassifier = null,
        IEpisodePlatformMatcher? platformMatcher = null,
        int? iTunesEpisodeCount = null,
        IReadOnlyList<AppleEpisode>? searchHits = null,
        TimeProvider? timeProvider = null,
        int pageSize = 20,
        ICatalogueShowEpisodeCounts? showEpisodeCounts = null,
        Action<CancellationToken>? onRequest = null)
    {
        var service = new FakeApplePodcastService(episodes, pageSize, onRequest);
        var lookup = new FakeAppleCatalogueLookup(iTunesEpisodeCount, searchHits, onRequest);
        var resolver = new AppleEpisodeResolver(
            service,
            new AppleShowEpisodePages(service),
            lookup,
            CatalogueTestServices.CreateExecutor(subjectClassifier, timeProvider, platformMatcher: platformMatcher,
                showEpisodeCounts: showEpisodeCounts),
            NullLogger<AppleEpisodeResolver>.Instance);
        return new AppleResolverHarness(resolver, service, lookup);
    }
}
