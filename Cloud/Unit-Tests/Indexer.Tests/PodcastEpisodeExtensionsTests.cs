using System.Text.Json;
using Azure.Core.Serialization;
using Azure.Search.Documents.Indexes;
using FluentAssertions;
using Indexer.Activities;
using Indexer.Models;
using Indexer.Orchestrations;
using Indexer.Services;
using Xunit;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Search.Formatting;
using RedditPodcastPoster.Search.Models;

namespace Indexer.Tests;

public class PodcastEpisodeExtensionsTests
{
    [Fact(DisplayName =
        "ToEpisodeSearchRecord maps platform ids from the episode language only, and wires YouTube image " +
        "compaction through the youtubeId.")]
    public void Maps_service_ids_language_and_compacts_youtube_image()
    {
        // Arrange
        var episode = CreateEpisode();
        episode.Images = new EpisodeImages
        {
            YouTube = new Uri($"https://i.ytimg.com/vi/{episode.YouTubeId}/maxresdefault.jpg"),
            Spotify = new Uri("https://i.scdn.co/image/opaque")
        };
        episode.Language = "es";
        var podcast = new Podcast
        {
            Name = " Podcast ",
            AppleId = 1234567890,
            Language = "fr",
            SearchTerms = "podcast terms"
        };

        // Act
        var result = new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord();

        // Assert
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

    [Fact(DisplayName =
        "INTEGRITY: ToEpisodeSearchRecord leaves Lang null when Episode.Language is null (product English) even if the " +
        "podcast has a non-English default, because search English is lang eq null and coalescing to podcast.Language " +
        "would exclude curated English episodes of non-English shows from the English subject filter.")]
    public void Leaves_lang_null_when_episode_language_unset_despite_podcast_default()
    {
        // Arrange
        var episode = CreateEpisode();
        episode.Language = null;
        var podcast = new Podcast { Name = "Podcast", Language = "fil" };

        // Act
        var result = new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord();

        // Assert
        result.Lang.Should().BeNull();
    }

    [Fact(DisplayName =
        "ToEpisodeSearchRecord omits blank platform ids and compacts a Spotify-only image.")]
    public void Compacts_spotify_image_and_omits_empty_ids()
    {
        // Arrange
        var episode = CreateEpisode();
        episode.SpotifyId = " ";
        EpisodeServicePresence.SetYouTubeIdentity(episode, null);
        EpisodeServicePresence.SetAppleIdentity(episode, null);
        episode.Images = new EpisodeImages
        {
            Spotify = new Uri("https://i.scdn.co/image/opaque")
        };

        // Act
        var result = new PodcastEpisode(new Podcast { Name = "Podcast" }, episode)
            .ToEpisodeSearchRecord();

        // Assert
        result.SpotifyId.Should().BeNull();
        result.YoutubeId.Should().BeNull();
        result.AppleId.Should().BeNull();
        result.Image.Should().Be("sopaque");
    }

    [Fact(DisplayName =
        "ToEpisodeSearchRecord truncates long descriptions on a word boundary and appends an ellipsis.")]
    public void Truncates_long_description_on_word_boundary_with_ellipsis()
    {
        // Arrange
        var episode = CreateEpisode();
        // Filler plus " Alpha" fits inside DescriptionSize; " Bravo" is the first word past the cut.
        episode.Description = new string('a', 10) + " " + new string('b', 161) + " Alpha Bravo continues";
        episode.Description.Length.Should().BeGreaterThan(Constants.DescriptionSize);

        // Act
        var result = new PodcastEpisode(new Podcast { Name = "Podcast" }, episode)
            .ToEpisodeSearchRecord();

        // Assert
        result.EpisodeDescription.Length.Should().BeLessThanOrEqualTo(Constants.DescriptionSize);
        result.EpisodeDescription.Should().EndWith("\u2026");
        result.EpisodeDescription.Should().Contain("Alpha");
        result.EpisodeDescription.Should().NotContain("Bravo");
    }

    [Fact(DisplayName =
        "ToEpisodeSearchRecord trims short descriptions without truncating or appending an ellipsis.")]
    public void Leaves_short_description_unchanged()
    {
        // Arrange
        var episode = CreateEpisode();
        episode.Description = "  Short description.  ";

        // Act
        var result = new PodcastEpisode(new Podcast { Name = "Podcast" }, episode)
            .ToEpisodeSearchRecord();

        // Assert
        result.EpisodeDescription.Should().Be("Short description.");
    }

    [Fact(DisplayName =
        "Slim EpisodeSearchRecord schema drops explicit, keeps platform ids, and leaves lang " +
        "filterable+facetable and retrievable for search/flix language flags.")]
    public void Slim_schema_drops_explicit_and_keeps_lang_retrievable()
    {
        // Arrange
        var builder = new FieldBuilder
        {
            Serializer = new JsonObjectSerializer(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })
        };

        // Act
        var fields = builder.Build(typeof(EpisodeSearchRecord));

        // Assert
        fields.Should().NotContain(field => field.Name == "explicit");
        fields.Should().Contain(field => field.Name == "spotifyId");
        fields.Should().Contain(field => field.Name == "youtubeId");
        fields.Should().Contain(field => field.Name == "appleId");
        fields.Should().Contain(field => field.Name == "podcastAppleId");
        fields.Should().Contain(field => field.Name == "svc");

        var language = fields.Single(field => field.Name == "lang");
        language.IsFilterable.Should().BeTrue();
        language.IsFacetable.Should().BeTrue();
        language.IsHidden.Should().BeFalse();
    }

    private static Episode CreateEpisode() => new()
    {
        Id = Guid.NewGuid(),
        Title = " Episode ",
        Description = "Description",
        Release = DateTime.UtcNow.Date.AddDays(-9).AddHours(12),
        Length = TimeSpan.FromSeconds(123),
        SpotifyId = "spotify-episode-id",
        YouTubeId = "youtube-id",
        AppleId = 987654321,
        Subjects = ["subject"],
        SearchTerms = "episode terms"
    };
}
