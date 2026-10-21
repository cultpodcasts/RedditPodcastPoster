using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.Bluesky.Client;

public class BlueskyPlatformCardSource(
    IYouTubeVideoService youTubeVideoService,
    IYouTubeServiceWrapper youTubeService,
    IYouTubeThumbnailResolver youTubeThumbnailResolver,
    ISpotifyClientWrapper spotifyClient,
    ILogger<BlueskyPlatformCardSource> logger) : IBlueskyPlatformCardSource
{
    public async Task<BlueskyPlatformCard?> TryGetAsync(Uri url, CancellationToken cancellationToken = default)
    {
        if (IsYouTube(url))
        {
            return await TryYouTubeAsync(url, cancellationToken).ConfigureAwait(false);
        }

        if (IsSpotify(url))
        {
            return await TrySpotifyAsync(url, cancellationToken).ConfigureAwait(false);
        }

        logger.LogWarning(
            "Bluesky embed card has no API for host '{host}'. Posting without a card.",
            url.Host);
        return null;
    }

    private async Task<BlueskyPlatformCard?> TryYouTubeAsync(Uri url, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var videoId = YouTubeIdResolver.Extract(url);
        if (string.IsNullOrWhiteSpace(videoId))
        {
            logger.LogWarning(
                "Bluesky YouTube URL had no video id for '{url}'. Posting without a card.",
                url);
            return null;
        }

        IList<Video>? videos;
        try
        {
            videos = await youTubeVideoService.GetVideoContentDetails(
                youTubeService,
                [videoId],
                new IndexingContext(),
                withSnippets: true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Bluesky YouTube card lookup failed for '{url}'. Posting without a card.", url);
            return null;
        }

        var video = videos?.FirstOrDefault();
        if (video?.Snippet is null || string.IsNullOrWhiteSpace(video.Snippet.Title))
        {
            logger.LogWarning(
                "Bluesky YouTube card lookup returned no snippet for '{url}'. Posting without a card.",
                url);
            return null;
        }

        var imageUrls = youTubeThumbnailResolver.GetUsableCandidateUrls(video);
        if (imageUrls.Count == 0)
        {
            logger.LogWarning(
                "Bluesky YouTube card lookup returned no image for '{url}'. Posting without a card.",
                url);
            return null;
        }

        return new BlueskyPlatformCard(
            url,
            BlueskyEmbedText.Truncate(video.Snippet.Title, BlueskyEmbedText.MaxTitleLength),
            BlueskyEmbedText.Truncate(video.Snippet.Description, BlueskyEmbedText.MaxDescriptionLength),
            imageUrls);
    }

    private async Task<BlueskyPlatformCard?> TrySpotifyAsync(Uri url, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var episodeId = SpotifyIdResolver.GetEpisodeId(url);
        if (string.IsNullOrWhiteSpace(episodeId))
        {
            logger.LogWarning(
                "Bluesky Spotify URL had no episode id for '{url}'. Posting without a card.",
                url);
            return null;
        }

        FullEpisode? episode;
        try
        {
            episode = await spotifyClient.GetFullEpisode(
                episodeId,
                new EpisodeRequest { Market = Market.CountryCode },
                new IndexingContext(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Bluesky Spotify card lookup failed for '{url}'. Posting without a card.", url);
            return null;
        }

        if (episode is null || string.IsNullOrWhiteSpace(episode.Name))
        {
            logger.LogWarning(
                "Bluesky Spotify card lookup returned no episode for '{url}'. Posting without a card.",
                url);
            return null;
        }

        var imageUrls = LargestImageUrls(episode);
        if (imageUrls.Count == 0)
        {
            logger.LogWarning(
                "Bluesky Spotify card lookup returned no image for '{url}'. Posting without a card.",
                url);
            return null;
        }

        return new BlueskyPlatformCard(
            url,
            BlueskyEmbedText.Truncate(episode.Name, BlueskyEmbedText.MaxTitleLength),
            BlueskyEmbedText.Truncate(episode.Description, BlueskyEmbedText.MaxDescriptionLength),
            imageUrls);
    }

    private static IReadOnlyList<Uri> LargestImageUrls(FullEpisode episode)
    {
        if (episode.Images is null || episode.Images.Count == 0)
        {
            return [];
        }

        return episode.Images
            .Where(image => image is not null && !string.IsNullOrWhiteSpace(image.Url))
            .OrderByDescending(image => image.Height)
            .Select(image => new Uri(image.Url))
            .ToArray();
    }

    private static bool IsYouTube(Uri url)
    {
        var host = url.IdnHost;
        return host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase) ||
               host.Equals("youtube.com", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".youtube.com", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSpotify(Uri url)
    {
        var host = url.IdnHost;
        return host.Equals("spotify.com", StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith(".spotify.com", StringComparison.OrdinalIgnoreCase);
    }
}
