using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace CatalogueMigrate;

public enum CatalogueMigrateApplyOutcome
{
    Completed,
    Conflict,
    Failed
}

/// <summary>
/// Moves one podcast, keeping ids (S-006): evidence first, then upsert destination,
/// verify, swap search, and only then delete the source episodes and the podcast.
/// Episodes are deleted before the podcast. A playable whose YouTube, Spotify, or Apple
/// service id the destination parent already holds is not written again. The search key
/// for that re-created episode id is deleted, including an Episode document the hourly
/// indexer already wrote, because the pull indexer has no deletion policy. The original
/// playable's search document stays. A re-run after a partial delete therefore cannot
/// leave a second playable or a second search document for that video.
/// </summary>
public class CatalogueMigrateApplier(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ICatalogueMigrateDestination destination,
    CatalogueMigrateRollbackPreviewer rollbackPreviewer,
    ILogger<CatalogueMigrateApplier> logger)
{
    public async Task<CatalogueMigrateApplyOutcome> Apply(
        CatalogueMigrateUnit unit,
        ICatalogueMigrateEvidenceBatch evidence,
        string runId,
        CancellationToken cancellationToken)
    {
        if (!unit.ApplyEligible)
        {
            throw new InvalidOperationException($"Unit {unit.Podcast.Id} is not apply-eligible.");
        }

        var podcast = unit.Podcast;
        var kind = unit.Kind;
        var parentId = unit.Plan.DestParentId ?? unit.Plan.DestPlayableIds.Single();
        var playableIds = unit.MoveEpisodes.Select(episode => episode.Id).ToArray();

        Task Record(string evt, string? detail = null, bool snapshot = false) =>
            evidence.Write(new CatalogueMigrateEvidenceRecord(
                evt,
                runId,
                kind,
                podcast.Id,
                unit.Plan.DestParentId,
                playableIds,
                DateTimeOffset.UtcNow,
                snapshot ? podcast : null,
                snapshot ? [.. unit.MoveEpisodes] : null,
                detail));

        await Record(CatalogueMigrateEvidenceEvent.Begin, snapshot: true);

        if (await HasSiblingParent(kind, podcast.Id))
        {
            await Record(CatalogueMigrateEvidenceEvent.Conflict, "Podcast id already has the other parent kind.");
            logger.LogError(
                "Catalogue migrate conflict {PodcastId} {Kind}: the id already has the other parent kind. Skipped.",
                podcast.Id,
                kind);
            return CatalogueMigrateApplyOutcome.Conflict;
        }

        try
        {
            // Read what the parent already holds before this write. A previous attempt may have stored every
            // playable, then failed while deleting source episodes; the indexer can save those missing videos
            // again as new episode ids. Those must not become a second playable or search document.
            var held = await destination.ListPlayables(kind, parentId, cancellationToken);
            var alreadyHeld = unit.MoveEpisodes
                .Where(episode => CatalogueMigrateHeldPlayables.AlreadyHeld(episode, held))
                .Select(episode => episode.Id)
                .ToHashSet();
            if (alreadyHeld.Count > 0)
            {
                logger.LogInformation(
                    "Catalogue migrate {PodcastId} {Kind}: skipped {SkippedCount} episode(s) already held by the destination on a YouTube, Spotify, or Apple service id.",
                    podcast.Id,
                    kind,
                    alreadyHeld.Count);
            }

            await WriteDestination(unit, alreadyHeld);
            await Record(
                CatalogueMigrateEvidenceEvent.DestWritten,
                alreadyHeld.Count == 0 ? null : $"skippedExistingServiceIds={alreadyHeld.Count}");

            var stored = await destination.CountPlayables(kind, parentId);
            var expected = kind == SubmitClassification.Film ? 1 : playableIds.Length;
            if (stored < expected)
            {
                return await Fail(Record, podcast.Id, kind, $"Verify failed: {stored} stored, {expected} expected.");
            }

            await Record(CatalogueMigrateEvidenceEvent.Verified, $"stored={stored}");

            var documents = CatalogueMigrateSearchDocuments.FromPodcastEpisodes(
                podcast,
                unit.SearchEpisodes.Where(episode => !alreadyHeld.Contains(episode.Id)),
                kind);
            if (!await destination.UploadSearch(documents, cancellationToken))
            {
                return await Fail(Record, podcast.Id, kind, "Search swap failed. Source rows kept; re-run resumes.");
            }

            // Keys this upload does not write may already be indexed: a removed episode (or one on a removed
            // podcast), and a re-created episode the parent already holds on a YouTube, Spotify, or Apple id.
            // Skipping the upload leaves the Episode document the hourly indexer wrote for the new id. The pull
            // indexer has no deletion policy, so delete those keys before the source row is gone. The original
            // playable id is not in this set (AlreadyHeld is false for the same id). Deleting a missing id succeeds.
            var searchDeleteIds = unit.MoveEpisodes
                .Where(episode =>
                    alreadyHeld.Contains(episode.Id) ||
                    EpisodeSearchIndexEligibility.ShouldExcludeFromSearch(podcast, episode))
                .Select(episode => episode.Id)
                .Distinct()
                .ToList();
            if (searchDeleteIds.Count > 0 &&
                !await destination.DeleteSearch(searchDeleteIds, cancellationToken))
            {
                return await Fail(Record, podcast.Id, kind, "Search delete failed. Source rows kept; re-run resumes.");
            }

            await Record(CatalogueMigrateEvidenceEvent.SearchSwapped, $"documents={documents.Count}");

            if (await SourceChangedDuringMove(unit, cancellationToken) is { } change)
            {
                return await Fail(Record, podcast.Id, kind,
                    $"{change} Source rows kept; re-run moves the current podcast and episodes.");
            }

            // Episodes first, then the podcast. A re-created episode that was not written again is still
            // removed here: the parent already holds that video, and the source row must not stay behind.
            foreach (var episode in unit.MoveEpisodes)
            {
                await episodeRepository.Delete(podcast.Id, episode.Id);
            }

            await podcastRepository.Delete(podcast.Id);
            await Record(CatalogueMigrateEvidenceEvent.SourceDeleted);
            await Record(CatalogueMigrateEvidenceEvent.Completed);
            logger.LogInformation(
                "Catalogue migrate applied {PodcastId} {Kind} parent={ParentId} playables={PlayableCount} searchDocuments={SearchDocumentCount}",
                podcast.Id,
                kind,
                unit.Plan.DestParentId,
                playableIds.Length,
                documents.Count);
            return CatalogueMigrateApplyOutcome.Completed;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Catalogue migrate apply failed for {PodcastId} {Kind}.", podcast.Id, kind);
            await Record(CatalogueMigrateEvidenceEvent.Failed, ex.GetType().Name + ": " + ex.Message);
            return CatalogueMigrateApplyOutcome.Failed;
        }
    }

    /// <summary>
    /// Restores one podcast from its begin snapshot: source rows back, search back to Episode,
    /// then the destination rows the batch wrote are deleted. Refused when the destination
    /// parent now holds playables the batch did not write.
    /// </summary>
    public async Task<bool> Rollback(
        CatalogueMigrateEvidenceRecord begin,
        ICatalogueMigrateEvidenceBatch evidence,
        string runId,
        bool apply,
        CancellationToken cancellationToken)
    {
        var preview = await rollbackPreviewer.Preview(begin, apply);
        var podcast = begin.BeforePodcast!;
        var episodes = begin.BeforeEpisodes!;
        var kind = begin.Kind;
        var parentId = preview.ParentId;

        Task Record(string evt, string? detail = null) =>
            evidence.Write(begin with
            {
                Event = evt,
                RunId = runId,
                At = DateTimeOffset.UtcNow,
                BeforePodcast = null,
                BeforeEpisodes = null,
                Detail = detail
            });

        if (preview.Refused)
        {
            if (apply)
            {
                await Record(CatalogueMigrateEvidenceEvent.RollbackRefused, $"stored={preview.Stored} written={preview.Written}");
            }

            return false;
        }

        var searchDocuments = preview.SearchDocuments;
        if (!apply)
        {
            return true;
        }

        await podcastRepository.Save(podcast);
        if (episodes.Length > 0)
        {
            await episodeRepository.Save(episodes);
        }

        await Record(CatalogueMigrateEvidenceEvent.RollbackRestored);
        if (!await destination.UploadSearch(searchDocuments, cancellationToken))
        {
            await Record(CatalogueMigrateEvidenceEvent.Failed, "Rollback search restore failed. Destination rows kept.");
            return false;
        }

        await Record(CatalogueMigrateEvidenceEvent.RollbackSearchRestored);
        foreach (var playableId in begin.DestPlayableIds)
        {
            await destination.DeletePlayable(kind, parentId, playableId);
        }

        await destination.DeleteParent(kind, parentId);
        await Record(CatalogueMigrateEvidenceEvent.RollbackCompleted);
        return true;
    }

    private async Task<CatalogueMigrateApplyOutcome> Fail(
        Func<string, string?, bool, Task> record,
        Guid podcastId,
        string kind,
        string detail)
    {
        logger.LogError("Catalogue migrate apply failed for {PodcastId} {Kind}: {Detail}", podcastId, kind, detail);
        await record(CatalogueMigrateEvidenceEvent.Failed, detail, false);
        return CatalogueMigrateApplyOutcome.Failed;
    }

    /// <summary>
    /// Re-reads the source just before it is deleted. The hourly indexer (or a submit) may have saved the podcast or
    /// added an episode since the plan read it; deleting then would lose that episode or leave the indexer to re-create
    /// the podcast. Returns why the move must stop, or null when the source is as planned.
    /// </summary>
    private async Task<string?> SourceChangedDuringMove(CatalogueMigrateUnit unit, CancellationToken cancellationToken)
    {
        var podcast = unit.Podcast;
        var current = await podcastRepository.GetPodcast(podcast.Id);
        if (current is not null && current.LastIndexed != podcast.LastIndexed)
        {
            return $"Podcast was indexed during the move (lastIndexed {podcast.LastIndexed:O} -> {current.LastIndexed:O}).";
        }

        // A failed or bypassed pass saves without moving lastIndexed but can still change the discovery bookmarks
        // (expensive-query flags, show counts, playlist history) the parent copied.
        if (current is not null && !IndexingSettingsEqual(podcast, current))
        {
            return "Podcast indexing settings changed during the move.";
        }

        var planned = unit.MoveEpisodes.Select(episode => episode.Id).ToHashSet();
        var arrived = 0;
        await foreach (var episode in episodeRepository.GetByPodcastId(podcast.Id).WithCancellation(cancellationToken))
        {
            if (!planned.Contains(episode.Id))
            {
                arrived++;
            }
        }

        return arrived > 0 ? $"{arrived} episode(s) arrived during the move." : null;
    }

    private static bool IndexingSettingsEqual(Podcast planned, Podcast current) =>
        JsonNode.DeepEquals(
            JsonSerializer.SerializeToNode(CatalogueIndexingSource.FromPodcast(planned)),
            JsonSerializer.SerializeToNode(CatalogueIndexingSource.FromPodcast(current)));

    private async Task<bool> HasSiblingParent(string kind, Guid podcastId) => kind switch
    {
        SubmitClassification.NewsReport => await destination.HasTvShow(podcastId),
        SubmitClassification.TvShowEpisode => await destination.HasNewsOrganisation(podcastId),
        _ => false
    };

    private Task WriteDestination(CatalogueMigrateUnit unit, IReadOnlySet<Guid> alreadyHeld)
    {
        var podcast = unit.Podcast;
        var episodes = unit.MoveEpisodes.Where(episode => !alreadyHeld.Contains(episode.Id)).ToList();
        // Playables keep Services only: an id without a services url would be lost. No client lookup in this CLI,
        // so use the deterministic id-derived url (existing urls and images are untouched).
        foreach (var episode in episodes)
        {
            EpisodeServicePresence.EnsureServiceUrlsFromIds(episode, podcast.AppleId);
        }
        switch (unit.Kind)
        {
            case SubmitClassification.NewsReport:
            {
                var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);
                var reports = episodes
                    .Select(episode => CatalogueParentKindMapper.ToNewsReport(episode, organisation))
                    .ToList();
                return destination.SaveNews(organisation, reports);
            }
            case SubmitClassification.TvShowEpisode:
            {
                var show = CatalogueParentKindMapper.ToTvShow(podcast);
                var playables = episodes
                    .Select(episode => CatalogueParentKindMapper.ToTvShowEpisode(episode, show))
                    .ToList();
                return destination.SaveTv(show, playables);
            }
            case SubmitClassification.Film:
                return episodes.Count == 0
                    ? Task.CompletedTask
                    : destination.SaveFilm(CatalogueParentKindMapper.ToFilm(podcast, episodes.Single()));
            default:
                throw new ArgumentOutOfRangeException(nameof(unit), unit.Kind, "Not a corpus-migrate kind.");
        }
    }
}
