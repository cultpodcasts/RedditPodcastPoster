using System.Text.Json;
using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.Episodes;

/// <summary>
/// Moves selected podcast episodes to a news organisation ("Move to news", #1388), as <see cref="NewsReport"/>s with the
/// same ids. Unlike the podcast kind transfer, only the named episodes move: the podcast row, its other episodes, its
/// Spotify / Apple / YouTube show ids and its file key are never changed, and the podcast is never deleted, even when
/// it is left with no episodes.
/// <para>
/// The target is an existing news organisation (by id, or by name among news organisations only, so a podcast and a
/// news organisation may share a name) or, with <c>createOrganisation</c>, a new one by name. A new organisation gets
/// a fresh id (never the podcast id), a new <c>news-</c> file key, the request's publisher (else none), and the news
/// creation defaults (bypass short-episode checking, always promote as hero) with no YouTube channel or playlist, so
/// it claims nothing a channel-scoped podcast holds.
/// </para>
/// Order, so a failure never loses an episode:
/// <list type="number">
/// <item>Check every requested id before any write: an id that is not on the podcast (and not already moved to this
/// organisation), or that is already a TV episode, a film or a news report of another organisation, refuses the whole
/// request and nothing is written.</item>
/// <item>Save a created organisation, then the news reports (<see cref="CatalogueParentKindMapper.ToNewsReport"/>:
/// services, subjects, posted / tweeted / Bluesky state, ignored and removed flags).</item>
/// <item>Swap each search document under the same id to ContentKind NewsReport (removed reports are deleted from the
/// index instead). On failure the source episodes are kept.</item>
/// <item>Re-read the organisation and the reports; a concurrent delete keeps the source episodes. Episodes edited during
/// the move are remapped first.</item>
/// <item>Delete only the moved episode rows.</item>
/// </list>
/// Re-sending resumes: a report already in the target organisation while its episode row exists is rewritten from the
/// episode; with the episode row gone, the id is reported as already moved.
/// </summary>
public class EpisodeNewsMoveService(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    INewsReportRepository newsReportRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    IFilmRepository filmRepository,
    IPlayableSearchDocumentSwap searchSwap,
    IFileKeyAllocator fileKeyAllocator,
    ILogger<EpisodeNewsMoveService> logger) : IEpisodeNewsMoveService
{
    /// <summary>Most episode ids one batch request may move (one HTTP request, well inside the host timeout).</summary>
    public const int MaxEpisodesPerRequest = 500;

    /// <summary>Ids per Cosmos <c>IN</c> lookup when checking for existing playables with the same id.</summary>
    private const int LookupChunkSize = 100;

    public async Task<EpisodeNewsMoveResult> MoveAsync(
        EpisodeNewsMoveCommand command,
        CancellationToken cancellationToken)
    {
        var result = await Move(command.PodcastId, [command.EpisodeId], command.Request, cancellationToken);
        return result with { EpisodeId = command.EpisodeId };
    }

    public Task<EpisodeNewsMoveResult> MoveManyAsync(
        PodcastNewsMoveCommand command,
        CancellationToken cancellationToken)
    {
        if (command.EpisodeIds is null || command.EpisodeIds.Count == 0)
        {
            return Task.FromResult(BadRequest("Give at least one episode id in episodeIds"));
        }

        if (command.EpisodeIds.Any(id => id == Guid.Empty))
        {
            return Task.FromResult(BadRequest("episodeIds cannot contain an empty id"));
        }

        var ids = command.EpisodeIds.Distinct().ToList();
        if (ids.Count > MaxEpisodesPerRequest)
        {
            return Task.FromResult(BadRequest(
                $"At most {MaxEpisodesPerRequest} episode ids per request. Send the rest in another request"));
        }

        return Move(command.PodcastId, ids, command.Request, cancellationToken);
    }

    private async Task<EpisodeNewsMoveResult> Move(
        Guid podcastId,
        IReadOnlyList<Guid> episodeIds,
        EpisodeNewsMoveRequest request,
        CancellationToken cancellationToken)
    {
        if (Validate(request) is { } invalid)
        {
            return invalid;
        }

        NewsOrganisation? organisation = null;
        var created = false;
        // A created organisation only exists once saved; until then a failure reports neither its id nor created.
        var organisationSaved = false;
        try
        {
            var podcast = await podcastRepository.GetPodcast(podcastId);
            if (podcast is null)
            {
                return NotFound("Podcast not found");
            }

            var resolved = await ResolveOrganisation(request, cancellationToken);
            if (resolved.Stop is not null)
            {
                return resolved.Stop;
            }

            organisation = resolved.Organisation!;
            created = resolved.Created;
            organisationSaved = !created;

            var plan = await Plan(podcast, episodeIds, organisation, cancellationToken);
            if (plan.NotFound.Count > 0 || plan.Conflicts.Count > 0)
            {
                logger.LogWarning(
                    "{method}: Refused moving {requested} episode(s) of podcast '{podcastId}' to news: {notFound} not on the podcast, {conflicts} already another playable. Nothing written.",
                    nameof(Move), episodeIds.Count, podcastId, plan.NotFound.Count, plan.Conflicts.Count);
                return new EpisodeNewsMoveResult(
                    plan.NotFound.Count > 0 ? EpisodeNewsMoveStatus.NotFound : EpisodeNewsMoveStatus.Conflict,
                    created ? null : organisation.Id,
                    organisation.Name,
                    CreatedOrganisation: false,
                    MovedEpisodeIds: [],
                    AlreadyMovedEpisodeIds: plan.AlreadyMoved,
                    NotFoundEpisodeIds: plan.NotFound,
                    ConflictEpisodeIds: plan.Conflicts,
                    Error: plan.NotFound.Count > 0
                        ? "Some episode ids are not episodes of this podcast. Nothing was moved"
                        : "Some episode ids are already a TV episode, a film or a news report of another organisation. Nothing was moved");
            }

            if (plan.ToMove.Count == 0)
            {
                logger.LogInformation(
                    "{method}: All {count} episode(s) were already moved to news organisation '{organisationId}'; accepting the re-send.",
                    nameof(Move), plan.AlreadyMoved.Count, organisation.Id);
                return Accepted(organisation, created, [], plan.AlreadyMoved);
            }

            var fingerprints = plan.ToMove.ToDictionary(episode => episode.Id, Fingerprint);
            var reports = MapReports(podcast, plan.ToMove, organisation);
            if (created)
            {
                organisation.LatestReleased = reports.Max(report => report.ReleaseSort);
                await CatalogueBulkWrite.RetryThrottled(() => newsOrganisationRepository.Save(organisation));
                organisationSaved = true;
                logger.LogInformation(
                    "{method}: Created news organisation '{name}' ('{organisationId}', file key '{fileKey}') for {count} episode(s) of podcast '{podcastId}'.",
                    nameof(Move), organisation.Name, organisation.Id, organisation.FileKey, reports.Count, podcast.Id);
            }

            await SaveReports(reports);
            if (!await SwapSearch(organisation, reports, cancellationToken))
            {
                logger.LogError(
                    "{method}: Search swap failed moving {count} episode(s) of podcast '{podcastId}' to news organisation '{organisationId}'. Episode rows kept; re-send to resume.",
                    nameof(Move), reports.Count, podcast.Id, organisation.Id);
                return Failed(organisation, created, plan.AlreadyMoved, indexing: true);
            }

            // Episodes the Indexer or a curator changed while the reports were written are remapped before their
            // source rows go, so that edit is not lost.
            var current = await ReadEpisodes(podcast.Id, plan.ToMove.Select(episode => episode.Id).ToHashSet(),
                cancellationToken);
            var changed = current
                .Where(episode => fingerprints[episode.Id] != Fingerprint(episode))
                .ToList();
            if (changed.Count > 0)
            {
                logger.LogInformation(
                    "{method}: {count} episode(s) of podcast '{podcastId}' changed during the move; remapping before delete.",
                    nameof(Move), changed.Count, podcast.Id);
                var remapped = MapReports(podcast, changed, organisation);
                await SaveReports(remapped);
                if (!await SwapSearch(organisation, remapped, cancellationToken))
                {
                    return Failed(organisation, created, plan.AlreadyMoved, indexing: true);
                }
            }

            // Last check before the only irreversible step: a concurrent delete of the organisation or of a report
            // would leave the episode with nowhere to live once its row is gone.
            if (await newsOrganisationRepository.GetNewsOrganisation(organisation.Id) is not { } currentOrganisation)
            {
                logger.LogError(
                    "{method}: News organisation '{organisationId}' is gone (changed concurrently). Episode rows kept; re-send to resume.",
                    nameof(Move), organisation.Id);
                return Failed(organisation, created, plan.AlreadyMoved, indexing: true);
            }

            var deletable = new List<Episode>();
            foreach (var episode in current)
            {
                if (await newsReportRepository.GetNewsReport(organisation.Id, episode.Id) is null)
                {
                    logger.LogError(
                        "{method}: News report '{episodeId}' is gone (changed concurrently). Episode row kept; re-send to resume.",
                        nameof(Move), episode.Id);
                    return Failed(organisation, created, plan.AlreadyMoved, indexing: true);
                }

                deletable.Add(episode);
            }

            foreach (var chunk in deletable.Chunk(LookupChunkSize))
            {
                await episodeRepository.DeleteMany(podcast.Id, chunk);
            }

            logger.LogInformation(
                "Moved {count} episode(s) from podcast '{podcastId}' to news organisation '{name}' ('{organisationId}', created {created}). The podcast is kept.",
                deletable.Count, podcast.Id, currentOrganisation.Name, currentOrganisation.Id, created);
            return Accepted(currentOrganisation, created, deletable.Select(episode => episode.Id).ToList(),
                plan.AlreadyMoved);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{method}: Failed moving {count} episode(s) of podcast '{podcastId}' to news. Episode rows kept; re-send to resume.",
                nameof(Move), episodeIds.Count, podcastId);
            return organisation is null || !organisationSaved
                ? new EpisodeNewsMoveResult(EpisodeNewsMoveStatus.Failed,
                    Error: "Unable to move to news. The source episodes were kept; re-send to resume")
                : Failed(organisation, created, [], indexing: false);
        }
    }

    private sealed record MovePlan(
        IReadOnlyList<Episode> ToMove,
        IReadOnlyList<Guid> AlreadyMoved,
        IReadOnlyList<Guid> NotFound,
        IReadOnlyList<Guid> Conflicts);

    /// <summary>Classifies every requested id. Writes nothing.</summary>
    private async Task<MovePlan> Plan(
        Podcast podcast,
        IReadOnlyList<Guid> episodeIds,
        NewsOrganisation organisation,
        CancellationToken cancellationToken)
    {
        var requested = episodeIds.ToHashSet();
        var episodes = (await ReadEpisodes(podcast.Id, requested, cancellationToken))
            .ToDictionary(episode => episode.Id);
        var reports = new Dictionary<Guid, NewsReport>();
        var otherPlayables = new HashSet<Guid>();
        foreach (var chunk in episodeIds.Chunk(LookupChunkSize))
        {
            var ids = chunk.ToList();
            await foreach (var report in newsReportRepository.GetAllBy(item => ids.Contains(item.Id))
                               .WithCancellation(cancellationToken))
            {
                reports[report.Id] = report;
            }

            await foreach (var tvEpisode in tvShowEpisodeRepository.GetAllBy(item => ids.Contains(item.Id))
                               .WithCancellation(cancellationToken))
            {
                otherPlayables.Add(tvEpisode.Id);
            }

            await foreach (var film in filmRepository.GetAllBy(item => ids.Contains(item.Id))
                               .WithCancellation(cancellationToken))
            {
                otherPlayables.Add(film.Id);
            }
        }

        var toMove = new List<Episode>();
        var alreadyMoved = new List<Guid>();
        var notFound = new List<Guid>();
        var conflicts = new List<Guid>();
        foreach (var id in episodeIds)
        {
            var inTarget = reports.TryGetValue(id, out var report) && report.NewsOrganisationId == organisation.Id;
            if (otherPlayables.Contains(id) || (report is not null && !inTarget))
            {
                conflicts.Add(id);
            }
            else if (episodes.TryGetValue(id, out var episode))
            {
                toMove.Add(episode);
            }
            else if (inTarget)
            {
                alreadyMoved.Add(id);
            }
            else
            {
                notFound.Add(id);
            }
        }

        return new MovePlan(toMove, alreadyMoved, notFound, conflicts);
    }

    private async Task<List<Episode>> ReadEpisodes(
        Guid podcastId,
        IReadOnlySet<Guid> ids,
        CancellationToken cancellationToken)
    {
        var episodes = new List<Episode>();
        await foreach (var episode in episodeRepository.GetByPodcastId(podcastId).WithCancellation(cancellationToken))
        {
            if (ids.Contains(episode.Id))
            {
                episodes.Add(episode);
            }
        }

        return episodes;
    }

    /// <summary>
    /// Reports keep Services only: missing <c>services.*.url</c> are filled from the episode's nested ids
    /// (deterministic, no platform calls, as the kind transfer does) before mapping. Flags are copied as they are.
    /// </summary>
    private static List<NewsReport> MapReports(
        Podcast podcast,
        IEnumerable<Episode> episodes,
        NewsOrganisation organisation) =>
        episodes
            .Select(episode =>
            {
                EpisodeServicePresence.EnsureServiceUrlsFromIds(episode, podcast.AppleId);
                return CatalogueParentKindMapper.ToNewsReport(episode, organisation);
            })
            .ToList();

    private async Task SaveReports(IReadOnlyList<NewsReport> reports) =>
        await Parallel.ForEachAsync(
            reports,
            new ParallelOptions { MaxDegreeOfParallelism = CatalogueBulkWrite.Parallelism },
            async (report, _) => await CatalogueBulkWrite.RetryThrottled(() => newsReportRepository.Save(report)));

    /// <summary>
    /// Same document shape as the news report edit and the news-report pull indexer, so the next pull run does not
    /// rewrite it. A removed report is dropped from the index instead of being rewritten as the new kind.
    /// </summary>
    private async Task<bool> SwapSearch(
        NewsOrganisation organisation,
        IReadOnlyList<NewsReport> reports,
        CancellationToken cancellationToken)
    {
        var excluded = reports
            .Where(report => CataloguePlayableSearchIndexEligibility.ShouldExcludeFromSearch(organisation, report))
            .Select(report => report.Id)
            .ToList();
        var documents = reports
            .Where(report => !excluded.Contains(report.Id))
            .Select(CataloguePlayableSearchDocument.For)
            .ToList();
        var uploaded = documents.Count == 0 || await searchSwap.UploadAsync(documents, cancellationToken);
        var deleted = excluded.Count == 0 || await searchSwap.DeleteAsync(excluded, cancellationToken);
        return uploaded && deleted;
    }

    private static string Fingerprint(Episode episode) => JsonSerializer.Serialize(episode);

    private static EpisodeNewsMoveResult? Validate(EpisodeNewsMoveRequest request)
    {
        var hasId = request.NewsOrganisationId is { } id && id != Guid.Empty;
        var hasName = !string.IsNullOrWhiteSpace(request.NewsOrganisationName);
        if (hasId == hasName)
        {
            return BadRequest("Give exactly one of newsOrganisationId or newsOrganisationName");
        }

        if (request.CreateOrganisation && !hasName)
        {
            return BadRequest("createOrganisation needs newsOrganisationName");
        }

        if (!string.IsNullOrWhiteSpace(request.Publisher) && !request.CreateOrganisation)
        {
            return BadRequest(
                "publisher only applies to a new news organisation (createOrganisation). Edit the organisation to change its publisher");
        }

        if (hasName && request.NewsOrganisationName!.Contains('/'))
        {
            return BadRequest("News organisation name cannot contain '/'");
        }

        return null;
    }

    /// <summary>
    /// Finds the target organisation, or builds (does not save) a new one. A name is matched among news organisations
    /// only, so a podcast with the same name is not a clash.
    /// </summary>
    private async Task<(NewsOrganisation? Organisation, bool Created, EpisodeNewsMoveResult? Stop)> ResolveOrganisation(
        EpisodeNewsMoveRequest request,
        CancellationToken cancellationToken)
    {
        if (request.NewsOrganisationId is { } id && id != Guid.Empty)
        {
            var byId = await newsOrganisationRepository.GetNewsOrganisation(id);
            if (byId is null)
            {
                return (null, false, NotFound("News organisation not found"));
            }

            return byId.Removed == true
                ? (null, false, BadRequest("A removed news organisation cannot take reports"))
                : (byId, false, null);
        }

        var name = request.NewsOrganisationName!.Trim();
        var matches = await PublisherNameAttachLookup.FindByName(newsOrganisationRepository, name, cancellationToken);
        if (matches.Count > 1)
        {
            logger.LogError("{method}: {count} news organisations are named '{name}'.",
                nameof(ResolveOrganisation), matches.Count, name);
            return (null, false, new EpisodeNewsMoveResult(
                EpisodeNewsMoveStatus.Conflict,
                Error: "Several news organisations have this name. Pick the organisation by id"));
        }

        if (matches.Count == 1)
        {
            var match = matches[0];
            if (request.CreateOrganisation)
            {
                logger.LogInformation(
                    "{method}: News organisation '{name}' already exists ('{organisationId}'); the episodes join it and the publisher is not applied.",
                    nameof(ResolveOrganisation), match.Name, match.Id);
            }

            return match.Removed == true
                ? (null, false, BadRequest("A removed news organisation cannot take reports"))
                : (match, false, null);
        }

        if (!request.CreateOrganisation)
        {
            return (null, false, NotFound("No news organisation has this name"));
        }

        var organisationId = Guid.NewGuid();
        var organisation = new NewsOrganisation(name)
        {
            Id = organisationId,
            FileKey = await fileKeyAllocator.Allocate(FileKeyKind.NewsOrganisation, name, organisationId),
            PublisherName = request.Publisher?.Trim() ?? string.Empty,
            // Same saved defaults as a kind-transferred or submit-created news organisation (bypass short, always
            // promote as hero), and no YouTube channel / playlist: the podcast keeps those.
            Indexing = CatalogueIndexingSource.ApplyNewsOrganisationCreationDefaults(null)
        };
        return (organisation, true, null);
    }

    private static EpisodeNewsMoveResult Accepted(
        NewsOrganisation organisation,
        bool created,
        IReadOnlyList<Guid> moved,
        IReadOnlyList<Guid> alreadyMoved) =>
        new(EpisodeNewsMoveStatus.Accepted, organisation.Id, organisation.Name.Trim(), created, moved, alreadyMoved,
            [], []);

    private static EpisodeNewsMoveResult Failed(
        NewsOrganisation organisation,
        bool created,
        IReadOnlyList<Guid> alreadyMoved,
        bool indexing) =>
        new(EpisodeNewsMoveStatus.Failed, organisation.Id, organisation.Name.Trim(), created, [], alreadyMoved, [], [],
            Error: "The move did not finish. The source episodes were kept; re-send to resume",
            FailureIndexing: indexing);

    private static EpisodeNewsMoveResult NotFound(string error) =>
        new(EpisodeNewsMoveStatus.NotFound, Error: error);

    private static EpisodeNewsMoveResult BadRequest(string error) =>
        new(EpisodeNewsMoveStatus.BadRequest, Error: error);
}
