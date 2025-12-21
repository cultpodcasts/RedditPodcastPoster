using Api.Dtos;
using Api.Models;
using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0;

namespace Api.Handlers;

public interface IEpisodeHandler
{
    Task<HttpResponseData> Delete(
        HttpRequestData req,
        Guid episodeId,
        ClientPrincipal? cp,
        CancellationToken c);

    Task<HttpResponseData> Publish(
        HttpRequestData req,
        EpisodePublishRequestWrapper publishRequest,
        ClientPrincipal? cp,
        CancellationToken c);

    Task<HttpResponseData> GetOutgoing(
        HttpRequestData req,
        ClientPrincipal? cp,
        CancellationToken c);

    Task<HttpResponseData> Post(
        HttpRequestData req,
        EpisodeChangeRequestWrapper episodeChangeRequestWrapper,
        ClientPrincipal? cp,
        CancellationToken c);

    Task<HttpResponseData> Get(
        HttpRequestData req,
        Guid episodeId,
        ClientPrincipal? cp,
        CancellationToken c);
}