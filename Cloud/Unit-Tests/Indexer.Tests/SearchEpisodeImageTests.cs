using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;

namespace Indexer.Tests;

public class SearchEpisodeImageTests
{
    [Theory(DisplayName =
        "A standard YouTube thumbnail for this episode's video id compacts to a quality token that expands back to the same URL.")]
    [InlineData("maxresdefault.jpg", "yx")]
    [InlineData("sddefault.jpg", "ys")]
    [InlineData("hqdefault.jpg", "yh")]
    [InlineData("mqdefault.jpg", "ym")]
    [InlineData("default.jpg", "yd")]
    public void youtube_thumbnail_for_this_video_compacts_to_a_quality_token(string fileName, string expected)
    {
        // Arrange
        const string youTubeId = "griffinsong42";
        var episode = CatalogEpisode(
            youTubeId,
            youTubeImage: new Uri($"https://i.ytimg.com/vi/{youTubeId}/{fileName}"));

        // Act
        var result = SearchEpisodeImage.From(episode);

        // Assert
        result.Image.Should().Be(expected);
        SearchEpisodeImage.Expand(result.Image, youTubeId)
            .Should().Be($"https://i.ytimg.com/vi/{youTubeId}/{fileName}");
    }

    [Fact(DisplayName =
        "A YouTube thumbnail whose video id is not this episode's youtubeId stays a full URL, because compaction cannot rebuild it from nested ids.")]
    public void youtube_thumbnail_for_a_different_video_is_kept_as_full_url()
    {
        // Arrange
        const string youTubeId = "harbourvale99";
        var episode = CatalogEpisode(
            youTubeId,
            youTubeImage: new Uri("https://i.ytimg.com/vi/someoneelsevid/hqdefault.jpg"));

        // Act
        var result = SearchEpisodeImage.From(episode);

        // Assert
        result.Image.Should().Be("https://i.ytimg.com/vi/someoneelsevid/hqdefault.jpg");
    }

    [Fact(DisplayName =
        "A standard Spotify cover with no YouTube art compacts to an id token that expands back to the same URL.")]
    public void spotify_cover_compacts_to_id_token_when_no_youtube()
    {
        // Arrange
        var episode = CatalogEpisode(
            youTubeId: null,
            spotifyImage: new Uri("https://i.scdn.co/image/ab6765ferngully00cover"));

        // Act
        var result = SearchEpisodeImage.From(episode);

        // Assert
        result.Image.Should().Be("sab6765ferngully00cover");
        SearchEpisodeImage.Expand(result.Image, null)
            .Should().Be("https://i.scdn.co/image/ab6765ferngully00cover");
    }

    [Fact(DisplayName =
        "A Spotify cover URL with a query string is kept in full, because it is not the compactable i.scdn.co shape.")]
    public void spotify_cover_with_query_is_kept_as_full_url()
    {
        // Arrange
        var episode = CatalogEpisode(
            youTubeId: null,
            spotifyImage: new Uri("https://i.scdn.co/image/saltandcinder?sig=1"));

        // Act
        var result = SearchEpisodeImage.From(episode);

        // Assert
        result.Image.Should().Be("https://i.scdn.co/image/saltandcinder?sig=1");
    }

    [Fact(DisplayName =
        "Standard Apple artwork compacts to host digit plus path that expands back to the same URL.")]
    public void apple_artwork_compacts_to_host_digit_and_path()
    {
        // Arrange
        var episode = CatalogEpisode(
            youTubeId: null,
            appleImage: new Uri("https://is3-ssl.mzstatic.com/image/thumb/Music/draymoor/600x600bb.jpg"));

        // Act
        var result = SearchEpisodeImage.From(episode);

        // Assert
        result.Image.Should().Be("a3Music/draymoor/600x600bb.jpg");
        SearchEpisodeImage.Expand(result.Image, null)
            .Should().Be("https://is3-ssl.mzstatic.com/image/thumb/Music/draymoor/600x600bb.jpg");
    }

    [Fact(DisplayName =
        "Non-default catalog artwork is kept as a full URL, because it has no compact token grammar.")]
    public void other_catalog_art_is_kept_as_full_url()
    {
        // Arrange
        var episode = CatalogEpisode(
            youTubeId: "unused",
            otherImage: new Uri("https://feeds.example.test/art.jpg"));

        // Act
        var result = SearchEpisodeImage.From(episode);

        // Assert
        result.Image.Should().Be("https://feeds.example.test/art.jpg");
    }

    [Fact(DisplayName =
        "Search cover coalesces YouTube catalog art ahead of Spotify, Apple, and remaining ImageCoalesceOrder keys.")]
    public void coalesce_prefers_youtube_over_spotify_apple_other()
    {
        // Arrange
        const string youTubeId = "penington7";
        var episode = CatalogEpisode(
            youTubeId,
            youTubeImage: new Uri($"https://i.ytimg.com/vi/{youTubeId}/sddefault.jpg"),
            spotifyImage: new Uri("https://i.scdn.co/image/ignoredspotify"),
            appleImage: new Uri("https://is1-ssl.mzstatic.com/image/thumb/ignored/1bb.jpg"),
            otherImage: new Uri("https://other.example.test/ignored.jpg"));

        // Act
        var result = SearchEpisodeImage.From(episode);

        // Assert
        result.Image.Should().Be("ys");
    }

    [Fact(DisplayName =
        "When the episode has no catalog artwork, the search image is an empty string so incremental merge can clear a prior image.")]
    public void no_images_yields_empty_string_not_null()
    {
        // Arrange
        var episode = CatalogEpisode(youTubeId: "whitloe3");

        // Act
        var result = SearchEpisodeImage.From(episode);

        // Assert
        result.Image.Should().NotBeNull();
        result.Image.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Expand leaves full http URLs and empty strings unchanged.")]
    public void expand_returns_full_urls_and_empty_unchanged()
    {
        // Arrange
        const string fullUrl = "https://other.example.test/art.jpg";

        // Act
        var expandedUrl = SearchEpisodeImage.Expand(fullUrl, null);
        var expandedEmpty = SearchEpisodeImage.Expand(string.Empty, null);

        // Assert
        expandedUrl.Should().Be(fullUrl);
        expandedEmpty.Should().BeEmpty();
    }

    private static Episode CatalogEpisode(
        string? youTubeId,
        Uri? youTubeImage = null,
        Uri? spotifyImage = null,
        Uri? appleImage = null,
        Uri? otherImage = null)
    {
        var episode = new Episode();
        if (!string.IsNullOrWhiteSpace(youTubeId))
        {
            EpisodeServicePresence.SetYouTubeIdentity(episode, youTubeId);
        }

        EpisodeServicePresence.SetCatalogImage(episode, ServiceKeys.YouTube, youTubeImage);
        EpisodeServicePresence.SetCatalogImage(episode, ServiceKeys.Spotify, spotifyImage);
        EpisodeServicePresence.SetCatalogImage(episode, ServiceKeys.Apple, appleImage);
        if (otherImage is not null)
        {
            EpisodeServicePresence.SetCatalogImage(episode, ServiceKeys.Vimeo, otherImage);
        }

        return episode;
    }
}
