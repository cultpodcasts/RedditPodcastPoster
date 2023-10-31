using System.Text.Json;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices.YouTube;

public class YouTubeVideoService : IYouTubeVideoService
{
    private const int MaxSearchResults = 5;
    private readonly ILogger<YouTubeVideoService> _logger;
    private readonly YouTubeService _youTubeService;

    public YouTubeVideoService(YouTubeService youTubeService,
        ILogger<YouTubeVideoService> logger)
    {
        _youTubeService = youTubeService;
        _logger = logger;
    }

    public async Task<IList<Video>?> GetVideoContentDetails(
        IEnumerable<string> videoIds,
        IndexingContext? indexingContext,
        bool withSnippets = false)
    {
        if (indexingContext.SkipYouTubeUrlResolving)
        {
            _logger.LogInformation(
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
                var contentdetails = "contentDetails";
                if (withSnippets)
                {
                    contentdetails = "snippet," + contentdetails;
                }

                request = _youTubeService.Videos.List(contentdetails);
                request.Id = string.Join(",", batchVideoIds);
                request.MaxResults = MaxSearchResults;
                VideoListResponse response;
                try
                {
                    response = await request.ExecuteAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to use {nameof(_youTubeService)}.");
                    indexingContext.SkipYouTubeUrlResolving = true;
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

        if (result.Any())
        {
            _logger.LogInformation($"YOUTUBE: {nameof(GetVideoContentDetails)} - {JsonSerializer.Serialize(result)}");
        }

        return result;
    }
}