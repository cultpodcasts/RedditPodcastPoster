using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Reddit;

namespace DevvitClient;

public class DevvitClientProcessor(
    IDevvitClient devvitClient,
    ILogger<DevvitClientProcessor> logger)
{
    public async Task Run(DevvitClientRequest request)
    {
        var createRequest = new DevvitEpisodeCreateRequest
        {
            PodcastName = request.PodcastName,
            Title = request.Title,
            Description = request.Description,
            ReleaseDateTime = request.ReleaseDateTime,
            Duration = request.Duration,
            SubredditName = request.SubredditName,
            FlairId = request.FlairId,
            FlairText = request.FlairText,
            ImageUrl = request.ImageUrl,
            ServiceLinks = new DevvitServiceLinks
            {
                Youtube = request.Youtube,
                Spotify = request.Spotify,
                ApplePodcasts = request.Apple
            }
        };

        var result = await devvitClient.CreateEpisodePost(createRequest);
        logger.LogInformation("Posted Devvit episode successfully. PostId='{PostId}' Url='{PostUrl}'.", result.PostId,
            result.PostUrl);
    }
}
