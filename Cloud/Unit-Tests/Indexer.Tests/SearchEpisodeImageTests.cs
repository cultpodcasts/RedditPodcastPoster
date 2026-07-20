using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer;
using RedditPodcastPoster.Models;
using Xunit;

namespace Indexer.Tests;

public class SearchEpisodeImageTests
{
    // image = youtube ?? spotify ?? apple ?? other, stored as-is.

    [Fact]
    public void YouTube_wins_and_its_url_is_stored_as_is()
    {
        // "Marbury Vale Broadcasting" — a standard i.ytimg thumbnail wins and is kept verbatim.
        // The projection must NOT inspect the thumbnail quality or rewrite the URL.
        var images = new EpisodeImages
        {
            YouTube = new Uri("https://i.ytimg.com/vi/griffinsong42/maxresdefault.jpg"),
            Spotify = new Uri("https://i.scdn.co/image/staleSpotifyCover"),
            Apple = new Uri("https://is1-ssl.mzstatic.com/apple"),
            Other = new Uri("https://other.example/other")
        };

        var result = SearchEpisodeImage.From(images);

        result.Image.Should().Be("https://i.ytimg.com/vi/griffinsong42/maxresdefault.jpg");
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

        var result = SearchEpisodeImage.From(images);

        result.Image.Should().Be("https://i.scdn.co/image/harbourvale");
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

        var result = SearchEpisodeImage.From(images);

        result.Image.Should().Be("https://is1-ssl.mzstatic.com/ferngully");
    }

    [Fact]
    public void Other_wins_when_it_is_the_only_image()
    {
        // "Salt & Cinder" only carries a generic feed image.
        var images = new EpisodeImages
        {
            Other = new Uri("https://feeds.saltandcinder.example/art.jpg")
        };

        var result = SearchEpisodeImage.From(images);

        result.Image.Should().Be("https://feeds.saltandcinder.example/art.jpg");
    }

    [Fact]
    public void No_images_yields_empty_string_not_null()
    {
        // "Draymoor Audio Collective" has no images: image is an EMPTY STRING (not null) so a merge
        // clears any image a prior version of the document may have carried.
        var result = SearchEpisodeImage.From(images: null);

        result.Image.Should().NotBeNull();
        result.Image.Should().BeEmpty();
    }
}
