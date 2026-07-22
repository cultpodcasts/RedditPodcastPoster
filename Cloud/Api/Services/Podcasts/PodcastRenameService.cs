using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.CloudflareRedirect.Models;
using RedditPodcastPoster.CloudflareRedirect.Services;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.Podcasts;

public class PodcastRenameService(
    IPodcastRepository podcastRepository,
    IRedirectService redirectService,
    IEpisodeSearchIndexerService searchIndexerService,
    PodcastEpisodeProjectionHelper episodeProjectionHelper,
    ILogger<PodcastRenameService> logger) : IPodcastRenameService
{
    private const int MaxPodcastToRename = 2;

    public async Task<PodcastRenameResult> RenameAsync(PodcastRenameCommand change, CancellationToken c)
    {
        try
        {
            if (change.NewName.Contains("/"))
            {
                logger.LogError("New podcast-name contains invalid-character: '{NewName}'.", change.NewName);
                return new PodcastRenameResult(PodcastRenameStatus.InvalidName);
            }

            logger.LogInformation(
                "{method}: Podcast Name-Change Request: podcast-name: '{name}'. new-name: '{newName}'.",
                nameof(RenameAsync), change.Name, change.NewName);
            var podcasts = await podcastRepository.GetAllBy(x =>
                    x.Name.ToLower() == change.Name.ToLower() || x.Name.ToLower() == change.NewName.ToLower())
                .ToListAsync(c);
            if (podcasts.Any(x => x.Name.ToLower() == change.NewName.ToLower()))
            {
                logger.LogError("Podcast found with new-name '{name}'.", change.Name);
                return new PodcastRenameResult(PodcastRenameStatus.Conflict);
            }

            if (podcasts.All(x => x.Name != change.Name))
            {
                logger.LogError("Podcast not found with name '{name}'.", change.Name);
                return new PodcastRenameResult(PodcastRenameStatus.NotFound);
            }

            var podcastsToUpdate = podcasts.Where(x => x.Name == change.Name).ToArray();
            if (podcastsToUpdate.Length > MaxPodcastToRename)
            {
                logger.LogError(
                    "Operation to rename podcasts with name '{name}' to '{newName}' impacts {podcastsToUpdateCount} podcasts. Operation rejected.",
                    change.Name, change.NewName, podcastsToUpdate.Length);
                return new PodcastRenameResult(PodcastRenameStatus.TooMany);
            }

            var result = await redirectService.CreatePodcastRedirect(new PodcastRedirect(change.Name, change.NewName));
            logger.LogInformation("Result of {method} = {result}", nameof(redirectService.CreatePodcastRedirect),
                result);
            if (result)
            {
                var episodeIds = new List<Guid>();
                foreach (var podcast in podcastsToUpdate)
                {
                    var oldName = podcast.Name;
                    podcast.Name = change.NewName;
                    await podcastRepository.Save(podcast);
                    await episodeProjectionHelper.HydrateDetachedEpisodePodcastProjection(podcast, c);
                    logger.LogInformation("Renamed podcast '{oldName}' to '{newName}'.", oldName, change.NewName);
                    episodeIds.AddRange(await episodeProjectionHelper.GetEpisodeIdsByPodcastId(podcast.Id, c));
                }

                var indexed = await searchIndexerService.IndexEpisodes(episodeIds.Distinct(), c);

                logger.LogInformation("Search-index run-state: {indexState}.", indexed);
                return new PodcastRenameResult(PodcastRenameStatus.Ok, indexed);
            }

            return new PodcastRenameResult(PodcastRenameStatus.BadRequest);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to rename podcast.", nameof(RenameAsync));
            return new PodcastRenameResult(PodcastRenameStatus.Failed);
        }
    }
}
