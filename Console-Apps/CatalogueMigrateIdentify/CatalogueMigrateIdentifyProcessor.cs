using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using Identify = RedditPodcastPoster.UrlSubmission.Migration.CatalogueMigrateIdentify;

namespace CatalogueMigrateIdentify;

public sealed record CatalogueMigrateIdentifyRunResult(int ExitCode, int CandidateCount);

public class CatalogueMigrateIdentifyProcessor(
    IPodcastRepository podcastRepository,
    IEpisodeRepository episodeRepository,
    ILogger<CatalogueMigrateIdentifyProcessor> logger)
{
    public async Task<CatalogueMigrateIdentifyRunResult> Run(CatalogueMigrateIdentifyRequest request)
    {
        if (request.Apply)
        {
            logger.LogError(
                "Catalogue migrate identify --apply is refused (exit 2). This tool does not write Cosmos or upload Search. Exit 2 is intended.");
            return new CatalogueMigrateIdentifyRunResult(2, 0);
        }

        logger.LogInformation("Catalogue migrate identify dry-run — no Cosmos writes.");

        var candidateCount = 0;
        await foreach (var podcast in EnumeratePodcasts(request.PodcastId))
        {
            if (podcast.Removed == true)
            {
                continue;
            }

            var episodes = await episodeRepository.GetByPodcastId(podcast.Id)
                .Where(episode => !episode.Removed)
                .ToListAsync();
            var suggestion = Identify.FromEpisodes(episodes);
            if (suggestion.ContentKind == SubmitClassification.Episode)
            {
                continue;
            }

            candidateCount++;
            logger.LogInformation(
                "Catalogue migrate candidate {PodcastId} {ContentKind} allowlist={RequiresAllowlist} curator={RequiresCurator} episodes={EpisodeCount}",
                podcast.Id,
                suggestion.ContentKind,
                suggestion.RequiresAllowlist,
                suggestion.RequiresCurator,
                episodes.Count);
        }

        logger.LogInformation("Catalogue migrate identify dry-run complete. Candidates={Count}", candidateCount);
        return new CatalogueMigrateIdentifyRunResult(0, candidateCount);
    }

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
