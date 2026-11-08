using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.Podcasts;

public interface IIndexPodcastHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        string podcastName,
        bool backlog,
        CancellationToken c);
}
