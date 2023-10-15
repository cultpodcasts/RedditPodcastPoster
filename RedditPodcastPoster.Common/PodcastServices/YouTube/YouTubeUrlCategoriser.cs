using System.Xml;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.UrlCategorisation;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.PodcastServices.YouTube;

public class YouTubeUrlCategoriser : IYouTubeUrlCategoriser
{
    private readonly ILogger<YouTubeUrlCategoriser> _logger;
    private readonly IYouTubeChannelService _youTubeChannelService;
    private readonly IYouTubeIdExtractor _youTubeIdExtractor;
    private readonly IYouTubeVideoService _youTubeVideoService;

    public YouTubeUrlCategoriser(
        IYouTubeChannelService youTubeChannelService,
        IYouTubeVideoService youTubeVideoService,
        IYouTubeIdExtractor youTubeIdExtractor,
        ILogger<YouTubeUrlCategoriser> logger)
    {
        _youTubeChannelService = youTubeChannelService;
        _youTubeVideoService = youTubeVideoService;
        _youTubeIdExtractor = youTubeIdExtractor;
        _logger = logger;
    }

    public bool IsMatch(Uri url)
    {
        return url.Host.ToLower().Contains("youtube");
    }

    public async Task<ResolvedYouTubeItem?> Resolve(IList<Podcast> podcasts, Uri url, IndexingContext indexingContext)
    {
        var pair = podcasts
            .SelectMany(podcast => podcast.Episodes, (podcast, episode) => new PodcastEpisodePair(podcast, episode))
            .FirstOrDefault(pair => pair.Episode.Urls.YouTube == url);

        if (pair != null)
        {
            return new ResolvedYouTubeItem(pair);
        }

        var videoId = _youTubeIdExtractor.Extract(url);
        if (videoId == null)
        {
            throw new InvalidOperationException($"Unable to find video-id in url '{url}'.");
        }

        var items = await _youTubeVideoService.GetVideoContentDetails(new[] {videoId}, indexingContext, true);
        if (items != null)
        {
            var item = items.FirstOrDefault();
            if (item == null)
            {
                throw new InvalidOperationException($"Unable to find video with id '{videoId}'.");
            }

            var channel =
                await _youTubeChannelService.GetChannelContentDetails(new YouTubeChannelId(item.Snippet.ChannelId),
                    indexingContext, true, true);
            if (channel != null)
            {
                return new ResolvedYouTubeItem(
                    item.Snippet.ChannelId,
                    item.Id,
                    item.Snippet.ChannelTitle,
                    channel!.Snippet.Description,
                    channel.ContentOwnerDetails.ContentOwner,
                    item.Snippet.Title,
                    item.Snippet.Description,
                    item.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
                    XmlConvert.ToTimeSpan(item.ContentDetails.Duration),
                    item.ToYouTubeUrl(),
                    item.ContentDetails.ContentRating.YtRating == "ytAgeRestricted"
                );
            }
        }
        else
        {
            if (indexingContext.SkipYouTubeUrlResolving)
            {
                throw new InvalidOperationException(
                    $"Error: {nameof(indexingContext.SkipYouTubeUrlResolving)} be true.");
            }
        }

        return null;
    }

    public Task<ResolvedYouTubeItem?> Resolve(PodcastServiceSearchCriteria criteria, Podcast? matchingPodcast,
        IndexingContext indexingContext)
    {
        return Task.FromResult((ResolvedYouTubeItem) null!)!;
    }
}