using Api.Extensions;
using Api.Models;
using Api.Resolvers;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Subjects.Providers;

namespace Api.Services.Episodes;

public class EpisodeGetService(
    IPodcastEpisodeResolver podcastEpisodeResolver,
    ICataloguePlayableResolver cataloguePlayableResolver,
    ICachedSubjectProvider subjectsProvider,
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
                // Not a podcast episode: a news report, TV episode or film keeps its id (catalogue migration), so
                // look there before reporting not found. Curators see removed items too, as with episodes.
                var catalogue = await cataloguePlayableResolver.Resolve(request.EpisodeId, cancellationToken);
                if (catalogue != null)
                {
                    return await Found(
                        catalogue.Projection.Episode, catalogue.Projection.Podcast, PlayableKind.For(catalogue),
                        cancellationToken);
                }

                logger.LogWarning("{method}: Episode with id '{episodeId}' not found.", nameof(GetAsync),
                    request.EpisodeId);
                return new EpisodeGetResult(EpisodeGetStatus.EpisodeNotFound);
            }

            if (podcastEpisodeResolverResponse.Podcast == null)
            {
                logger.LogWarning("{method}: Podcast with id '{podcastId}' not found for episode-id '{episodeId}'.",
                    nameof(GetAsync), podcastEpisodeResolverResponse.Episode.PodcastId,
                    request.EpisodeId);
                return new EpisodeGetResult(EpisodeGetStatus.PodcastNotFound);
            }

            return await Found(
                podcastEpisodeResolverResponse.Episode,
                podcastEpisodeResolverResponse.Podcast,
                PlayableKind.ForPodcastEpisode(
                    podcastEpisodeResolverResponse.Episode.Id, podcastEpisodeResolverResponse.Podcast),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to get episode.", nameof(GetAsync));
            return new EpisodeGetResult(EpisodeGetStatus.Failed);
        }
    }

    private async Task<EpisodeGetResult> Found(
        RedditPodcastPoster.Models.Episodes.Episode episode,
        RedditPodcastPoster.Models.Podcasts.Podcast podcast,
        PlayableKind kind,
        CancellationToken cancellationToken)
    {
        var subjects = await subjectsProvider.GetAll().ToListAsync(cancellationToken);
        return new EpisodeGetResult(EpisodeGetStatus.Ok, episode, podcast, subjects, kind);
    }
}
