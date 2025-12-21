using System.Net;
using Api.Dtos;
using Api.Extensions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.ContentPublisher;

namespace Api.Handlers;

public class PublishHandler(IContentPublisher contentPublisher,
    ILogger<PublishHandler>logger) : IPublishHandler
{
    public async Task<HttpResponseData> PublishHomepage(HttpRequestData req, ClientPrincipal? cp, CancellationToken c)
    {
        try
        {
            var result = await contentPublisher.PublishHomepage();
            if (!result.HomepagePublished || (result.PreProcessedHomepagePublished.HasValue &&
                                              !result.PreProcessedHomepagePublished.Value))
            {
                logger.LogError("{method}: Failed to publish homepage. Result: {result}",
                    nameof(PublishHomepage), result);
                return await req.CreateResponse(HttpStatusCode.InternalServerError)
                    .WithJsonBody(PublishHomepageResponse.ToDto(result), c);
            }

            return await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(PublishHomepageResponse.ToDto(result), c);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to publish homepage.", nameof(PublishHomepage));
        }

        var failure = req.CreateResponse(HttpStatusCode.InternalServerError);
        return failure;
    }
}