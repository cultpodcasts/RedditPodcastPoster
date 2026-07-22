using Api.Extensions;
using Api.Models;
using Api.Resolvers;
using Microsoft.Extensions.Logging;

namespace Api.Services.Public;

public class PublicEpisodeGetService(
    IPodcastEpisodeResolver podcastEpisodeResolver,
    ILogger<PublicEpisodeGetService> logger) : IPublicEpisodeGetService
{
    public async Task<PublicEpisodeGetResult> GetAsync(
        PodcastEpisodeRequestWrapper request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("{GetName}: Get episode with id '{EpisodeId}'.", nameof(GetAsync),
                request.EpisodeId);

            var podcastEpisodeResolverResponse =
                await podcastEpisodeResolver.ResolvePodcast(
                    request.ToPodcastEpisodeResolverRequest(),
                    nameof(GetAsync));

            if (podcastEpisodeResolverResponse.Episode == null || podcastEpisodeResolverResponse.Episode.Removed)
            {
                logger.LogWarning("{GetName}: Episode with id '{EpisodeId}' not found.", nameof(GetAsync),
                    request.EpisodeId);
                return new PublicEpisodeGetResult(PublicEpisodeGetStatus.NotFound);
            }

            if (podcastEpisodeResolverResponse.Podcast == null ||
                podcastEpisodeResolverResponse.Podcast.Removed == true)
            {
                logger.LogWarning(
                    "{GetName}: Podcast with id '{PodcastId}' for episode '{EpisodeId}' not found.",
                    nameof(GetAsync), podcastEpisodeResolverResponse.Episode.PodcastId, request.EpisodeId);
                return new PublicEpisodeGetResult(PublicEpisodeGetStatus.NotFound);
            }

            return new PublicEpisodeGetResult(
                PublicEpisodeGetStatus.Ok,
                podcastEpisodeResolverResponse.Episode,
                podcastEpisodeResolverResponse.Podcast);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(GetAsync)}: Failed to get episode.");
            return new PublicEpisodeGetResult(PublicEpisodeGetStatus.Failed);
        }
    }
}
