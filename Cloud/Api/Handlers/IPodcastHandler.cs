using Api.Dtos;
using Api.Models;
using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0;
using PodcastRenameRequest = Api.Models.PodcastRenameRequest;

namespace Api.Handlers;

public interface IPodcastHandler
{
    Task<HttpResponseData> Post(
        HttpRequestData req,
        PodcastChangeRequestWrapper podcastChangeRequestWrapper,
        ClientPrincipal? clientPrincipal,
        CancellationToken c);

    Task<HttpResponseData> Index(
        HttpRequestData req, 
        string podcastName, 
        ClientPrincipal? clientPrincipal,
        CancellationToken c);

    Task<HttpResponseData> Rename(
        HttpRequestData req, 
        PodcastRenameRequest change, 
        ClientPrincipal? clientPrincipal,
        CancellationToken c);

    Task<HttpResponseData> Get(
        HttpRequestData req, 
        PodcastGetRequest podcastGetRequest, 
        ClientPrincipal? clientPrincipal, 
        CancellationToken c);
}