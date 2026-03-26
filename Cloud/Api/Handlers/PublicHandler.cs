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

            var podcastEpisodeResolverResponse =
                await podcastEpisodeResolver.ResolvePodcast(podcastEpisodeRequestWrapper.ToPodcastEpisodeResolverRequest(),
                    nameof(Get));

            if (podcastEpisodeResolverResponse.Episode == null || podcastEpisodeResolverResponse.Episode.Removed)
            {
                logger.LogWarning("{GetName}: Episode with id '{EpisodeId}' not found.", nameof(Get),
                    podcastEpisodeRequestWrapper.EpisodeId);
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            if (podcastEpisodeResolverResponse.Podcast == null || podcastEpisodeResolverResponse.Podcast.Removed == true)
            {
                logger.LogWarning("{GetName}: Podcast with id '{PodcastId}' for episode '{EpisodeId}' not found.",
                    nameof(Get), podcastEpisodeResolverResponse.Episode.PodcastId, podcastEpisodeRequestWrapper.EpisodeId);
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var publicEpisode = new PublicEpisode
            {
                PodcastName = podcastEpisodeResolverResponse.Podcast.Name,
                Id = podcastEpisodeResolverResponse.Episode.Id,
                Title = podcastEpisodeResolverResponse.Episode.Title,
                Description = podcastEpisodeResolverResponse.Episode.Description,
                Release = podcastEpisodeResolverResponse.Episode.Release,
                Length = podcastEpisodeResolverResponse.Episode.Length,
                Explicit = podcastEpisodeResolverResponse.Episode.Explicit,
                Urls = podcastEpisodeResolverResponse.Episode.Urls,
                Subjects = podcastEpisodeResolverResponse.Episode.Subjects,
                Image = podcastEpisodeResolverResponse.Episode.Images?.YouTube ??
                        podcastEpisodeResolverResponse.Episode.Images?.Spotify ?? podcastEpisodeResolverResponse.Episode.Images?.Apple ?? podcastEpisodeResolverResponse.Episode.Images?.Other
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