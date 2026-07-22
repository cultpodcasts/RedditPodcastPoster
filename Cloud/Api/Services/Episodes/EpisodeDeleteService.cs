using Api.Extensions;
using Api.Models;
using Api.Resolvers;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlShortening.Services;

namespace Api.Services.Episodes;

public class EpisodeDeleteService(
    IPodcastEpisodeResolver podcastEpisodeResolver,
    IEpisodeRepository episodeRepository,
    EpisodeSearchIndexCleanup searchIndexCleanup,
    IShortnerService shortnerService,
    ILogger<EpisodeDeleteService> logger) : IEpisodeDeleteService
{
    public async Task<EpisodeDeleteResult> DeleteAsync(
        PodcastEpisodeRequestWrapper request,
        CancellationToken cancellationToken)
    {
        try
        {
            var resolved = await podcastEpisodeResolver.ResolvePodcast(
                request.ToPodcastEpisodeResolverRequest(),
                nameof(DeleteAsync));

            if (resolved.State == PodcastEpisodeResolveState.PodcastConflict)
            {
                logger.LogWarning(
                    "Delete: Multiple podcasts with Podcast-Name: '{podcastName}', Episode-id: '{episodeId}'.",
                    request.PodcastName, request.EpisodeId);
                return new EpisodeDeleteResult(EpisodeDeleteStatus.PodcastConflict);
            }

            if (resolved.Episode == null || resolved.Podcast == null)
            {
                logger.LogWarning(
                    "Delete: missing episode or podcast. Podcast-Name: '{podcastName}', Episode-id: '{episodeId}'.",
                    request.PodcastName, request.EpisodeId);
                return new EpisodeDeleteResult(EpisodeDeleteStatus.NotFound);
            }

            if (resolved.Episode.Tweeted || resolved.Episode.Posted)
            {
                return new EpisodeDeleteResult(
                    EpisodeDeleteStatus.AlreadySocial,
                    Posted: resolved.Episode.Posted,
                    Tweeted: resolved.Episode.Tweeted);
            }

            await episodeRepository.Delete(resolved.Episode.PodcastId, resolved.Episode.Id);
            await searchIndexCleanup.DeleteSearchEntry(resolved.Podcast.Name, request.EpisodeId, cancellationToken);
            await shortnerService.Delete(new PodcastEpisode(resolved.Podcast, resolved.Episode));

            logger.LogWarning(
                "Delete detached episode from podcast with id '{podcastId}' and episode-id '{episodeId}'.",
                resolved.Podcast.Id, resolved.Episode.Id);

            return new EpisodeDeleteResult(EpisodeDeleteStatus.Deleted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete episode.");
            return new EpisodeDeleteResult(EpisodeDeleteStatus.Failed);
        }
    }
}
