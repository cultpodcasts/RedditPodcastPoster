using Microsoft.Azure.Functions.Worker.Http;
using Api.Dtos;
using Api.Models;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers.Subjects;

public interface IPostSubjectHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        SubjectChangeRequestWrapper subjectChangeRequestWrapper,
        ClientPrincipal? cp,
        CancellationToken c);
}
