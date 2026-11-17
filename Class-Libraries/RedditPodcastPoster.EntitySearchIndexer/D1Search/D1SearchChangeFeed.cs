using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.EntitySearchIndexer.D1Search;

public interface ID1SearchChangeFeed
{
    /// <summary>
    /// Mirrors every playable whose Cosmos <c>_ts</c> is at or after <paramref name="since"/> into D1: eligible ones
    /// are upserted, excluded ones deleted. Never throws for a D1 or Cosmos failure (logged, returned).
    /// </summary>
    Task<D1SearchChangeFeedResult> Sync(DateTimeOffset since, CancellationToken cancellationToken);

    /// <summary>
    /// The hourly drip: <see cref="Sync"/> from now minus <see cref="D1SearchOptions.DripLookbackMinutes"/>.
    /// A no-op when no target is active.
    /// </summary>
    Task<D1SearchChangeFeedResult> Drip(CancellationToken cancellationToken);
}

/// <summary>
/// The D1 counterpart of Azure's Cosmos <c>_ts</c> pull indexers (SP-2.1, epic #1312). Azure gets episodes, TV
/// episodes and news reports from its hourly pull and films from the FilmSearchPusher, and the Indexer function's
/// new episodes reach search <b>only</b> that way, so a push-site mirror alone would miss most of the corpus. This
/// reads the four playable containers by <c>_ts</c> and writes D1 directly; it runs from the Publisher activity next
/// to <c>SearchIndexerService.RunIndexer</c>.
/// <list type="bullet">
/// <item><b>Stateless:</b> a fixed look-back (default 3 h) instead of a stored watermark, so there is no LookUps
/// write; the overlap is free because the Worker's <c>modifiedAt</c> guard turns a repeat into a no-op retry.</item>
/// <item><b>Better than the Azure pull on removals:</b> a playable removed in the window is deleted from D1 (the
/// Azure pull only filters it out and relies on the push path).</item>
/// <item><b>Not covered:</b> hard-deleted Cosmos documents leave no <c>_ts</c>; the weekly reconcile (SP-2.2 #1322)
/// removes those orphans, and the push-site mirror deletes the ones Api requests delete.</item>
/// <item><b>Streams:</b> documents are flushed every <see cref="D1SearchIndexWriter.MaxUpsertsPerRequest"/>, so a
/// very long look-back (the SP-2.2 backfill) never holds the corpus in memory.</item>
/// </list>
/// </summary>
public sealed class D1SearchChangeFeed(
    IEpisodeRepository episodeRepository,
    IPodcastRepository podcastRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    INewsReportRepository newsReportRepository,
    IFilmRepository filmRepository,
    ID1SearchIndexWriter writer,
    IOptions<D1SearchOptions> options,
    TimeProvider timeProvider,
    ILogger<D1SearchChangeFeed> logger) : ID1SearchChangeFeed
{
    private readonly D1SearchOptions _options = options.Value;

    public Task<D1SearchChangeFeedResult> Drip(CancellationToken cancellationToken)
    {
        if (!writer.IsActive)
        {
            return Task.FromResult(D1SearchChangeFeedResult.Inactive);
        }

        var lookback = TimeSpan.FromMinutes(Math.Max(1, _options.DripLookbackMinutes));
        return Sync(timeProvider.GetUtcNow() - lookback, cancellationToken);
    }

    public async Task<D1SearchChangeFeedResult> Sync(DateTimeOffset since, CancellationToken cancellationToken)
    {
        if (!writer.IsActive)
        {
            return D1SearchChangeFeedResult.Inactive;
        }

        var sinceTs = since.ToUnixTimeSeconds();
        var batch = new Batch(writer, cancellationToken);
        var failedToBuild = 0;
        try
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

                if (podcast is null || D1SearchEligibility.ShouldExclude(podcast, episode))
                {
                    // An episode without its podcast cannot be indexed (Azure's push path refuses it too).
                    await batch.Delete(episode.Id.ToString());
                    continue;
                }

                failedToBuild += await batch.Upsert(episode.Id, () => D1SearchDocuments.For(podcast, episode), logger);
            }

            await foreach (var episode in tvShowEpisodeRepository.GetAllBy(e => e.Timestamp >= sinceTs)
                               .WithCancellation(cancellationToken))
            {
                if (D1SearchEligibility.ShouldExclude(episode))
                {
                    await batch.Delete(episode.Id.ToString());
                    continue;
                }

                failedToBuild += await batch.Upsert(episode.Id, () => D1SearchDocuments.For(episode), logger);
            }

            await foreach (var report in newsReportRepository.GetAllBy(r => r.Timestamp >= sinceTs)
                               .WithCancellation(cancellationToken))
            {
                if (D1SearchEligibility.ShouldExclude(report))
                {
                    await batch.Delete(report.Id.ToString());
                    continue;
                }

                failedToBuild += await batch.Upsert(report.Id, () => D1SearchDocuments.For(report), logger);
            }

            await foreach (var film in filmRepository.GetAllBy(f => f.Timestamp >= sinceTs)
                               .WithCancellation(cancellationToken))
            {
                if (D1SearchEligibility.ShouldExclude(film))
                {
                    await batch.Delete(film.Id.ToString());
                    continue;
                }

                failedToBuild += await batch.Upsert(film.Id, () => D1SearchDocuments.For(film), logger);
            }

            await batch.Flush();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex,
                "D1 search change feed since {Since:O} stopped reading Cosmos after {Upserts} upsert(s) and {Deletes} delete(s); " +
                "the next drip repeats it. Azure indexing is unaffected.",
                since, batch.Upserts, batch.Deletes);
            return new D1SearchChangeFeedResult(true, false, batch.Upserts, batch.Deletes, failedToBuild, batch.WritesFailed);
        }

        var result = new D1SearchChangeFeedResult(true, !batch.WritesFailed && failedToBuild == 0,
            batch.Upserts, batch.Deletes, failedToBuild, batch.WritesFailed);
        logger.LogInformation(
            "D1 search change feed since {Since:O}: {Upserts} upsert(s), {Deletes} delete(s), {FailedToBuild} not built, " +
            "write failures {WritesFailed}.",
            since, result.Upserts, result.Deletes, result.FailedToBuild, result.WritesFailed);
        return result;
    }

    /// <summary>Buffers documents and deletes and flushes them in request-sized chunks.</summary>
    private sealed class Batch(ID1SearchIndexWriter writer, CancellationToken cancellationToken)
    {
        private readonly List<D1SearchDocument> _upserts = [];
        private readonly List<string> _deletes = [];

        // A playable that changed kind keeps its id (RPP #1428): the old kind's removed document and the new kind's
        // live one can both be in the window. The upsert must win whichever container is read first.
        private readonly HashSet<string> _upserted = new(StringComparer.Ordinal);
        public int Upserts { get; private set; }
        public int Deletes { get; private set; }
        public bool WritesFailed { get; private set; }

        public async Task<int> Upsert(Guid id, Func<D1SearchDocument> build, ILogger logger)
        {
            D1SearchDocument document;
            try
            {
                document = build();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "D1 search change feed could not build the document for playable '{Id}'.", id);
                return 1;
            }

            _upserts.Add(document);
            _upserted.Add(document.Id);
            _deletes.Remove(document.Id);
            Upserts++;
            if (_upserts.Count >= D1SearchIndexWriter.MaxUpsertsPerRequest)
            {
                await Flush();
            }

            return 0;
        }

        public async Task Delete(string id)
        {
            if (_upserted.Contains(id))
            {
                return;
            }

            _deletes.Add(id);
            Deletes++;
            if (_deletes.Count >= D1SearchIndexWriter.MaxDeletesPerRequest)
            {
                await Flush();
            }
        }

        public async Task Flush()
        {
            if (_upserts.Count == 0 && _deletes.Count == 0)
            {
                return;
            }

            var result = await writer.Write(_upserts.ToArray(), _deletes.ToArray(), cancellationToken);
            WritesFailed |= !result.Succeeded;
            _upserts.Clear();
            _deletes.Clear();
        }
    }
}

/// <param name="Ran">At least one D1 target was active.</param>
/// <param name="Succeeded">Every document was built and every write succeeded.</param>
/// <param name="FailedToBuild">Playables whose document could not be built (logged with their id).</param>
public sealed record D1SearchChangeFeedResult(
    bool Ran,
    bool Succeeded,
    int Upserts,
    int Deletes,
    int FailedToBuild,
    bool WritesFailed)
{
    public static D1SearchChangeFeedResult Inactive { get; } = new(false, true, 0, 0, 0, false);
}
