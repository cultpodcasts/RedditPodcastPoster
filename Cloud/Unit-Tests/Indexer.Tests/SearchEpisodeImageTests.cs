using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Models;
using Xunit;

namespace Indexer.Tests;

public class SearchEpisodeImageTests
{
    // image = youtube ?? spotify ?? apple ?? other

    [Fact]
    public void YouTube_wins_when_present()
    {
        // "The Gilded Compass" only publishes on a non-ytimg host, so its full URL is kept.
        var images = new EpisodeImages
        {
            YouTube = new Uri("https://cdn.gildedcompass.example/cover.png"),
            Spotify = new Uri("https://i.scdn.co/image/spotify"),
            Apple = new Uri("https://is1-ssl.mzstatic.com/apple"),
            Other = new Uri("https://other.example/other")
        };

        var result = SearchEpisodeImage.From(images, "gildedcompass");

        result.Image.Should().Be("https://cdn.gildedcompass.example/cover.png");
        result.YoutubeImageVariant.Should().BeNull();
    }

    [Fact]
    public void Spotify_wins_when_no_youtube()
    {
        // "Harbour & Vale" has no YouTube image, so Spotify is next.
        var images = new EpisodeImages
        {
            Spotify = new Uri("https://i.scdn.co/image/harbourvale"),
            Apple = new Uri("https://is1-ssl.mzstatic.com/apple"),
            Other = new Uri("https://other.example/other")
        };

        var result = SearchEpisodeImage.From(images, "harbourvale");

        result.Image.Should().Be("https://i.scdn.co/image/harbourvale");
        result.YoutubeImageVariant.Should().BeNull();
    }

    [Fact]
    public void Apple_wins_when_no_youtube_or_spotify()
    {
        // "Ferngully Radio Hour" only has Apple and other art.
        var images = new EpisodeImages
        {
            Apple = new Uri("https://is1-ssl.mzstatic.com/ferngully"),
            Other = new Uri("https://other.example/other")
        };

        var result = SearchEpisodeImage.From(images, "ferngully");

        result.Image.Should().Be("https://is1-ssl.mzstatic.com/ferngully");
        result.YoutubeImageVariant.Should().BeNull();
    }

    [Fact]
    public void Other_wins_when_it_is_the_only_image()
    {
        // "Salt & Cinder" only carries a generic feed image.
        var images = new EpisodeImages
        {
            Other = new Uri("https://feeds.saltandcinder.example/art.jpg")
        };

        var result = SearchEpisodeImage.From(images, "saltcinder");

        result.Image.Should().Be("https://feeds.saltandcinder.example/art.jpg");
        result.YoutubeImageVariant.Should().BeNull();
    }

    [Theory]
    [InlineData("maxresdefault.jpg", "maxres")]
    [InlineData("sddefault.jpg", "sd")]
    [InlineData("hqdefault.jpg", "hq")]
    public void Standard_youtube_thumbnail_is_compacted_to_variant_with_empty_image(
        string file, string expectedVariant)
    {
        // "Marbury Vale Broadcasting" gets a standard i.ytimg thumbnail for its own video: the URL
        // is dropped (client rebuilds it) and image is an EMPTY STRING so an Azure Search merge
        // clears any stale cover art already in the index.
        const string youTubeId = "griffinsong42";
        var images = new EpisodeImages
        {
            YouTube = new Uri($"https://i.ytimg.com/vi/{youTubeId}/{file}"),
            Spotify = new Uri("https://i.scdn.co/image/staleSpotifyCover")
        };

        var result = SearchEpisodeImage.From(images, youTubeId);

        result.Image.Should().NotBeNull();
        result.Image.Should().BeEmpty();
        result.YoutubeImageVariant.Should().Be(expectedVariant);
    }

    [Fact]
    public void Non_standard_youtube_thumbnail_file_is_not_compacted()
    {
        // A "default.jpg" thumbnail is not one of the derivable variants, so the URL is kept.
        const string youTubeId = "quillhaven";
        var images = new EpisodeImages
        {
            YouTube = new Uri($"https://i.ytimg.com/vi/{youTubeId}/default.jpg")
        };

        var result = SearchEpisodeImage.From(images, youTubeId);

        result.Image.Should().Be($"https://i.ytimg.com/vi/{youTubeId}/default.jpg");
        result.YoutubeImageVariant.Should().BeNull();
    }

    [Fact]
    public void Youtube_thumbnail_for_a_different_video_id_is_not_compacted()
    {
        // The thumbnail belongs to another video, so it cannot be rebuilt from this episode's id.
        var images = new EpisodeImages
        {
            YouTube = new Uri("https://i.ytimg.com/vi/someOtherVideo/maxresdefault.jpg")
        };

        var result = SearchEpisodeImage.From(images, "quillhaven");

        result.Image.Should().Be("https://i.ytimg.com/vi/someOtherVideo/maxresdefault.jpg");
        result.YoutubeImageVariant.Should().BeNull();
    }

    [Fact]
    public void Non_ytimg_youtube_url_is_not_compacted()
    {
        // "Thornby & Fennwick Media" uses a custom host, so the full URL lands on image.
        var images = new EpisodeImages
        {
            YouTube = new Uri("https://images.thornbyfennwick.example/quillhaven/custom-art.png")
        };

        var result = SearchEpisodeImage.From(images, "quillhaven");

        result.Image.Should().Be("https://images.thornbyfennwick.example/quillhaven/custom-art.png");
        result.YoutubeImageVariant.Should().BeNull();
    }

    [Fact]
    public void Standard_youtube_thumbnail_without_a_youtube_id_is_not_compacted()
    {
        // Without a youTubeId the client cannot rebuild the URL, so keep it.
        var images = new EpisodeImages
        {
            YouTube = new Uri("https://i.ytimg.com/vi/griffinsong42/maxresdefault.jpg")
        };

        var result = SearchEpisodeImage.From(images, youTubeId: null);

        result.Image.Should().Be("https://i.ytimg.com/vi/griffinsong42/maxresdefault.jpg");
        result.YoutubeImageVariant.Should().BeNull();
    }

    [Fact]
    public void No_images_yields_empty_string_and_no_variant()
    {
        // "Draymoor Audio Collective" has no images: image is an EMPTY STRING (not null) so a merge
        // clears any image a prior version of the document may have carried.
        var result = SearchEpisodeImage.From(images: null, "draymoor");

        result.Image.Should().NotBeNull();
        result.Image.Should().BeEmpty();
        result.YoutubeImageVariant.Should().BeNull();
    }
}
