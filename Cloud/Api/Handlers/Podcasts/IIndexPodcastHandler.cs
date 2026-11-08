using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.Podcasts;

public interface IIndexPodcastHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        string podcastName,
        CancellationToken c);

    Task<HttpResponseData> HandleBacklog(
        IHandlerContext ctx,
        string podcastName,
        CancellationToken c);
}
