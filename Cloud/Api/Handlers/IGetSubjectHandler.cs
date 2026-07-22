using Microsoft.Azure.Functions.Worker.Http;
using RedditPodcastPoster.Auth0.Models;

namespace Api.Handlers;

public interface IGetSubjectHandler
{
    Task<HttpResponseData> Handle(
        HttpRequestData req,
        string subjectName,
        ClientPrincipal? cp,
        CancellationToken c);
}
