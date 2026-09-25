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
        var kept = new string('a', Constants.DescriptionSize - 24) + " Alpha";
        episode.Description = kept + " BravoContinuesPastTheCut";
        episode.Description.Length.Should().BeGreaterThan(Constants.DescriptionSize);

        // Act
        var result = new PodcastEpisode(new Podcast { Name = "Podcast" }, episode)
            .ToEpisodeSearchRecord();

        // Assert
        result.Description.Should().NotBeNull();
        result.Description!.Length.Should().BeLessThanOrEqualTo(Constants.DescriptionSize);
        result.Description.Should().EndWith("\u2026");
        result.Description.Should().Contain("Alpha");
        result.Description.Should().NotContain("Bravo");
        result.EpisodeDescription.Should().BeNull();
    }

    [Fact(DisplayName =
        "ToEpisodeSearchRecord sets contentKind Episode and copies title, series name, and truncated description " +
        "onto the unified playable fields, and series description from the podcast blurb.")]
    public void Maps_unified_playable_fields_for_an_episode()
    {
        // Arrange
        var episode = CreateEpisode();
        episode.Title = "  " + episode.Title.Trim() + "  ";
        episode.Description = "Episode blurb";
        var podcast = new Podcast
        {
            Name = " Series ",
            Description = "Parent blurb"
        };

        // Act
        var result = new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord(includeUnifiedPlayableFields: true);

        // Assert
        result.ContentKind.Should().Be(SearchContentKind.Episode);
        result.Title.Should().Be(episode.Title.Trim());
        result.SeriesName.Should().Be("Series");
        result.Description.Should().Be("Episode blurb");
        result.EpisodeTitle.Should().BeNull();
        result.EpisodeDescription.Should().BeNull();
        result.PodcastName.Should().BeNull();
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
        result.Description.Should().Be("Short description.");
        result.EpisodeDescription.Should().BeNull();
    }

    [Fact(DisplayName =
        "ToEpisodeSearchRecord leaves contentKind, title, seriesName, and description unset " +
        "when unified fields are turned off, and the upload JSON keeps episodeTitle, podcastName, " +
        "and episodeDescription, for an index that still has the old names.")]
    public void omits_unified_playable_fields_when_the_flag_is_off()
    {
        // Arrange
        var episode = CreateEpisode();
        episode.Title = "Kept title";
        episode.Description = "Episode blurb";
        var podcast = new Podcast
        {
            Name = "Series",
            Description = "Parent blurb"
        };
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var result = new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord(includeUnifiedPlayableFields: false);
        var json = JsonSerializer.Serialize(result, serializerOptions);

        // Assert
        result.ContentKind.Should().BeNull();
        result.Title.Should().BeNull();
        result.SeriesName.Should().BeNull();
        result.Description.Should().BeNull();
        result.EpisodeTitle.Should().Be("Kept title");
        result.EpisodeDescription.Should().Be("Episode blurb");
        result.PodcastName.Should().Be("Series");
        json.Should().NotContain("\"contentKind\"");
        json.Should().NotContain("\"title\"");
        json.Should().NotContain("\"seriesName\"");
        json.Should().NotContain("\"description\"");
        json.Should().Contain("\"episodeTitle\"");
    }

    [Fact(DisplayName =
        "ToEpisodeSearchRecord by default includes contentKind, title, seriesName, and description " +
        "and omits episodeTitle, podcastName, and episodeDescription, because the rebuilt index " +
        "only has the replacement names.")]
    public void includes_unified_playable_fields_on_the_default_upload()
    {
        // Arrange
        var episode = CreateEpisode();
        episode.Title = "Kept title";
        episode.Description = "Episode blurb";
        var podcast = new Podcast
        {
            Name = "Series",
            Description = "Parent blurb"
        };
        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Act
        var result = new PodcastEpisode(podcast, episode)
            .ToEpisodeSearchRecord();
        var json = JsonSerializer.Serialize(result, serializerOptions);

        // Assert
        json.Should().Contain("\"contentKind\":\"Episode\"");
        json.Should().Contain("\"title\":\"Kept title\"");
        json.Should().Contain("\"seriesName\":\"Series\"");
        json.Should().Contain("\"description\":\"Episode blurb\"");
        json.Should().NotContain("episodeTitle");
        json.Should().NotContain("podcastName");
        json.Should().NotContain("episodeDescription");
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

        var contentKind = fields.Single(field => field.Name == "contentKind");
        contentKind.IsFilterable.Should().BeTrue();
        contentKind.IsFacetable.Should().BeTrue();
        fields.Single(field => field.Name == "title").IsSearchable.Should().BeTrue();
        fields.Single(field => field.Name == "description").IsSearchable.Should().BeTrue();
        fields.Should().NotContain(field => field.Name == "seriesDescription");
        fields.Should().NotContain(field => field.Name == "episodeTitle");
        fields.Should().NotContain(field => field.Name == "podcastName");
        fields.Should().NotContain(field => field.Name == "episodeDescription");
        var seriesName = fields.Single(field => field.Name == "seriesName");
        seriesName.IsSearchable.Should().BeTrue();
        seriesName.IsFacetable.Should().BeTrue();
    }

    private static Episode CreateEpisode() => new()
    {
        Id = Guid.NewGuid(),
        Title = " Episode ",
        Description = "Description",
        ReleaseUtc = DateTime.UtcNow.Date.AddDays(-9).AddHours(12),
        Length = TimeSpan.FromSeconds(123),
        SpotifyId = "spotify-episode-id",
        YouTubeId = "youtube-id",
        AppleId = 987654321,
        Subjects = ["subject"],
        SearchTerms = "episode terms"
    };
}
