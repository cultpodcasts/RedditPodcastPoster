using Api.Models;

namespace Api.Dtos.Extensions;

public static class EpisodePublishResponseExtension
{
    public static EpisodePublishResponse ToDto(this EpisodePublishOutcome outcome)
    {
        return new EpisodePublishResponse(outcome.PodcastId ?? Guid.Empty)
        {
            Posted = outcome.Posted,
            Tweeted = outcome.Tweeted,
            BlueskyPosted = outcome.BlueskyPosted,
            FailedTweetContent = outcome.FailedTweetContent
        };
    }
}
