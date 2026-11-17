using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.EntitySearchIndexer.D1Search;

/// <summary>
/// One playable read from Cosmos for the D1 search index: its document when it is eligible, null when it is excluded
/// (removed, ineligible, or an episode without its podcast). When <see cref="BuildFailed"/> is set, the document
/// couldn't be built (logged with the id). Its eligibility is unknown, so callers neither write nor delete it.
/// </summary>
public sealed record D1SearchCandidate(string Id, D1SearchDocument? Document, bool BuildFailed = false)
{
    public bool Excluded => Document is null && !BuildFailed;
}

public interface ID1SearchCatalogue
{
    /// <summary>
    /// Streams every episode, TV episode, news report and film whose Cosmos <c>_ts</c> is at or after
    /// <paramref name="sinceTs"/> (epoch seconds; 0 for the whole catalogue) as <see cref="D1SearchCandidate"/>s, built
    /// with the same eligibility and Azure document builders as the push path. Cosmos failures propagate.
    /// </summary>
    IAsyncEnumerable<D1SearchCandidate> Read(long sinceTs, CancellationToken cancellationToken);
}

/// <summary>
/// The four playable containers as D1 search candidates (SP-2.1 drip, SP-2.2 reconcile). A playable that changed kind
/// keeps its id (RPP #1428), so the same id can come back twice: once excluded (the old kind's removed document) and
/// once with a document. Callers let the document win.
/// </summary>
public sealed class D1SearchCatalogue(
    IEpisodeRepository episodeRepository,
    IPodcastRepository podcastRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    INewsReportRepository newsReportRepository,
    IFilmRepository filmRepository,
    ILogger<D1SearchCatalogue> logger) : ID1SearchCatalogue
{
    public async IAsyncEnumerable<D1SearchCandidate> Read(
        long sinceTs,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var podcasts = new Dictionary<Guid, Podcast?>();
        await foreach (var episode in episodeRepository.GetAllBy(e => e.Timestamp >= sinceTs)
                           .WithCancellation(cancellationToken))
        {
            if (!podcasts.TryGetValue(episode.PodcastId, out var podcast))
            {
                podcast = await podcastRepository.GetPodcast(episode.PodcastId);
                podcasts[episode.PodcastId] = podcast;
            }

            // An episode without its podcast cannot be indexed (Azure's push path refuses it too).
            yield return podcast is null || D1SearchEligibility.ShouldExclude(podcast, episode)
                ? new D1SearchCandidate(episode.Id.ToString(), null)
                : Build(episode.Id, () => D1SearchDocuments.For(podcast, episode));
        }

        await foreach (var episode in tvShowEpisodeRepository.GetAllBy(e => e.Timestamp >= sinceTs)
                           .WithCancellation(cancellationToken))
        {
            yield return D1SearchEligibility.ShouldExclude(episode)
                ? new D1SearchCandidate(episode.Id.ToString(), null)
                : Build(episode.Id, () => D1SearchDocuments.For(episode));
        }

        await foreach (var report in newsReportRepository.GetAllBy(r => r.Timestamp >= sinceTs)
                           .WithCancellation(cancellationToken))
        {
            yield return D1SearchEligibility.ShouldExclude(report)
                ? new D1SearchCandidate(report.Id.ToString(), null)
                : Build(report.Id, () => D1SearchDocuments.For(report));
        }

        await foreach (var film in filmRepository.GetAllBy(f => f.Timestamp >= sinceTs)
                           .WithCancellation(cancellationToken))
        {
            yield return D1SearchEligibility.ShouldExclude(film)
                ? new D1SearchCandidate(film.Id.ToString(), null)
                : Build(film.Id, () => D1SearchDocuments.For(film));
        }
    }

    private D1SearchCandidate Build(Guid id, Func<D1SearchDocument> build)
    {
        try
        {
            return new D1SearchCandidate(id.ToString(), build());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "D1 search could not build the document for playable '{Id}'.", id);
            return new D1SearchCandidate(id.ToString(), null, BuildFailed: true);
        }
    }
}
