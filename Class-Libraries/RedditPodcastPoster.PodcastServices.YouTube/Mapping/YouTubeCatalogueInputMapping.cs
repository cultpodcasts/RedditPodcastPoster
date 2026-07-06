using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;

namespace RedditPodcastPoster.PodcastServices.YouTube.Mapping;

public static class YouTubeCatalogueInputMapping
{
    public static YouTubeCatalogueInput ToCatalogueInput(
        this SearchResult searchResult,
        Google.Apis.YouTube.v3.Data.Video videoDetails,
        Uri? image) =>
        new(
            searchResult.Id.VideoId,
            searchResult.Snippet.Title.Trim(),
            videoDetails.Snippet.Description.Trim(),
            videoDetails.GetLength() ?? TimeSpan.Zero,
            searchResult.Snippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
            searchResult.ToYouTubeUrl(),
            image);

    public static YouTubeCatalogueInput ToCatalogueInput(
        this PlaylistItemSnippet playlistItemSnippet,
        Google.Apis.YouTube.v3.Data.Video videoDetails,
        Uri? image) =>
        new(
            playlistItemSnippet.ResourceId.VideoId,
            playlistItemSnippet.Title.Trim(),
            videoDetails.Snippet.Description.Trim(),
            videoDetails.GetLength() ?? TimeSpan.Zero,
            playlistItemSnippet.PublishedAtDateTimeOffset!.Value.UtcDateTime,
            playlistItemSnippet.ToYouTubeUrl(),
            image);
}
