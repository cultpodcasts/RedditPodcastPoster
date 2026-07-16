using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Factories;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Spotify.Mapping;
using RedditPodcastPoster.Text;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Mapping;

public class SpotifyCatalogueInputMappingRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly IHtmlSanitiser _htmlSanitiser = new HtmlSanitiser(NullLogger<HtmlSanitiser>.Instance);
    private readonly EpisodeFromCandidateFactory _factory = new();

    [Fact(DisplayName =
        "When a Spotify API episode is mapped through catalogue input, adapter, and factory, " +
        "the episode matches the legacy FromSpotify shape because provider boundaries must preserve indexed fields.")]
    public void Spotify_api_round_trip_matches_legacy_episode_shape()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var title = _fixture.CreateTitle();
        var descriptionHtml = $"<p>{_fixture.Create<string>()}</p>";
        var duration = _fixture.CreateDuration();
        var releaseDate = DomainTestFixture.UtcDateDaysAgo(3).ToString("yyyy-MM-dd");
        var spotifyUrl = _fixture.DefaultSpotifyUrl(spotifyId).ToString();
        var imageUrl = _fixture.Create<Uri>().ToString();
        var images = new List<Image> { new() { Url = imageUrl, Height = 640 } };
        var apiEpisode = new SimpleEpisode
        {
            Id = spotifyId,
            Name = title,
            HtmlDescription = descriptionHtml,
            DurationMs = (int)duration.TotalMilliseconds,
            ReleaseDate = releaseDate,
            ExternalUrls = new Dictionary<string, string> { ["spotify"] = spotifyUrl },
            Images = images
        };
        // Act
        var input = apiEpisode.ToCatalogueInput(_htmlSanitiser);
        var candidate = new SpotifyEpisodeAdapter().Adapt(input);
        var episode = _factory.Create(candidate, explicitContent: false);

        // Assert
        episode.ShouldMatchExpectation(EpisodeExpectation.From(candidate));
    }

    [Fact(DisplayName =
        "When Spotify returns no HTML description, catalogue mapping produces an empty sanitized description " +
        "because null descriptions must not propagate as null strings.")]
    public void Null_html_description_maps_to_empty_sanitized_description()
    {
        // Arrange
        var episode = CreateSimpleEpisode();
        episode.HtmlDescription = null!;

        // Act
        var input = episode.ToCatalogueInput(_htmlSanitiser);

        // Assert
        input.Description.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When FullEpisode HtmlDescription is null, catalogue mapping produces an empty sanitized description " +
        "because the full-episode enricher path must tolerate the same API nulls as SimpleEpisode.")]
    public void Full_episode_null_html_description_maps_to_empty_sanitized_description()
    {
        // Arrange
        var episode = CreateFullEpisode();
        episode.HtmlDescription = null!;

        // Act
        var input = episode.ToCatalogueInput(_htmlSanitiser);

        // Assert
        input.Description.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When Spotify episode has no images, catalogue mapping leaves Image null " +
        "because missing artwork must not throw during adapter conversion.")]
    public void Simple_episode_without_images_maps_to_null_image_url()
    {
        // Arrange
        var episode = CreateSimpleEpisode();
        episode.Images = [];

        // Act
        var input = episode.ToCatalogueInput(_htmlSanitiser);

        // Assert
        input.Image.Should().BeNull();
    }

    [Fact(DisplayName =
        "When FullEpisode has no images, catalogue mapping leaves Image null " +
        "because both provider DTO paths must tolerate absent artwork.")]
    public void Full_episode_without_images_maps_to_null_image_url()
    {
        // Arrange
        var episode = CreateFullEpisode();
        episode.Images = [];

        // Act
        var input = episode.ToCatalogueInput(_htmlSanitiser);

        // Assert
        input.Image.Should().BeNull();
    }

    [Fact(DisplayName =
        "When Spotify episode name has leading or trailing whitespace, catalogue mapping trims the title " +
        "because indexed titles must match legacy FromSpotify behavior.")]
    public void Title_whitespace_is_trimmed()
    {
        // Arrange
        var title = _fixture.CreateTitle();
        var episode = CreateSimpleEpisode(name: $"  {title}  ");

        // Act
        var input = episode.ToCatalogueInput(_htmlSanitiser);

        // Assert
        input.Title.Should().Be(title);
    }

    [Fact(DisplayName =
        "When the same episode data arrives as SimpleEpisode or FullEpisode, catalogue mapping produces equivalent inputs " +
        "because both provider paths must index identically.")]
    public void Simple_episode_and_full_episode_produce_equivalent_catalogue_input()
    {
        // Arrange
        var spotifyId = _fixture.CreateSpotifyId();
        var title = _fixture.CreateTitle();
        var descriptionHtml = $"<p>{_fixture.Create<string>()}</p>";
        var duration = _fixture.CreateDuration();
        var releaseDate = DomainTestFixture.UtcDateDaysAgo(3).ToString("yyyy-MM-dd");
        var spotifyUrl = _fixture.DefaultSpotifyUrl(spotifyId).ToString();
        var imageUrl = _fixture.Create<Uri>().ToString();
        var images = new List<Image> { new() { Url = imageUrl, Height = 640 } };

        var simpleEpisode = new SimpleEpisode
        {
            Id = spotifyId,
            Name = title,
            HtmlDescription = descriptionHtml,
            DurationMs = (int)duration.TotalMilliseconds,
            ReleaseDate = releaseDate,
            ExternalUrls = new Dictionary<string, string> { ["spotify"] = spotifyUrl },
            Images = images
        };

        var fullEpisode = new FullEpisode
        {
            Id = spotifyId,
            Name = title,
            HtmlDescription = descriptionHtml,
            DurationMs = (int)duration.TotalMilliseconds,
            ReleaseDate = releaseDate,
            ExternalUrls = new Dictionary<string, string> { ["spotify"] = spotifyUrl },
            Images = images
        };

        // Act
        var fromSimple = simpleEpisode.ToCatalogueInput(_htmlSanitiser);
        var fromFull = fullEpisode.ToCatalogueInput(_htmlSanitiser);

        // Assert
        fromSimple.Should().BeEquivalentTo(fromFull, options => options
            .ComparingByMembers<SpotifyCatalogueInput>());
    }

    private SimpleEpisode CreateSimpleEpisode(string? name = null, string? htmlDescription = null)
    {
        var spotifyId = _fixture.CreateSpotifyId();
        return new SimpleEpisode
        {
            Id = spotifyId,
            Name = name ?? _fixture.CreateTitle(),
            HtmlDescription = htmlDescription ?? $"<p>{_fixture.Create<string>()}</p>",
            DurationMs = (int)_fixture.CreateDuration().TotalMilliseconds,
            ReleaseDate = DomainTestFixture.UtcDateDaysAgo(1).ToString("yyyy-MM-dd"),
            ExternalUrls = new Dictionary<string, string>
            {
                ["spotify"] = _fixture.DefaultSpotifyUrl(spotifyId).ToString()
            },
            Images = []
        };
    }

    private FullEpisode CreateFullEpisode(string? name = null, string? htmlDescription = null)
    {
        var spotifyId = _fixture.CreateSpotifyId();
        return new FullEpisode
        {
            Id = spotifyId,
            Name = name ?? _fixture.CreateTitle(),
            HtmlDescription = htmlDescription ?? $"<p>{_fixture.Create<string>()}</p>",
            DurationMs = (int)_fixture.CreateDuration().TotalMilliseconds,
            ReleaseDate = DomainTestFixture.UtcDateDaysAgo(1).ToString("yyyy-MM-dd"),
            ExternalUrls = new Dictionary<string, string>
            {
                ["spotify"] = _fixture.DefaultSpotifyUrl(spotifyId).ToString()
            },
            Images = []
        };
    }
}
