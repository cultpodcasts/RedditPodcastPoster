using System.Net;
using Api.Dtos;
using Api.Extensions;
using Api.Models;
using Api.Resolvers;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Auth0;

namespace Api.Handlers;

public class PublicHandler(
    IPodcastEpisodeResolver podcastEpisodeResolver,
    ILogger<PublicHandler> logger) : IPublicHandler
{
    public async Task<HttpResponseData> Get(HttpRequestData req,
        PodcastEpisodeRequestWrapper podcastEpisodeRequestWrapper, ClientPrincipal? _,
        CancellationToken c)
    {
        try
        {
            logger.LogInformation("{GetName}: Get episode with id '{EpisodeId}'.", nameof(Get),
                podcastEpisodeRequestWrapper.EpisodeId);

            var (episode, podcast) =
                await podcastEpisodeResolver.ResolvePodcast(podcastEpisodeRequestWrapper,
                    nameof(Get));

            if (episode == null || episode.Removed)
            {
                logger.LogWarning("{GetName}: Episode with id '{EpisodeId}' not found.", nameof(Get),
                    podcastEpisodeRequestWrapper.EpisodeId);
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            if (podcast == null || podcast.Removed == true)
            {
                logger.LogWarning("{GetName}: Podcast with id '{PodcastId}' for episode '{EpisodeId}' not found.",
                    nameof(Get), episode.PodcastId, podcastEpisodeRequestWrapper.EpisodeId);
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var publicEpisode = new PublicEpisode
            {
                PodcastName = podcast.Name,
                Id = episode.Id,
                Title = episode.Title,
                Description = episode.Description,
                Release = episode.Release,
                Length = episode.Length,
                Explicit = episode.Explicit,
                Urls = episode.Urls,
                Subjects = episode.Subjects,
                Image = episode.Images?.YouTube ??
                        episode.Images?.Spotify ?? episode.Images?.Apple ?? episode.Images?.Other
            };

            var success = await req.CreateResponse(HttpStatusCode.OK)
                .WithJsonBody(publicEpisode, c);
            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Get)}: Failed to get episode.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve episode"), c);
        return failure;
    }
}