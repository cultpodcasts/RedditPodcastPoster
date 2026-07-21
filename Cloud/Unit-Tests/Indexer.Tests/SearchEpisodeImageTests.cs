using FluentAssertions;
using Indexer.Activities;
using Indexer.Models;
using Indexer.Orchestrations;
using Indexer.Services;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.Models.Episodes;
using Xunit;

namespace Indexer.Tests;

public class SearchEpisodeImageTests
{
    // image = youtube ?? spotify ?? apple ?? other, then loss-lessly compacted to a token the client
    // expands back to the exact same URL. Fictional names throughout.

    [Theory]
    [InlineData("maxresdefault.jpg", "yx")]
    [InlineData("sddefault.jpg", "ys")]
    [InlineData("hqdefault.jpg", "yh")]
    [InlineData("mqdefault.jpg", "ym")]
    [InlineData("default.jpg", "yd")]
    public void YouTube_thumbnail_for_this_video_compacts_to_a_quality_token(string fileName, string expected)
    {
        // "Marbury Vale Broadcasting" — every quality the resolver can pick is representable, so the
        // exact probed thumbnail is preserved (recorded, never re-picked).
        const string youTubeId = "griffinsong42";
        var images = new EpisodeImages
        {
            YouTube = new Uri($"https://i.ytimg.com/vi/{youTubeId}/{fileName}")
        };

        var result = SearchEpisodeImage.From(images, youTubeId);

        result.Image.Should().Be(expected);
        SearchEpisodeImage.Expand(result.Image, youTubeId)
            .Should().Be($"https://i.ytimg.com/vi/{youTubeId}/{fileName}");
    }

    [Fact]
    public void YouTube_thumbnail_for_a_different_video_is_kept_as_full_url()
    {
        // "Harbour & Vale" carries a thumbnail whose video id is NOT the episode's own youtubeId, so
        // it cannot be rebuilt from youtubeId — keep the full URL rather than lose it.
        var images = new EpisodeImages
        {
            YouTube = new Uri("https://i.ytimg.com/vi/someoneelsevid/hqdefault.jpg")
        };

        var result = SearchEpisodeImage.From(images, youTubeId: "harbourvale99");

        result.Image.Should().Be("https://i.ytimg.com/vi/someoneelsevid/hqdefault.jpg");
    }

    [Fact]
    public void Spotify_cover_compacts_to_id_token_when_no_youtube()
    {
        // "Ferngully Radio Hour" — only the fixed i.scdn.co prefix is dropped; the opaque id round-trips.
        var images = new EpisodeImages
        {
            Spotify = new Uri("https://i.scdn.co/image/ab6765ferngully00cover")
        };

        var result = SearchEpisodeImage.From(images, youTubeId: null);

        result.Image.Should().Be("sab6765ferngully00cover");
        SearchEpisodeImage.Expand(result.Image, null)
            .Should().Be("https://i.scdn.co/image/ab6765ferngully00cover");
    }

    [Fact]
    public void Spotify_cover_with_query_is_kept_as_full_url()
    {
        // A non-standard Spotify URL (query string) is not compacted — kept in full, nothing lost.
        var images = new EpisodeImages
        {
            Spotify = new Uri("https://i.scdn.co/image/saltandcinder?sig=1")
        };

        var result = SearchEpisodeImage.From(images, youTubeId: null);

        result.Image.Should().Be("https://i.scdn.co/image/saltandcinder?sig=1");
    }

    [Fact]
    public void Apple_artwork_compacts_to_host_digit_and_path()
    {
        // "Draymoor Audio Collective" — only the fixed prefix is dropped; the host digit and the deep
        // path (slashes included) are preserved verbatim.
        var images = new EpisodeImages
        {
            Apple = new Uri("https://is3-ssl.mzstatic.com/image/thumb/Music/draymoor/600x600bb.jpg")
        };

        var result = SearchEpisodeImage.From(images, youTubeId: null);

        result.Image.Should().Be("a3Music/draymoor/600x600bb.jpg");
        SearchEpisodeImage.Expand(result.Image, null)
            .Should().Be("https://is3-ssl.mzstatic.com/image/thumb/Music/draymoor/600x600bb.jpg");
    }

    [Fact]
    public void Other_art_is_kept_as_full_url()
    {
        // "Salt & Cinder" only carries a generic feed image — no known prefix, kept in full.
        var images = new EpisodeImages
        {
            Other = new Uri("https://feeds.saltandcinder.example/art.jpg")
        };

        var result = SearchEpisodeImage.From(images, youTubeId: "unused");

        result.Image.Should().Be("https://feeds.saltandcinder.example/art.jpg");
    }

    [Fact]
    public void Coalesce_prefers_youtube_over_spotify_apple_other()
    {
        const string youTubeId = "penington7";
        var images = new EpisodeImages
        {
            YouTube = new Uri($"https://i.ytimg.com/vi/{youTubeId}/sddefault.jpg"),
            Spotify = new Uri("https://i.scdn.co/image/ignoredspotify"),
            Apple = new Uri("https://is1-ssl.mzstatic.com/image/thumb/ignored/1bb.jpg"),
            Other = new Uri("https://other.example/ignored.jpg")
        };

        var result = SearchEpisodeImage.From(images, youTubeId);

        result.Image.Should().Be("ys");
    }

    [Fact]
    public void No_images_yields_empty_string_not_null()
    {
        // No images: image is an EMPTY STRING (not null) so a merge clears any image a prior version
        // of the document carried.
        var result = SearchEpisodeImage.From(images: null, youTubeId: "whitloe3");

        result.Image.Should().NotBeNull();
        result.Image.Should().BeEmpty();
    }

    [Fact]
    public void Expand_returns_full_urls_and_empty_unchanged()
    {
        SearchEpisodeImage.Expand("https://other.example/art.jpg", null)
            .Should().Be("https://other.example/art.jpg");
        SearchEpisodeImage.Expand(string.Empty, null).Should().BeEmpty();
    }
}
