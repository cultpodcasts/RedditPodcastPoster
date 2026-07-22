using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Subjects;

public interface IPutSubjectHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        Subject subject,
        ClientPrincipal? cp,
        CancellationToken ct);
}
