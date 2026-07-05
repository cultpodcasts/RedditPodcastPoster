using System.Xml;
using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.YouTube.Mapping;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Mapping;

public class YouTubeCatalogueInputMappingRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When YouTube video content details omit duration, catalogue mapping uses zero duration " +
        "because the provider filters zero-length videos downstream.")]
    public void Null_video_length_maps_to_zero_duration()
    {
        // Arrange
        var catalogueInput = _fixture.CreateYouTubeCatalogueInput();
        var searchResult = CreateSearchResult(catalogueInput);
        var videoDetails = CreateVideoDetails(catalogueInput, durationIso8601: null);

        // Act
        var input = searchResult.ToCatalogueInput(videoDetails, catalogueInput.Image);

        // Assert
        input.Duration.Should().Be(TimeSpan.Zero);
    }

    [Fact(DisplayName =
        "When the same YouTube video is mapped from search results or a playlist item, catalogue mapping produces equivalent inputs " +
        "because both retrieval paths feed the same adapter.")]
    public void Search_result_and_playlist_item_snippet_produce_equivalent_catalogue_input()
    {
        // Arrange
        var catalogueInput = _fixture.CreateYouTubeCatalogueInput();
        var searchResult = CreateSearchResult(catalogueInput);
        var playlistItemSnippet = CreatePlaylistItemSnippet(catalogueInput);
        var videoDetails = CreateVideoDetails(
            catalogueInput,
            durationIso8601: XmlConvert.ToString(catalogueInput.Duration));

        // Act
        var fromSearch = searchResult.ToCatalogueInput(videoDetails, catalogueInput.Image);
        var fromPlaylist = playlistItemSnippet.ToCatalogueInput(videoDetails, catalogueInput.Image);

        // Assert
        fromSearch.Should().BeEquivalentTo(fromPlaylist, options => options
            .ComparingByMembers<YouTubeCatalogueInput>());
    }

    private SearchResult CreateSearchResult(YouTubeCatalogueInput catalogueInput) =>
        new()
        {
            Id = new ResourceId { VideoId = catalogueInput.YouTubeId },
            Snippet = new SearchResultSnippet
            {
                Title = catalogueInput.Title,
                PublishedAtDateTimeOffset = catalogueInput.Release
            }
        };

    private static PlaylistItemSnippet CreatePlaylistItemSnippet(YouTubeCatalogueInput catalogueInput) =>
        new()
        {
            ResourceId = new ResourceId { VideoId = catalogueInput.YouTubeId },
            Title = catalogueInput.Title,
            PublishedAtDateTimeOffset = catalogueInput.Release
        };

    private static Google.Apis.YouTube.v3.Data.Video CreateVideoDetails(
        YouTubeCatalogueInput catalogueInput,
        string? durationIso8601) =>
        new()
        {
            Snippet = new VideoSnippet { Description = catalogueInput.Description },
            ContentDetails = new VideoContentDetails { Duration = durationIso8601 }
        };
}
