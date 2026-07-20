using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Models;
using Xunit;

namespace Indexer.Tests;

public class SearchEpisodeImageTests
{
    // image = youtube ?? spotify ?? apple ?? other, with YouTube and Spotify standard covers
    // compacted to a token the client re-expands.

    [Fact]
    public void Compactable_youtube_thumbnail_becomes_variant_with_empty_image()
    {
        // "Marbury Vale Broadcasting" — a standard i.ytimg thumbnail wins and is compacted. The
        // variant is taken from the filename AS-IS (sd here), not re-evaluated to a higher quality.
        var images = new EpisodeImages
        {
            YouTube = new Uri("https://i.ytimg.com/vi/griffinsong42/sddefault.jpg"),
            Spotify = new Uri("https://i.scdn.co/image/staleSpotifyCover"),
            Apple = new Uri("https://is1-ssl.mzstatic.com/image/thumb/apple/600x600bb.jpg"),
            Other = new Uri("https://other.example/other")
        };

        var result = SearchEpisodeImage.From(images, "griffinsong42");

        result.Image.Should().BeEmpty();
        result.YoutubeImageVariant.Should().Be("sd");
        result.SpotifyImageId.Should().BeEmpty();
    }

    [Theory]
    [InlineData("maxresdefault.jpg", "maxres")]
    [InlineData("sddefault.jpg", "sd")]
    [InlineData("hqdefault.jpg", "hq")]
    public void Youtube_variant_is_preserved_exactly_as_in_the_url(string filename, string expectedVariant)
    {
        var images = new EpisodeImages
        {
            YouTube = new Uri($"https://i.ytimg.com/vi/pinewick77/{filename}")
        };

        var result = SearchEpisodeImage.From(images, "pinewick77");

        result.YoutubeImageVariant.Should().Be(expectedVariant);
        result.Image.Should().BeEmpty();
    }

    [Fact]
    public void Non_standard_youtube_image_keeps_full_url()
    {
        // "Copperfield Hour" — a non-standard YouTube thumbnail filename is not compactable, so the
        // full URL is kept and no variant is emitted.
        var images = new EpisodeImages
        {
            YouTube = new Uri("https://i.ytimg.com/vi/copperfield/mqdefault.jpg")
        };

        var result = SearchEpisodeImage.From(images, "copperfield");

        result.Image.Should().Be("https://i.ytimg.com/vi/copperfield/mqdefault.jpg");
        result.YoutubeImageVariant.Should().BeEmpty();
        result.SpotifyImageId.Should().BeEmpty();
    }

    [Fact]
    public void Youtube_thumbnail_for_a_different_video_keeps_full_url()
    {
        // The thumbnail path must match THIS episode's youTubeId to be compacted.
        var images = new EpisodeImages
        {
            YouTube = new Uri("https://i.ytimg.com/vi/someoneelse/maxresdefault.jpg")
        };

        var result = SearchEpisodeImage.From(images, "griffinsong42");

        result.Image.Should().Be("https://i.ytimg.com/vi/someoneelse/maxresdefault.jpg");
        result.YoutubeImageVariant.Should().BeEmpty();
    }

    [Fact]
    public void Compactable_spotify_cover_becomes_id_with_empty_image()
    {
        // "Harbour & Vale" has no YouTube image; its standard i.scdn.co cover compacts to the id.
        var images = new EpisodeImages
        {
            Spotify = new Uri("https://i.scdn.co/image/ab6765630000ba8aharbourvale"),
            Apple = new Uri("https://is1-ssl.mzstatic.com/image/thumb/apple/600x600bb.jpg"),
            Other = new Uri("https://other.example/other")
        };

        var result = SearchEpisodeImage.From(images, youTubeId: null);

        result.Image.Should().BeEmpty();
        result.SpotifyImageId.Should().Be("ab6765630000ba8aharbourvale");
        result.YoutubeImageVariant.Should().BeEmpty();
    }

    [Fact]
    public void Non_standard_spotify_cover_keeps_full_url()
    {
        // A Spotify URL with a query string is not losslessly reversible, so it is kept in full.
        var images = new EpisodeImages
        {
            Spotify = new Uri("https://i.scdn.co/image/tidewater?size=large")
        };

        var result = SearchEpisodeImage.From(images, youTubeId: null);

        result.Image.Should().Be("https://i.scdn.co/image/tidewater?size=large");
        result.SpotifyImageId.Should().BeEmpty();
    }

    [Fact]
    public void Apple_cover_is_never_compacted()
    {
        // "Ferngully Radio Hour" only has Apple/other art. Apple URLs are never compacted.
        var images = new EpisodeImages
        {
            Apple = new Uri("https://is1-ssl.mzstatic.com/image/thumb/Podcasts/ferngully/600x600bb.jpg"),
            Other = new Uri("https://other.example/other")
        };

        var result = SearchEpisodeImage.From(images, youTubeId: null);

        result.Image.Should().Be("https://is1-ssl.mzstatic.com/image/thumb/Podcasts/ferngully/600x600bb.jpg");
        result.YoutubeImageVariant.Should().BeEmpty();
        result.SpotifyImageId.Should().BeEmpty();
    }

    [Fact]
    public void Other_wins_when_it_is_the_only_image()
    {
        // "Salt & Cinder" only carries a generic feed image.
        var images = new EpisodeImages
        {
            Other = new Uri("https://feeds.saltandcinder.example/art.jpg")
        };

        var result = SearchEpisodeImage.From(images, youTubeId: null);

        result.Image.Should().Be("https://feeds.saltandcinder.example/art.jpg");
        result.YoutubeImageVariant.Should().BeEmpty();
        result.SpotifyImageId.Should().BeEmpty();
    }

    [Fact]
    public void No_images_yields_empty_strings_not_null()
    {
        // "Draymoor Audio Collective" has no images: every projected value is an EMPTY STRING (not
        // null) so a merge clears whatever a prior version of the document carried.
        var result = SearchEpisodeImage.From(images: null, youTubeId: "draymoor");

        result.Image.Should().BeEmpty();
        result.YoutubeImageVariant.Should().BeEmpty();
        result.SpotifyImageId.Should().BeEmpty();
    }
}
