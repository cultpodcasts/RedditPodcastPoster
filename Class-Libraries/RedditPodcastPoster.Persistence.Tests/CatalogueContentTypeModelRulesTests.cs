using System.Text.Json;
using FluentAssertions;
using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;

namespace RedditPodcastPoster.Persistence.Tests;

public class CatalogueContentTypeModelRulesTests
{
    private static readonly string[] ForbiddenProviderIdProperties =
        ["Ids", "YouTubeId", "SpotifyId", "AppleId", "YoutubeId"];

    [Fact(DisplayName =
        "ContentKind enumerates the four playable kinds: Episode, TvShowEpisode, Film, NewsReport.")]
    public void ContentKind_has_expected_playable_values()
    {
        // Arrange
        var names = Enum.GetNames<ContentKind>();

        // Act
        var values = Enum.GetValues<ContentKind>();

        // Assert
        names.Should().BeEquivalentTo(["Episode", "TvShowEpisode", "Film", "NewsReport"]);
        values.Should().HaveCount(4);
        values.Should().Contain(ContentKind.Episode);
        values.Should().Contain(ContentKind.TvShowEpisode);
        values.Should().Contain(ContentKind.Film);
        values.Should().Contain(ContentKind.NewsReport);
    }

    [Fact(DisplayName =
        "Film, TvShow, TvShowEpisode, NewsOrganisation, and NewsReport are CosmosSelector types with matching ModelType.")]
    public void Catalogue_entities_are_cosmos_selectors_with_model_type()
    {
        // Arrange / Act / Assert
        typeof(Film).Should().BeAssignableTo<CosmosSelector>();
        typeof(TvShow).Should().BeAssignableTo<CosmosSelector>();
        typeof(TvShowEpisode).Should().BeAssignableTo<CosmosSelector>();
        typeof(NewsOrganisation).Should().BeAssignableTo<CosmosSelector>();
        typeof(NewsReport).Should().BeAssignableTo<CosmosSelector>();

        new Film().ModelType.Should().Be(ModelType.Film);
        new TvShow().ModelType.Should().Be(ModelType.TvShow);
        new TvShowEpisode().ModelType.Should().Be(ModelType.TvShowEpisode);
        new NewsOrganisation().ModelType.Should().Be(ModelType.NewsOrganisation);
        new NewsReport().ModelType.Should().Be(ModelType.NewsReport);
    }

    [Fact(DisplayName =
        "Film has no parent id property because a film is a standalone playable with no series parent.")]
    public void Film_has_no_parent_id_property()
    {
        // Arrange
        var filmType = typeof(Film);
        var forbiddenParentIds = new[] { "NewsOrganisationId", "TvShowId", "PodcastId" };

        // Act
        var propertyNames = filmType.GetProperties().Select(p => p.Name).ToArray();

        // Assert
        propertyNames.Should().NotContain(forbiddenParentIds);
    }

    [Fact(DisplayName =
        "Film, TvShowEpisode, and NewsReport have no provider-id properties (YouTube/Spotify/Apple/Ids): " +
        "platform presence is services only, unlike podcast Episode collection identity.")]
    public void Non_podcast_playables_have_no_provider_id_fields()
    {
        // Arrange
        var playableTypes = new[] { typeof(Film), typeof(TvShowEpisode), typeof(NewsReport) };

        // Act / Assert
        foreach (var type in playableTypes)
        {
            var propertyNames = type.GetProperties().Select(p => p.Name).ToArray();
            propertyNames.Should().NotContain(ForbiddenProviderIdProperties, because: type.Name);
            propertyNames.Should().Contain("Services", because: type.Name);
            type.GetProperty("Services")!.PropertyType
                .Should().Be(typeof(Dictionary<string, ServiceLink>), because: type.Name);
        }
    }

