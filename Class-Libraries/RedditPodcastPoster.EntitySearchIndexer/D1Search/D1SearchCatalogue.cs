using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.EntitySearchIndexer.D1Search;

/// <summary>
/// One playable read from Cosmos for the D1 search index: its document when it is eligible, null when it is excluded
/// (removed, ineligible, or an episode without its podcast). When <see cref="BuildFailed"/> is set, the document
/// couldn't be built (logged with the id). Its eligibility is unknown, so callers neither write nor delete it.
/// <see cref="TimestampSeconds"/> is the source Cosmos <c>_ts</c> used for the reconcile watermark (for a podcast
/// episode, the later of the episode's and the podcast's <c>_ts</c>). It is <b>not</b> the document's
/// <c>modifiedAt</c>, which stays the episode's own <c>_ts</c> (#1475).
/// <see cref="ParentChanged"/> is set only for an episode re-emitted because its podcast changed, when the podcast's
/// indexed fields no longer match the episode's projection (<see cref="D1SearchEpisodeParentProjection"/>). Its
/// document must be re-sent even though its <c>modifiedAt</c> equals the stored one.
/// </summary>
public sealed record D1SearchCandidate(
    string Id,
    D1SearchDocument? Document,
    bool BuildFailed = false,
    long TimestampSeconds = 0,
    bool ParentChanged = false)
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

    /// <summary>
    /// <see cref="Read"/> plus every episode of a podcast whose <c>_ts</c> is at or after <paramref name="sinceTs"/>,
    /// even when the episode itself is older, because the episode search document denormalises the live podcast. Such
    /// an episode is marked <see cref="D1SearchCandidate.ParentChanged"/> only when the podcast's indexed fields differ
    /// from the episode's projection; a podcast saved for bookkeeping leaves its episodes unchanged (#1475).
    /// When <paramref name="sinceTs"/> is 0 this is the
    /// same as <see cref="Read"/> (every podcast would otherwise match and every episode would be read twice).
    /// Ids already emitted by the playable <c>_ts</c> window are not yielded again.
    /// </summary>
    IAsyncEnumerable<D1SearchCandidate> ReadIncremental(long sinceTs, CancellationToken cancellationToken);

    /// <summary>
    /// The number of documents in the four playable containers, used as the catalogue-bar total on a full scan.
    /// Some of those documents may later be excluded; the bar counts candidates, not only eligible documents.
    /// </summary>
    Task<int> CountPlayables(CancellationToken cancellationToken);

    /// <summary>
    /// Every currently eligible playable id, from a cheap id-and-flag projection (no search documents). Used by
    /// incremental reconcile to find D1 orphans without a second full document build.
    /// </summary>
    IAsyncEnumerable<string> ReadEligibleIds(CancellationToken cancellationToken);
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
    public IAsyncEnumerable<D1SearchCandidate> Read(long sinceTs, CancellationToken cancellationToken) =>
        ReadCore(sinceTs, includeEpisodesOfChangedPodcasts: false, cancellationToken);

    public IAsyncEnumerable<D1SearchCandidate> ReadIncremental(long sinceTs, CancellationToken cancellationToken) =>
        ReadCore(sinceTs, includeEpisodesOfChangedPodcasts: sinceTs > 0, cancellationToken);

    public async Task<int> CountPlayables(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return checked(
            await episodeRepository.Count()
            + await tvShowEpisodeRepository.Count()
            + await newsReportRepository.Count()
            + await filmRepository.Count());
    }

    public async IAsyncEnumerable<string> ReadEligibleIds(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var podcasts = new Dictionary<Guid, bool?>();
        await foreach (var podcast in podcastRepository
                           .GetAllBy(_ => true, p => new D1SearchPodcastRemovedRow { Id = p.Id, Removed = p.Removed })
                           .WithCancellation(cancellationToken))
        {
            podcasts[podcast.Id] = podcast.Removed;
        }

        await foreach (var episode in episodeRepository
                           .GetAllBy(_ => true, e => new D1SearchEpisodeEligibilityRow
                           {
                               Id = e.Id,
                               PodcastId = e.PodcastId,
                               Removed = e.Removed,
                               ParentRemoved = e.ParentRemoved
                           })
                           .WithCancellation(cancellationToken))
        {
            if (podcasts.TryGetValue(episode.PodcastId, out var podcastRemoved)
                && !episode.Removed
                && episode.ParentRemoved != true
                && podcastRemoved != true)
            {
                yield return episode.Id.ToString();
            }
        }

        await foreach (var episode in tvShowEpisodeRepository
                           .GetAllBy(_ => true, e => new D1SearchPlayableEligibilityRow
                           {
                               Id = e.Id,
                               Removed = e.Removed,
                               ParentRemoved = e.ParentRemoved
                           })
                           .WithCancellation(cancellationToken))
        {
            if (episode.Removed != true && episode.ParentRemoved != true)
            {
                yield return episode.Id.ToString();
            }
        }

        await foreach (var report in newsReportRepository
                           .GetAllBy(_ => true, r => new D1SearchPlayableEligibilityRow
                           {
                               Id = r.Id,
                               Removed = r.Removed,
                               ParentRemoved = r.ParentRemoved
                           })
                           .WithCancellation(cancellationToken))
        {
            if (report.Removed != true && report.ParentRemoved != true)
            {
                yield return report.Id.ToString();
            }
        }

        await foreach (var film in filmRepository
                           .GetAllBy(_ => true, f => new D1SearchPlayableEligibilityRow
                           {
                               Id = f.Id,
                               Removed = f.Removed
                           })
                           .WithCancellation(cancellationToken))
        {
            if (film.Removed != true)
            {
                yield return film.Id.ToString();
            }
        }
    }

    private async IAsyncEnumerable<D1SearchCandidate> ReadCore(
        long sinceTs,
        bool includeEpisodesOfChangedPodcasts,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var podcasts = new Dictionary<Guid, Podcast?>();
        var yielded = new HashSet<string>(StringComparer.Ordinal);

        await foreach (var episode in episodeRepository.GetAllBy(e => e.Timestamp >= sinceTs)
                           .WithCancellation(cancellationToken))
        {
            yield return await CandidateForEpisode(episode, podcasts, yielded, cancellationToken);
        }

        if (includeEpisodesOfChangedPodcasts)
        {
            await foreach (var podcast in podcastRepository.GetAllBy(p => p.Timestamp >= sinceTs)
                               .WithCancellation(cancellationToken))
            {
                podcasts[podcast.Id] = podcast;
                await foreach (var episode in episodeRepository.GetByPodcastId(podcast.Id)
                                   .WithCancellation(cancellationToken))
                {
                    if (yielded.Contains(episode.Id.ToString()))
                    {
                        continue;
                    }

                    yield return await CandidateForEpisode(
                        episode, podcasts, yielded, cancellationToken, viaChangedPodcast: true);
                }
            }
        }

        await foreach (var episode in tvShowEpisodeRepository.GetAllBy(e => e.Timestamp >= sinceTs)
                           .WithCancellation(cancellationToken))
        {
            var id = episode.Id.ToString();
            yielded.Add(id);
            yield return D1SearchEligibility.ShouldExclude(episode)
                ? new D1SearchCandidate(id, null, TimestampSeconds: episode.Timestamp)
                : Build(episode.Id, () => D1SearchDocuments.For(episode), episode.Timestamp);
        }

        await foreach (var report in newsReportRepository.GetAllBy(r => r.Timestamp >= sinceTs)
                           .WithCancellation(cancellationToken))
        {
            var id = report.Id.ToString();
            yielded.Add(id);
            yield return D1SearchEligibility.ShouldExclude(report)
                ? new D1SearchCandidate(id, null, TimestampSeconds: report.Timestamp)
                : Build(report.Id, () => D1SearchDocuments.For(report), report.Timestamp);
        }

        await foreach (var film in filmRepository.GetAllBy(f => f.Timestamp >= sinceTs)
                           .WithCancellation(cancellationToken))
        {
            var id = film.Id.ToString();
            yielded.Add(id);
            yield return D1SearchEligibility.ShouldExclude(film)
                ? new D1SearchCandidate(id, null, TimestampSeconds: film.Timestamp)
                : Build(film.Id, () => D1SearchDocuments.For(film), film.Timestamp);
        }
    }

    private async Task<D1SearchCandidate> CandidateForEpisode(
        Episode episode,
        Dictionary<Guid, Podcast?> podcasts,
        HashSet<string> yielded,
        CancellationToken cancellationToken,
        bool viaChangedPodcast = false)
    {
        var id = episode.Id.ToString();
        yielded.Add(id);
        if (!podcasts.TryGetValue(episode.PodcastId, out var podcast))
        {
            cancellationToken.ThrowIfCancellationRequested();
            podcast = await podcastRepository.GetPodcast(episode.PodcastId);
            podcasts[episode.PodcastId] = podcast;
        }

        var timestamp = Math.Max(episode.Timestamp, podcast?.Timestamp ?? 0);
        // An episode without its podcast cannot be indexed (Azure's push path refuses it too).
        if (podcast is null || D1SearchEligibility.ShouldExclude(podcast, episode))
        {
            return new D1SearchCandidate(id, null, TimestampSeconds: timestamp);
        }

        var parentChanged = viaChangedPodcast && D1SearchEpisodeParentProjection.IsBehind(podcast, episode);
        return Build(episode.Id, () => D1SearchDocuments.For(podcast, episode), timestamp, parentChanged);
    }

    private D1SearchCandidate Build(
        Guid id, Func<D1SearchDocument> build, long timestampSeconds, bool parentChanged = false)
    {
        try
        {
            return new D1SearchCandidate(
                id.ToString(), build(), TimestampSeconds: timestampSeconds, ParentChanged: parentChanged);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "D1 search could not build the document for playable '{Id}'.", id);
            return new D1SearchCandidate(id.ToString(), null, BuildFailed: true, TimestampSeconds: timestampSeconds);
        }
    }
}

/// <summary>
/// Cosmos LINQ projection row. Must stay a class with a parameterless constructor and settable properties
/// (see <c>IFilterableRepository</c>).
/// </summary>
internal sealed class D1SearchPodcastRemovedRow
{
    public Guid Id { get; set; }
    public bool? Removed { get; set; }
}

internal sealed class D1SearchEpisodeEligibilityRow
{
    public Guid Id { get; set; }
    public Guid PodcastId { get; set; }
    public bool Removed { get; set; }
    public bool? ParentRemoved { get; set; }
}

internal sealed class D1SearchPlayableEligibilityRow
{
    public Guid Id { get; set; }
    public bool? Removed { get; set; }
    public bool? ParentRemoved { get; set; }
}
