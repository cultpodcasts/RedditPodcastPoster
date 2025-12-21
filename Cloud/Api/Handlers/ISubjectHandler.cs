using Api.Dtos;
using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0;

namespace Api.Handlers;

public interface ISubjectHandler
{
    Task<HttpResponseData> Get(HttpRequestData req, string subjectName, ClientPrincipal? cp,
        CancellationToken c);

    Task<HttpResponseData> Post(HttpRequestData req,
        SubjectChangeRequestWrapper subjectChangeRequestWrapper, ClientPrincipal? cp, CancellationToken c);

    Task<HttpResponseData> Put(HttpRequestData req, Dtos.Subject subject, ClientPrincipal? cp,
        CancellationToken ct);
}