    [Fact(DisplayName =
        "Film release may be year-only or a calendar date; TvShowEpisode and NewsReport release are calendar date — " +
        "JSON stores a bare year number, yyyy-MM-dd string, or ISO-8601 Zulu datetime (not a precision object).")]
    public void Non_podcast_playables_use_catalogue_release_json_scalars()
    {
        // Arrange
        var yearRelease = CatalogueRelease.FromYear(2020);
        var dateRelease = CatalogueRelease.FromDate(new DateOnly(2020, 6, 15));
        var dateTimeRelease = CatalogueRelease.FromDateTimeUtc(
            new DateTime(2020, 6, 15, 12, 34, 56, DateTimeKind.Utc));

        // Act
        var yearJson = JsonSerializer.Serialize(yearRelease);
        var dateJson = JsonSerializer.Serialize(dateRelease);
        var dateTimeJson = JsonSerializer.Serialize(dateTimeRelease);
        var yearRoundTrip = JsonSerializer.Deserialize<CatalogueRelease>(yearJson);
        var dateRoundTrip = JsonSerializer.Deserialize<CatalogueRelease>(dateJson);
        var dateTimeRoundTrip = JsonSerializer.Deserialize<CatalogueRelease>(dateTimeJson);

        // Assert
        typeof(Film).GetProperty(nameof(Film.Release))!.PropertyType.Should().Be(typeof(CatalogueRelease));
        typeof(TvShowEpisode).GetProperty(nameof(TvShowEpisode.Release))!.PropertyType.Should()
            .Be(typeof(CatalogueRelease));
        typeof(NewsReport).GetProperty(nameof(NewsReport.Release))!.PropertyType.Should()
            .Be(typeof(CatalogueRelease));

        yearJson.Should().Be("2020");
        dateJson.Should().Be("\"2020-06-15\"");
        dateTimeJson.Should().Be("\"2020-06-15T12:34:56Z\"");

        yearRoundTrip!.Precision.Should().Be(CatalogueReleasePrecision.Year);
        yearRoundTrip.Year.Should().Be(2020);
        yearRoundTrip.Date.Should().BeNull();

        dateRoundTrip!.Precision.Should().Be(CatalogueReleasePrecision.Date);
        dateRoundTrip.Date.Should().Be(new DateOnly(2020, 6, 15));

        dateTimeRoundTrip!.Precision.Should().Be(CatalogueReleasePrecision.DateTimeUtc);
        dateTimeRoundTrip.DateTimeUtc.Should().Be(new DateTime(2020, 6, 15, 12, 34, 56, DateTimeKind.Utc));
    }

    [Fact(DisplayName =
        "Film, TvShow, and NewsOrganisation file keys use kind prefixes (film-/tvshow-/news-) so public JSON DB " +
        "and backups do not collide with unprefixed podcast series keys.")]
    public void Series_like_entities_use_prefixed_file_keys()
    {
        // Arrange
        var name = "Example Show";

        // Act
        var film = new Film(name);
        var tvShow = new TvShow(name);
        var newsOrg = new NewsOrganisation(name);
        var slug = FileKeyFactory.GetFileKey(name);

        // Assert
        film.FileKey.Should().Be($"{FileKeyFactory.FilmPrefix}{slug}");
        tvShow.FileKey.Should().Be($"{FileKeyFactory.TvShowPrefix}{slug}");
        newsOrg.FileKey.Should().Be($"{FileKeyFactory.NewsOrganisationPrefix}{slug}");
        slug.Should().NotStartWith("film-");
    }

    [Fact(DisplayName =
        "TvShowEpisode.SetTvShowProperties updates TvShowId and TvShowName from the parent show.")]
    public void TvShowEpisode_SetTvShowProperties_updates_id_and_name()
    {
        // Arrange
        var tvShowId = Guid.NewGuid();
        var tvShowName = Guid.NewGuid().ToString("N");
        var tvShow = new TvShow { Id = tvShowId, Name = $" {tvShowName} " };
        var episode = new TvShowEpisode();

        // Act
        var updated = episode.SetTvShowProperties(tvShow);

        // Assert
        updated.Should().BeTrue();
        episode.TvShowId.Should().Be(tvShowId);
        episode.TvShowName.Should().Be(tvShowName);
    }
}
