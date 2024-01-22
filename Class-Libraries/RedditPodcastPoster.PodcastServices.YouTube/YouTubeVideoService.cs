using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeVideoService(
    YouTubeService youTubeService,
    ILogger<YouTubeVideoService> logger)
    : IYouTubeVideoService
{
    private const int MaxSearchResults = 5;

    public async Task<IList<Video>?> GetVideoContentDetails(
        IEnumerable<string> videoIds,
        IndexingContext? indexingContext,
        bool withSnippets = false)
    {
        if (indexingContext is {SkipYouTubeUrlResolving: true})
        {
            logger.LogInformation(
                $"Skipping '{nameof(GetVideoContentDetails)}' as '{nameof(indexingContext.SkipYouTubeUrlResolving)}' is set. Video-ids: '{string.Join(",", videoIds)}'.");
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
                VideosResource.ListRequest request;
                var contentDetails = "contentDetails";
                if (withSnippets)
                {
                    contentDetails = "snippet," + contentDetails;
                }

                request = youTubeService.Videos.List(contentDetails);
                request.Id = string.Join(",", batchVideoIds);
                request.MaxResults = MaxSearchResults;
                VideoListResponse response;
                try
                {
                    response = await request.ExecuteAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Failed to use {nameof(youTubeService)}.");
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
            batchVideoIds = videoIds.Skip(batch * MaxSearchResults)
                .Take(MaxSearchResults);
        }

        return result;
    }
}