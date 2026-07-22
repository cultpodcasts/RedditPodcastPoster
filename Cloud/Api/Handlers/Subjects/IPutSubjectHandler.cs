using Api.Models;
using Api.Dtos;
using Api.Dtos.Extensions;
using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Subjects;

public interface IPutSubjectHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        SubjectChangeRequest subject,
        ClientPrincipal? cp,
        CancellationToken ct);
}
