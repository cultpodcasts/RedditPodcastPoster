using Microsoft.Extensions.Logging;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Migration;
using Identify = RedditPodcastPoster.UrlSubmission.Migration.CatalogueMigrateIdentify;

namespace CatalogueMigrate;

public sealed record CatalogueMigrateRunResult(int ExitCode, int PlannedCount, int SearchDocumentCount);

public class CatalogueMigrateProcessor(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ILogger<CatalogueMigrateProcessor> logger)
{
    public async Task<CatalogueMigrateRunResult> Run(CatalogueMigrateRequest request)
    {
        if (request.Apply)
        {
            logger.LogError(
                "Catalogue migrate --apply is refused (exit 2). This tool does not write Cosmos or upload Search. Exit 2 is intended.");
            return new CatalogueMigrateRunResult(2, 0, 0);
        }

        if (!IsSupportedKind(request.Kind))
        {
            logger.LogError(
                "Catalogue migrate --kind must be NewsReport, Film, or TvShowEpisode. Got {Kind}.",
                request.Kind);
            return new CatalogueMigrateRunResult(1, 0, 0);
        }

        if (request.Kind is SubmitClassification.Film or SubmitClassification.TvShowEpisode
            && request.PodcastId is null)
        {
            logger.LogError(
                "Catalogue migrate --kind {Kind} requires --podcast-id, because stored identify cannot flag Film or TV.",
                request.Kind);
            return new CatalogueMigrateRunResult(1, 0, 0);
        }

        logger.LogInformation(
            "Catalogue migrate dry-run kind={Kind} — no Cosmos writes and no search upload. Order is News then Film then TV.",
            request.Kind);

        var plannedCount = 0;
        var searchDocumentCount = 0;
        await foreach (var podcast in EnumeratePodcasts(request.PodcastId))
        {
            if (podcast.Removed == true)
            {
                continue;
            }

            var episodes = await episodeRepository.GetByPodcastId(podcast.Id)
                .Where(episode => !episode.Removed)
                .ToListAsync();

            var planEpisodes = episodes;
            if (request.Kind == SubmitClassification.NewsReport)
            {
                var nonNewsIds = Identify.NonNewsEpisodeIds(episodes);
                if (nonNewsIds.Count > 0)
                {
                    if (nonNewsIds.Count < episodes.Count)
                    {
                        logger.LogWarning(
                            "Catalogue migrate skipped mixed News show {PodcastId}; non-News episode ids: {EpisodeIds}",
                            podcast.Id,
                            string.Join(",", nonNewsIds));
                    }

                    continue;
                }

                if (Identify.FromEpisodes(episodes).ContentKind != SubmitClassification.NewsReport)
                {
                    continue;
                }

                planEpisodes = episodes.Where(Identify.IsNewsReportEpisode).ToList();
            }

            var plan = CatalogueMigrateMover.Plan(podcast, planEpisodes, request.Kind);
            if (!plan.Accepted)
            {
                logger.LogWarning(
                    "Catalogue migrate rejected {PodcastId} {Kind}: {Reason}",
                    podcast.Id,
                    request.Kind,
                    plan.RejectReason);
                continue;
            }

            var searchDocuments = CatalogueMigrateSearchDocuments.FromPodcastEpisodes(
                podcast,
                planEpisodes,
                request.Kind);
            plannedCount++;
            searchDocumentCount += searchDocuments.Count;
            logger.LogInformation(
                "Catalogue migrate plan {PodcastId} {Kind} parent={ParentId} playables={PlayableCount} searchDocuments={SearchDocumentCount} allowlist={RequiresAllowlist}",
                plan.SourcePodcastId,
                plan.ContentKind,
                plan.DestParentId,
                plan.DestPlayableIds.Length,
                searchDocuments.Count,
                plan.RequiresAllowlist);
        }

        logger.LogInformation(
            "Catalogue migrate dry-run complete. Planned={Count} SearchDocuments={SearchDocumentCount}",
            plannedCount,
            searchDocumentCount);
        return new CatalogueMigrateRunResult(0, plannedCount, searchDocumentCount);
    }

    private static bool IsSupportedKind(string kind) =>
        kind is SubmitClassification.NewsReport
            or SubmitClassification.Film
            or SubmitClassification.TvShowEpisode;

    private IAsyncEnumerable<Podcast> EnumeratePodcasts(Guid? podcastId)
    {
        if (podcastId is not { } id)
        {
            return podcastRepository.GetAll();
        }

        return EnumerateOne(id);
    }

    private async IAsyncEnumerable<Podcast> EnumerateOne(Guid id)
    {
        var podcast = await podcastRepository.GetPodcast(id);
        if (podcast is not null)
        {
            yield return podcast;
        }
    }
}
