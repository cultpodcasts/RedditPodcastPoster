using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher.Publishers;

namespace Api.Services.Homepage;

public class HomepagePublishService(
    IHomepagePublisher contentPublisher,
    ILogger<HomepagePublishService> logger) : IHomepagePublishService
{
    public async Task<HomepagePublishResult> PublishAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await contentPublisher.PublishHomepage();
            if (!result.HomepagePublished || (result.PreProcessedHomepagePublished.HasValue &&
                                              !result.PreProcessedHomepagePublished.Value))
            {
                logger.LogError("{method}: Failed to publish homepage. Result: {result}",
                    nameof(PublishAsync), result);
                return new HomepagePublishResult(HomepagePublishStatus.Failed, result);
            }

            return new HomepagePublishResult(HomepagePublishStatus.Ok, result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to publish homepage.", nameof(PublishAsync));
            return new HomepagePublishResult(HomepagePublishStatus.Failed);
        }
    }
}
