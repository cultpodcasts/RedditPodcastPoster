using System.Text.Json;
using Azure.Core.Serialization;
using Azure.Search.Documents.Indexes;
using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Search.Formatting;
using RedditPodcastPoster.Search.Models;
using Xunit;

namespace Indexer.Tests;

public class PodcastEpisodeExtensionsTests
{
    [Fact]
    public void Maps_service_ids_language_and_compacts_youtube_image()
    {
        var episode = CreateEpisode();
        episode.Images = new EpisodeImages
        {
            YouTube = new Uri($"https://i.ytimg.com/vi/{episode.YouTubeId}/maxresdefault.jpg"),
            Spotify = new Uri("https://i.scdn.co/image/opaque")
        };
        episode.Language = null;
        var podcast = new Podcast
        {
            Name = " Podcast ",
            AppleId = 1234567890,
            Language = "es",
            SearchTerms = "podcast terms"
        };

        var result = new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord();

        result.SpotifyId.Should().Be("spotify-episode-id");
        result.YoutubeId.Should().Be("youtube-id");
        result.AppleId.Should().Be("987654321");
        result.PodcastAppleId.Should().Be("1234567890");
        result.Lang.Should().Be("es");
        // Image handling is owned by SearchEpisodeImage (see SearchEpisodeImageTests); this just
        // confirms ToEpisodeSearchRecord wires the youtubeId through so the selected maxresdefault
        // thumbnail is loss-lessly compacted to the "yx" token.
        result.Image.Should().Be("yx");
        result.Duration.Should().Be("00:02:03");
    }

    [Fact]
    public void Compacts_spotify_image_and_omits_empty_ids()
    {
        var episode = CreateEpisode();
        episode.SpotifyId = " ";
        episode.YouTubeId = string.Empty;
        episode.AppleId = null;
        episode.Images = new EpisodeImages
        {
            Spotify = new Uri("https://i.scdn.co/image/opaque")
        };

        var result = new PodcastEpisode(new Podcast { Name = "Podcast" }, episode)
            .ToEpisodeSearchRecord();

        result.SpotifyId.Should().BeNull();
        result.YoutubeId.Should().BeNull();
        result.AppleId.Should().BeNull();
        result.Image.Should().Be("sopaque");
    }

    [Fact]
    public void Truncates_long_description_on_word_boundary_with_ellipsis()
    {
        var episode = CreateEpisode();
        // 220 chars of complete words, then a partial token that would be mid-cut at 230.
        episode.Description = new string('a', 10) + " " + new string('b', 209) + " Salt Lake City continues";
        episode.Description.Length.Should().BeGreaterThan(Constants.DescriptionSize);

        var result = new PodcastEpisode(new Podcast { Name = "Podcast" }, episode)
            .ToEpisodeSearchRecord();

        result.EpisodeDescription.Length.Should().BeLessThanOrEqualTo(Constants.DescriptionSize);
        result.EpisodeDescription.Should().EndWith("\u2026");
        result.EpisodeDescription.Should().Contain("Salt");
        result.EpisodeDescription.Should().NotContain("Lake");
    }

    [Fact]
    public void Leaves_short_description_unchanged()
    {
        var episode = CreateEpisode();
        episode.Description = "  Short description.  ";

        var result = new PodcastEpisode(new Podcast { Name = "Podcast" }, episode)
            .ToEpisodeSearchRecord();

        result.EpisodeDescription.Should().Be("Short description.");
    }

    [Fact]
    public void Slim_schema_drops_explicit_and_hides_language()
    {
        var fields = new FieldBuilder
        {
            Serializer = new JsonObjectSerializer(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })
        }.Build(typeof(EpisodeSearchRecord));

        fields.Should().NotContain(field => field.Name == "explicit");
        fields.Should().Contain(field => field.Name == "spotifyId");
        fields.Should().Contain(field => field.Name == "youtubeId");
        fields.Should().Contain(field => field.Name == "appleId");
        fields.Should().Contain(field => field.Name == "podcastAppleId");

        var language = fields.Single(field => field.Name == "lang");
        language.IsFilterable.Should().BeTrue();
        language.IsFacetable.Should().BeTrue();
        language.IsHidden.Should().BeTrue();
    }

    private static Episode CreateEpisode() => new()
    {
        Id = Guid.NewGuid(),
        Title = " Episode ",
        Description = "Description",
        Release = new DateTime(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc),
        Length = TimeSpan.FromSeconds(123),
        SpotifyId = "spotify-episode-id",
        YouTubeId = "youtube-id",
        AppleId = 987654321,
        Subjects = ["subject"],
        SearchTerms = "episode terms"
    };
}
