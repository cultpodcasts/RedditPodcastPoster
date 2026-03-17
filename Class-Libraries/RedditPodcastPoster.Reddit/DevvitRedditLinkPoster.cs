using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Reddit;

public class DevvitRedditLinkPoster(
    IRedditPostTitleFactory redditPostTitleFactory,
    IDevvitClient devvitClient,
    IOptions<SubredditSettings> subredditSettings,
    IOptions<DevvitSettings> devvitSettings,
    ILogger<DevvitRedditLinkPoster> logger) : IRedditLinkPoster
{
    private readonly SubredditSettings _subredditSettings = subredditSettings.Value;
    private readonly DevvitSettings _devvitSettings = devvitSettings.Value;

    public async Task<PostResponse> Post(PostModel postModel)
    {
        if (postModel.YouTube == null && postModel.Spotify == null && postModel.Apple == null)
        {
            return new PostResponse(null, false);
        }

        var title = await redditPostTitleFactory.ConstructPostTitle(postModel);
        var request = new DevvitEpisodeCreateRequest
        {
            PodcastName = postModel.PodcastName,
            Title = title,
            Description = Truncate(postModel.EpisodeDescription, _devvitSettings.DescriptionMaxLength),
            ReleaseDateTime = postModel.Published.ToUniversalTime().ToString("O"),
            Duration = postModel.EpisodeLength,
            SubredditName = _subredditSettings.SubredditName,
            ServiceLinks = new DevvitServiceLinks
            {
                Youtube = postModel.YouTube?.ToString(),
                Spotify = postModel.Spotify?.ToString(),
                ApplePodcasts = postModel.Apple?.ToString()
            }
        };

        var response = await devvitClient.CreateEpisodePost(request);
        logger.LogInformation("Devvit post created. PostId: '{PostId}', Url: '{PostUrl}'.", response.PostId,
            response.PostUrl);

        return new PostResponse(null, true);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (maxLength <= 0)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return string.Concat(value.AsSpan(0, maxLength - 1), "…");
    }
}
