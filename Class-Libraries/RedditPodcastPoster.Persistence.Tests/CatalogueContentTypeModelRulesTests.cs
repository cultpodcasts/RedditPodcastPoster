using System.Reflection;
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
        yearRoundTrip.DateTimeUtc.Should().BeNull();

        dateRoundTrip!.Precision.Should().Be(CatalogueReleasePrecision.Date);
        dateRoundTrip.Year.Should().Be(2020);
        dateRoundTrip.Date.Should().Be(new DateOnly(2020, 6, 15));
        dateRoundTrip.DateTimeUtc.Should().BeNull();

        dateTimeRoundTrip!.Precision.Should().Be(CatalogueReleasePrecision.DateTimeUtc);
        dateTimeRoundTrip.Year.Should().Be(2020);
        dateTimeRoundTrip.Date.Should().Be(new DateOnly(2020, 6, 15));
        dateTimeRoundTrip.DateTimeUtc.Should().Be(new DateTime(2020, 6, 15, 12, 34, 56, DateTimeKind.Utc));
    }

    [Fact(DisplayName =
        "CatalogueRelease has no public constructor: System.Text.Json still deserializes year, date, and Zulu " +
        "scalars via CatalogueReleaseJsonConverter factories, so Cosmos document round-trips remain valid.")]
    public void CatalogueRelease_private_constructor_still_json_round_trips_via_converter()
    {
        // Arrange
        var publicConstructors = typeof(CatalogueRelease)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        var yearJson = "1999";
        var dateJson = "\"1999-03-01\"";
        var dateTimeJson = "\"1999-03-01T08:15:30Z\"";

        // Act
        var fromYear = JsonSerializer.Deserialize<CatalogueRelease>(yearJson);
        var fromDate = JsonSerializer.Deserialize<CatalogueRelease>(dateJson);
        var fromDateTime = JsonSerializer.Deserialize<CatalogueRelease>(dateTimeJson);
        var yearWired = JsonSerializer.Serialize(fromYear);
        var dateWired = JsonSerializer.Serialize(fromDate);
        var dateTimeWired = JsonSerializer.Serialize(fromDateTime);

        // Assert
        publicConstructors.Should().BeEmpty(
            "object-initializer construction must not be able to set Year/Date/DateTimeUtc independently");

        fromYear.Should().NotBeNull();
        fromYear!.Precision.Should().Be(CatalogueReleasePrecision.Year);
        fromYear.Year.Should().Be(1999);
        fromYear.Date.Should().BeNull();
        fromYear.DateTimeUtc.Should().BeNull();
        yearWired.Should().Be(yearJson);

        fromDate.Should().NotBeNull();
        fromDate!.Precision.Should().Be(CatalogueReleasePrecision.Date);
        fromDate.Date.Should().Be(new DateOnly(1999, 3, 1));
        fromDate.DateTimeUtc.Should().BeNull();
        dateWired.Should().Be(dateJson);

        fromDateTime.Should().NotBeNull();
        fromDateTime!.Precision.Should().Be(CatalogueReleasePrecision.DateTimeUtc);
        fromDateTime.DateTimeUtc.Should().Be(new DateTime(1999, 3, 1, 8, 15, 30, DateTimeKind.Utc));
        dateTimeWired.Should().Be(dateTimeJson);
    }

    [Fact(DisplayName =
        "Film.Release nested property serializes and deserializes CatalogueRelease scalars even though " +
        "CatalogueRelease has only a private constructor.")]
    public void Film_nested_release_json_round_trips_with_private_catalogue_release_ctor()
    {
        // Arrange
        var film = new Film("Nested Release Specimen")
        {
            Release = CatalogueRelease.FromYear(2012)
        };

        // Act
        var json = JsonSerializer.Serialize(film);
        var roundTrip = JsonSerializer.Deserialize<Film>(json);

        // Assert
        json.Should().Contain("\"release\":2012");
        roundTrip.Should().NotBeNull();
        roundTrip!.Release.Should().NotBeNull();
        roundTrip.Release!.Precision.Should().Be(CatalogueReleasePrecision.Year);
        roundTrip.Release.Year.Should().Be(2012);
        roundTrip.Release.Date.Should().BeNull();
        roundTrip.Release.DateTimeUtc.Should().BeNull();
    }

    [Fact(DisplayName =
        "NewsReport.Release nested date scalar deserializes through CatalogueReleaseJsonConverter " +
        "without requiring a public CatalogueRelease constructor.")]
    public void NewsReport_nested_release_date_deserializes_via_converter()
    {
        // Arrange
        var report = new NewsReport
        {
            Title = "Nested Date Specimen",
            Release = CatalogueRelease.FromDate(new DateOnly(2018, 11, 20))
        };

        // Act
        var json = JsonSerializer.Serialize(report);
        var roundTrip = JsonSerializer.Deserialize<NewsReport>(json);

        // Assert
        json.Should().Contain("\"release\":\"2018-11-20\"");
        roundTrip.Should().NotBeNull();
        roundTrip!.Release.Should().NotBeNull();
        roundTrip.Release!.Precision.Should().Be(CatalogueReleasePrecision.Date);
        roundTrip.Release.Date.Should().Be(new DateOnly(2018, 11, 20));
        roundTrip.Release.DateTimeUtc.Should().BeNull();
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
