using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeVideoService(
    ILogger<YouTubeVideoService> logger)
    : IYouTubeVideoService
{
    private const int MaxSearchResults = 5;

    public async Task<IList<Video>?> GetVideoContentDetails(
        YouTubeServiceWrapper youTubeService,
        IEnumerable<string> videoIds,
        IndexingContext? indexingContext,
        bool withSnippets = false,
        bool withStatistics = false)
    {
        if (indexingContext is {SkipYouTubeUrlResolving: true})
        {
            logger.LogInformation(
                "Skipping '{methodName}' as '{skipYouTubeUrlResolvingName}' is set. Video-ids: '{videoIds}'.",
                nameof(GetVideoContentDetails), nameof(indexingContext.SkipYouTubeUrlResolving),
                string.Join(",", videoIds));
            return null;
        }

        var result = new List<Video>();
        var nextPageToken = "";
        var batch = 0;
        var batchVideoIds = videoIds.Take(MaxSearchResults);
        while (batchVideoIds.Any())
        {
            while (nextPageToken != null)
            {
                var contentDetails = "contentDetails";
                if (withSnippets)
                {
                    contentDetails = "snippet," + contentDetails;
                }

                if (withStatistics)
                {
                    contentDetails = "statistics," + contentDetails;
                }

                var request = youTubeService.YouTubeService.Videos.List(contentDetails);
                request.Id = string.Join(",", batchVideoIds);
                request.MaxResults = MaxSearchResults;
                VideoListResponse response;
                try
                {
                    response = await request.ExecuteAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Failed to use {youTubeServiceName} with api-key-name '{apiKeyName}' obtaining videos matching video-ids '{videoIds}'.",
                        nameof(youTubeService.YouTubeService), youTubeService.ApiKeyName, string.Join(",", videoIds));
                    if (indexingContext != null)
                    {
                        indexingContext.SkipYouTubeUrlResolving = true;
                    }

                    return null;
                }

                result.AddRange(response.Items);
                nextPageToken = response.NextPageToken;
            }

            nextPageToken = "";
            batch++;
            batchVideoIds = videoIds
                .Skip(batch * MaxSearchResults)
                .Take(MaxSearchResults);
        }

        return result;
    }
}