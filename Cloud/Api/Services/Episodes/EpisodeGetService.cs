using Api.Extensions;
using Api.Models;
using Api.Resolvers;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Subjects.Providers;

namespace Api.Services.Episodes;

public class EpisodeGetService(
    IPodcastEpisodeResolver podcastEpisodeResolver,
    ICachedSubjectProvider subjectsProvider,
    EpisodeDiscreteMapper episodeDiscreteMapper,
    ILogger<EpisodeGetService> logger) : IEpisodeGetService
{
    public async Task<EpisodeGetResult> GetAsync(
        PodcastEpisodeRequestWrapper request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("{method}: Get episode with id '{episodeId}'.", nameof(GetAsync),
                request.EpisodeId);

            var podcastEpisodeResolverResponse =
                await podcastEpisodeResolver.ResolvePodcast(
                    request.ToPodcastEpisodeResolverRequest(), nameof(GetAsync));

            if (podcastEpisodeResolverResponse.Episode == null)
            {
                logger.LogWarning("{method}: Episode with name '{episodeId}' not found.", nameof(GetAsync),
                    request.EpisodeId);
                return new EpisodeGetResult(EpisodeGetStatus.EpisodeNotFound);
            }

            if (podcastEpisodeResolverResponse.Podcast == null)
            {
                logger.LogWarning("{method}: Podcast with id '{podcastName}' not found for episode-id '{episodeId}'.",
                    nameof(GetAsync), podcastEpisodeResolverResponse,
                    request.PodcastName);
                return new EpisodeGetResult(EpisodeGetStatus.PodcastNotFound);
            }

            var subjects = await subjectsProvider.GetAll().ToListAsync(cancellationToken);
            var discreteEpisode = await episodeDiscreteMapper.ToDiscreteEpisode(
                podcastEpisodeResolverResponse.Episode,
                podcastEpisodeResolverResponse.Podcast,
                subjects,
                includeGuestSuggestions: true);

            return new EpisodeGetResult(EpisodeGetStatus.Ok, discreteEpisode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to get episode.", nameof(GetAsync));
            return new EpisodeGetResult(EpisodeGetStatus.Failed);
        }
    }
}
