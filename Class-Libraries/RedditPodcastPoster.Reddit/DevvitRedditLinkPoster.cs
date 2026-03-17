using System.Globalization;
using System.Text;
using System.Text.Json;
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
            Description = postModel.EpisodeDescription,
            ReleaseDateTime = postModel.Published.ToUniversalTime(),
            Duration = ParseDuration(postModel.EpisodeLength),
            SubredditName = _subredditSettings.SubredditName,
            ServiceLinks = new DevvitServiceLinks
            {
                YouTube = postModel.YouTube,
                Spotify = postModel.Spotify,
                Apple = postModel.Apple
            }
        };

        TruncateDescriptionIfPostDataLimitExceeded(request, _devvitSettings.PostDataMaxBytes);

        var response = await devvitClient.CreateEpisodePost(request);
        logger.LogInformation("Devvit post created. PostId: '{PostId}', Url: '{PostUrl}'.", response.PostId,
            response.PostUrl);

        return new PostResponse(null, true);
    }

    private void TruncateDescriptionIfPostDataLimitExceeded(DevvitEpisodeCreateRequest request, int maxBytes)
    {
        if (maxBytes <= 0 || IsWithinPostDataLimit(request, maxBytes))
        {
            return;
        }

        var originalDescription = request.Description;
        if (string.IsNullOrWhiteSpace(originalDescription))
        {
            return;
        }

        var low = 0;
        var high = originalDescription.Length;
        string? best = null;

        while (low <= high)
        {
            var mid = (low + high) / 2;
            request.Description = BuildDescriptionCandidate(originalDescription, mid);

            if (IsWithinPostDataLimit(request, maxBytes))
            {
                best = request.Description;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        request.Description = best ?? string.Empty;

        if (!IsWithinPostDataLimit(request, maxBytes))
        {
            logger.LogWarning(
                "Devvit payload still exceeds configured postData limit of '{MaxBytes}' bytes after description truncation.",
                maxBytes);
        }
    }

    private static bool IsWithinPostDataLimit(DevvitEpisodeCreateRequest request, int maxBytes)
    {
        var json = JsonSerializer.Serialize(request);
        var length = Encoding.UTF8.GetByteCount(json);
        return length <= maxBytes;
    }

    private static string BuildDescriptionCandidate(string description, int maxLength)
    {
        if (maxLength <= 0)
        {
            return string.Empty;
        }

        if (description.Length <= maxLength)
        {
            return description;
        }

        if (maxLength == 1)
        {
            return "…";
        }

        return string.Concat(description.AsSpan(0, maxLength - 1), "…");
    }

    private static TimeSpan ParseDuration(string duration)
    {
        if (TimeSpan.TryParseExact(duration, @"\[h\:mm\:ss\]", CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (TimeSpan.TryParse(duration, CultureInfo.InvariantCulture, out parsed))
        {
            return parsed;
        }

        return TimeSpan.Zero;
    }
}
