using RedditPodcastPoster.ContentPublisher.Models;

namespace Api.Dtos.Extensions;

public static class PublishHomepageResponseExtension
{
    public static PublishHomepageResponse ToDto(this PublishHomepageResult result)
    {
        return new PublishHomepageResponse
        {
            HomepagePublished = result.HomepagePublished,
            PreProcessedHomepagePublished = result.PreProcessedHomepagePublished
        };
    }
}
