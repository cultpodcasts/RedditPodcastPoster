using Api.Dtos;
using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher.Publishers;

namespace Api.Services.Homepage;

public interface IHomepagePublishService
{
    Task<HomepagePublishResult> PublishAsync(CancellationToken cancellationToken);
}

public class HomepagePublishService(
    IHomepagePublisher contentPublisher,
    ILogger<HomepagePublishService> logger) : IHomepagePublishService
{
    public async Task<HomepagePublishResult> PublishAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await contentPublisher.PublishHomepage();
            var dto = PublishHomepageResponse.ToDto(result);
            if (!result.HomepagePublished || (result.PreProcessedHomepagePublished.HasValue &&
                                              !result.PreProcessedHomepagePublished.Value))
            {
                logger.LogError("{method}: Failed to publish homepage. Result: {result}",
                    nameof(PublishAsync), result);
                return new HomepagePublishResult(HomepagePublishStatus.Failed, dto);
            }

            return new HomepagePublishResult(HomepagePublishStatus.Ok, dto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to publish homepage.", nameof(PublishAsync));
            return new HomepagePublishResult(HomepagePublishStatus.Failed);
        }
    }
}
