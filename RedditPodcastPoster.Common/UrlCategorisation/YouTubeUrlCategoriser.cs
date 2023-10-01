using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Common.PodcastServices.YouTube;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Common.UrlCategorisation;

public class YouTubeUrlCategoriser : IYouTubeUrlCategoriser
{
    private static readonly Regex VideoId = new(@"v=(?'videoId'[\-\w]+)", RegexOptions.Compiled);
    private readonly ILogger<YouTubeUrlCategoriser> _logger;

    private readonly IYouTubeSearchService _youTubeSearchService;

    public YouTubeUrlCategoriser(
        IYouTubeSearchService youTubeSearchService,
        ILogger<YouTubeUrlCategoriser> logger)
    {
        _youTubeSearchService = youTubeSearchService;
        _logger = logger;
    }

    public bool IsMatch(Uri url)
    {
        return url.Host.ToLower().Contains("youtube");
    }

    public async Task<ResolvedYouTubeItem?> Resolve(List<Podcast> podcasts, Uri url, IndexingContext indexingContext)
    {
        var pair = podcasts
            .SelectMany(podcast => podcast.Episodes, (podcast, episode) => new PodcastEpisodePair(podcast, episode))
            .FirstOrDefault(pair => pair.Episode.Urls.YouTube == url);

        if (pair != null)
        {
            return new ResolvedYouTubeItem(pair);
        }

        var videoIdMatch = VideoId.Match(url.ToString()).Groups["videoId"];
        if (!videoIdMatch.Success)
        {
            throw new InvalidOperationException($"Unable to find video-id in url '{url}'.");
        }

        var items = await _youTubeSearchService.GetVideoDetails(new[] {videoIdMatch.Value}, indexingContext);
        var item = items.FirstOrDefault();
        if (item == null)
        {
            throw new InvalidOperationException($"Unable to find video with id '{videoIdMatch.Value}'.");
        }

        var channel =
            await _youTubeSearchService.GetChannel(new YouTubeChannelId(item.Snippet.ChannelId), indexingContext);
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

        return null;
    }

    public Task<ResolvedYouTubeItem?> Resolve(PodcastServiceSearchCriteria criteria, Podcast? matchingPodcast,
        IndexingContext indexingContext)
    {
        return Task.FromResult((ResolvedYouTubeItem) null!)!;
    }
}