using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.UrlShortening.Services;
using RedditPodcastPoster.UrlSubmission.Services;

namespace Api.Services.Episodes;

/// <summary>
/// Moves one podcast episode into a new Film, the way CatalogueMigrate moves a one-episode podcast to a film:
/// <list type="number">
/// <item>Map with <see cref="CatalogueParentKindMapper.ToFilm"/>, so the film keeps the episode id, services, subjects,
/// and posted / tweeted / Bluesky state. The name is the episode title unless the request gives one.</item>
/// <item>Write the film, then swap the search document under the same id to ContentKind Film
/// (<see cref="CatalogueMigrateSearchDocuments"/>), then delete the source episode row.</item>
/// </list>
/// As in the migrator, a failed search swap stops before the source row is deleted. Re-sending the request then
/// resumes: a film with the episode id while the episode row still exists is the interrupted move, so the film is
/// reused rather than reported as a conflict.
/// After the episode row is deleted, the episode's short-link record is deleted (best effort), as catalogue edits do,
/// so the Shortner falls back to search and redirects the old short link straight to the film page. Re-sending after
/// a completed move (episode row gone, film with the episode id present) is accepted again for that film.
/// The source podcast is never deleted or changed, even when it has no episodes left.
/// </summary>
public class EpisodeFilmMoveService(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    IFilmRepository filmRepository,
    IPlayableSearchDocumentSwap searchSwap,
    IShortnerService shortnerService,
    ILogger<EpisodeFilmMoveService> logger) : IEpisodeFilmMoveService
{
    public async Task<EpisodeFilmMoveResult> MoveAsync(EpisodeFilmMoveCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var podcast = await podcastRepository.GetPodcast(command.PodcastId);
            var episode = podcast is null
                ? null
                : await episodeRepository.GetEpisode(command.PodcastId, command.EpisodeId);
            if (podcast is null)
            {
                return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.NotFound);
            }

            if (episode is null)
            {
                // A re-send after a completed move (double click, or a lost 202): the film has the episode id.
                var moved = await filmRepository.GetFilm(command.EpisodeId);
                if (moved is null)
                {
                    return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.NotFound);
                }

                logger.LogInformation(
                    "{method}: Episode '{episodeId}' was already moved to film '{filmId}'; accepting the re-send.",
                    nameof(MoveAsync), command.EpisodeId, moved.Id);
                return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.Accepted, moved.Id, moved.Name);
            }

            if (episode.Removed)
            {
                return new EpisodeFilmMoveResult(
                    EpisodeFilmMoveStatus.BadRequest,
                    Error: "A removed episode cannot be moved to a film");
            }

            var film = await filmRepository.GetFilm(episode.Id);
            if (film is not null)
            {
                logger.LogWarning(
                    "{method}: Film '{filmId}' already exists for episode still in podcast '{podcastId}'; resuming the move.",
                    nameof(MoveAsync), film.Id, podcast.Id);
            }
            else
            {
                var name = string.IsNullOrWhiteSpace(command.Request.Title)
                    ? episode.Title.Trim()
                    : command.Request.Title.Trim();
                if (name.Length == 0)
                {
                    return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.BadRequest, Error: "Film name is required");
                }

                if (name.Contains('/'))
                {
                    return new EpisodeFilmMoveResult(
                        EpisodeFilmMoveStatus.BadRequest,
                        Error: "Film name cannot contain '/'");
                }

                var clashes = await PublisherNameAttachLookup.FindByName(filmRepository, name, cancellationToken);
                if (clashes.Count > 0)
                {
                    logger.LogError("{method}: A film named '{name}' already exists.", nameof(MoveAsync), name);
                    return new EpisodeFilmMoveResult(
                        EpisodeFilmMoveStatus.Conflict,
                        clashes[0].Id,
                        clashes[0].Name,
                        "A film with this name already exists");
                }

                // Films keep Services only (no nested ids). Materialise watch?v= from ids.youtube
                // when services.youtube.url is missing so search/Watch keep YouTube after the move.
                EpisodeServicePresence.EnsureYouTubeWatchUrl(episode);
                film = CatalogueParentKindMapper.ToFilm(podcast, episode);
                film.Name = name;
                film.FileKey = FileKeyFactory.GetFilmFileKey(name);
                // ToFilm copies publisher fields from the podcast, including Removed. Here the (active) episode is the
                // unit that moves, so the film is active even when it leaves a removed podcast.
                film.Removed = null;
                await filmRepository.Save(film);
            }

            // Same document shape as the film PATCH and the film pull indexer, so the next pull run does not rewrite it.
            var documents = new[] { CataloguePlayableSearchDocument.For(film) };
            if (!await searchSwap.UploadAsync(documents, cancellationToken))
            {
                logger.LogError(
                    "{method}: Search swap failed for film '{filmId}'. Episode row kept; re-send to resume.",
                    nameof(MoveAsync), film.Id);
                return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.Failed, film.Id, film.Name, FailureIndexing: true);
            }

            await episodeRepository.Delete(podcast.Id, episode.Id);
            logger.LogInformation(
                "Moved episode '{episodeId}' from podcast '{podcastId}' to film '{filmName}'.",
                episode.Id, podcast.Id, film.Name);
            await DeleteShortLink(podcast, episode);
            return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.Accepted, film.Id, film.Name);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to move episode '{episodeId}' of podcast '{podcastId}' to a film.",
                nameof(MoveAsync), command.EpisodeId, command.PodcastId);
            return new EpisodeFilmMoveResult(EpisodeFilmMoveStatus.Failed);
        }
    }

    /// <summary>
    /// The short-link key is the episode id (base 64), which the film keeps. With no record the Shortner looks the id up
    /// in search (now ContentKind Film) and redirects to <c>/film/{name}/{shortId}</c>. The move has already succeeded,
    /// so a failure is logged and does not fail the request.
    /// </summary>
    private async Task DeleteShortLink(
        RedditPodcastPoster.Models.Podcasts.Podcast podcast,
        RedditPodcastPoster.Models.Episodes.Episode episode)
    {
        try
        {
            var result = await shortnerService.Delete(new PodcastEpisode(podcast, episode));
            if (!result.Success)
            {
                logger.LogWarning(
                    "{method}: Short-link record for '{episodeId}' was not deleted (absent, or the KV delete failed).",
                    nameof(DeleteShortLink), episode.Id);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Short-link delete failed for moved episode '{episodeId}'.",
                nameof(DeleteShortLink), episode.Id);
        }
    }
}
