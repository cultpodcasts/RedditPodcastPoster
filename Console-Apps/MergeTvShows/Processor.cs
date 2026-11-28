using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;

namespace MergeTvShows;

public class Processor(
    ITvShowRepository tvShowRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    IFileKeyAllocator fileKeyAllocator,
    IPlayableSearchDocumentSwap searchSwap,
    ILogger<Processor> logger)
{
    public async Task<int> Process(MergeTvShowsRequest request, CancellationToken cancellationToken)
    {
        if (request.TargetId == Guid.Empty ||
            string.IsNullOrWhiteSpace(request.TargetName) ||
            !request.SourceIds.Any())
        {
            logger.LogError("Merge requires --target-id, --target-name, and --source-ids.");
            return 1;
        }

        var sourceIds = request.SourceIds.Distinct().Where(id => id != request.TargetId).ToArray();
        if (sourceIds.Length == 0)
        {
            logger.LogError("Provide at least one source show id different from the target.");
            return 1;
        }

        var renames = ParseRenames(request.RenameEpisodes);
        var deleteEpisodeIds = request.DeleteEpisodeIds.Distinct().ToHashSet();

        var target = await tvShowRepository.GetTvShow(request.TargetId);
        if (target is null)
        {
            logger.LogError("Target TV show '{id}' was not found.", request.TargetId);
            return 1;
        }

        var sources = new List<TvShow>();
        foreach (var sourceId in sourceIds)
        {
            var source = await tvShowRepository.GetTvShow(sourceId);
            if (source is null)
            {
                logger.LogError("Source TV show '{id}' was not found.", sourceId);
                return 1;
            }

            sources.Add(source);
        }

        var targetName = request.TargetName.Trim();
        if (string.IsNullOrWhiteSpace(targetName))
        {
            logger.LogError("target-name must not be blank.");
            return 1;
        }

        var oldName = target.Name;
        var oldFileKey = target.FileKey;
        var newFileKey = await fileKeyAllocator.AllocateForRename(
            FileKeyKind.TvShow, targetName, target.Id, target.FileKey);
        target.Name = targetName;

        var toMove = new List<(Guid OldTvShowId, TvShowEpisode Episode)>();
        foreach (var source in sources)
        {
            await foreach (var episode in tvShowEpisodeRepository.GetByTvShowId(source.Id)
                               .WithCancellation(cancellationToken))
            {
                if (deleteEpisodeIds.Contains(episode.Id))
                {
                    continue;
                }

                toMove.Add((source.Id, episode));
            }
        }

        await foreach (var episode in tvShowEpisodeRepository.GetByTvShowId(target.Id)
                           .WithCancellation(cancellationToken))
        {
            if (deleteEpisodeIds.Contains(episode.Id))
            {
                continue;
            }

            // Already on the target — still apply rename / denormalised name.
            toMove.Add((target.Id, episode));
        }

        var deleteEpisodes = new List<(Guid TvShowId, Guid EpisodeId, string Title)>();
        foreach (var episodeId in deleteEpisodeIds)
        {
            var episode = await tvShowEpisodeRepository.GetBy(item => item.Id == episodeId);
            if (episode is null)
            {
                logger.LogWarning("Delete episode '{id}' was not found; skipping.", episodeId);
                continue;
            }

            deleteEpisodes.Add((episode.TvShowId, episode.Id, episode.Title));
        }

        logger.LogWarning(
            "Plan: rename target '{targetId}' '{oldName}' -> '{newName}' (fileKey '{oldKey}' -> '{newKey}'); move {moveCount} episode(s); delete {deleteCount} episode(s); delete {sourceCount} emptied show(s). Apply={apply}.",
            target.Id, oldName, targetName, oldFileKey, newFileKey,
            toMove.Count, deleteEpisodes.Count, sources.Count, request.Apply);

        foreach (var (oldShowId, episode) in toMove)
        {
            var newTitle = renames.TryGetValue(episode.Id, out var renamed) ? renamed : episode.Title;
            logger.LogWarning(
                "  move episode '{episodeId}' '{oldTitle}' -> '{newTitle}' from show '{oldShowId}' to '{targetId}'.",
                episode.Id, episode.Title, newTitle, oldShowId, target.Id);
        }

        foreach (var (showId, episodeId, title) in deleteEpisodes)
        {
            logger.LogWarning("  delete episode '{episodeId}' '{title}' from show '{showId}'.", episodeId, title,
                showId);
        }

        // Trailers live on the show document (RPP #1412). A source's trailers move onto the target before the source
        // is deleted, so deleting it cannot lose them.
        foreach (var source in sources)
        {
            foreach (var trailer in source.Trailers ?? [])
            {
                var alreadyOnTarget = target.HasTrailer(trailer.Id);
                logger.LogWarning(
                    alreadyOnTarget
                        ? "  keep trailer '{trailerId}' '{title}': target '{targetId}' already holds it (source '{showId}' copy is dropped with the show)."
                        : "  move trailer '{trailerId}' '{title}' from show '{showId}' to '{targetId}'.",
                    trailer.Id, trailer.Title, alreadyOnTarget ? target.Id : source.Id,
                    alreadyOnTarget ? source.Id : target.Id);
            }
        }

        foreach (var source in sources)
        {
            logger.LogWarning("  delete emptied show '{showId}' '{name}'.", source.Id, source.Name);
        }

        if (!request.Apply)
        {
            logger.LogWarning("Dry-run only. Re-run with --apply to write.");
            await fileKeyAllocator.DisposeAsync();
            return 0;
        }

        try
        {
            // ETag-guarded write of only the fields this tool owns, with the sources' trailers adopted. A trailer
            // attached to the target since it was read is kept; a retry finds the sources' trailers already adopted.
            var updatedTarget = await tvShowRepository.Update(target.Id, latest =>
            {
                latest.Name = targetName;
                latest.FileKey = newFileKey;
                foreach (var source in sources)
                {
                    latest.AdoptTrailersFrom(source);
                }

                return true;
            });
            if (updatedTarget is null)
            {
                logger.LogError("Target TV show '{id}' was deleted during the merge.", target.Id);
                return 1;
            }

            target = updatedTarget;

            var searchDocs = new List<EpisodeSearchRecord>();
            foreach (var (oldShowId, episode) in toMove)
            {
                if (renames.TryGetValue(episode.Id, out var renamed))
                {
                    episode.Title = renamed;
                }

                episode.SetTvShowProperties(target);
                await tvShowEpisodeRepository.Save(episode);
                if (oldShowId != target.Id)
                {
                    await tvShowEpisodeRepository.Delete(oldShowId, episode.Id);
                }

                searchDocs.Add(CataloguePlayableSearchDocument.For(episode));
            }

            var deletedSearchIds = new List<Guid>();
            foreach (var (showId, episodeId, _) in deleteEpisodes)
            {
                await tvShowEpisodeRepository.Delete(showId, episodeId);
                deletedSearchIds.Add(episodeId);
            }

            foreach (var source in sources)
            {
                var remaining = await tvShowEpisodeRepository.GetByTvShowId(source.Id).CountAsync(cancellationToken);
                if (remaining > 0)
                {
                    logger.LogError(
                        "Source show '{showId}' still has {count} episode(s); not deleting the show.",
                        source.Id, remaining);
                    return 2;
                }

                // A trailer attached to the source since it was read must move too: the source is about to go.
                var latestSource = await tvShowRepository.GetTvShow(source.Id);
                if (latestSource?.Trailers is { Count: > 0 })
                {
                    await tvShowRepository.Update(target.Id, latest => latest.AdoptTrailersFrom(latestSource).Count > 0);
                }

                await tvShowRepository.Delete(source.Id);
            }

            var uploaded = searchDocs.Count == 0 || await searchSwap.UploadAsync(searchDocs, cancellationToken);
            var deleted = deletedSearchIds.Count == 0 ||
                          await searchSwap.DeleteAsync(deletedSearchIds, cancellationToken);
            if (!uploaded || !deleted)
            {
                logger.LogError(
                    "Cosmos writes succeeded but search sync failed (uploaded={uploaded}, deleted={deleted}).",
                    uploaded, deleted);
                return 3;
            }

            logger.LogWarning("Merge applied. Target '{targetId}' '{name}' fileKey '{fileKey}'.", target.Id, target.Name,
                target.FileKey);
            return 0;
        }
        finally
        {
            await fileKeyAllocator.DisposeAsync();
        }
    }

    private static Dictionary<Guid, string> ParseRenames(IEnumerable<string> raw)
    {
        var map = new Dictionary<Guid, string>();
        foreach (var entry in raw)
        {
            if (string.IsNullOrWhiteSpace(entry))
            {
                continue;
            }

            var split = entry.Split('=', 2);
            if (split.Length != 2 || !Guid.TryParse(split[0].Trim(), out var id) ||
                string.IsNullOrWhiteSpace(split[1]))
            {
                throw new ArgumentException(
                    $"Invalid --rename-episode value '{entry}'. Expected id=Title.");
            }

            map[id] = split[1].Trim();
        }

        return map;
    }
}
