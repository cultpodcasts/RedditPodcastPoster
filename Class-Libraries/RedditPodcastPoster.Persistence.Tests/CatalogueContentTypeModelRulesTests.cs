using System.Reflection;
using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.ContentKinds;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;

namespace RedditPodcastPoster.Persistence.Tests;

public class CatalogueContentTypeModelRulesTests
{
    private readonly Fixture _fixture = new();

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
        "Podcast, TvShow, NewsOrganisation, and Film subclass Publisher so they share social handles, " +
        "publisher label, subject defaults, known/search terms, and indexing bookmarks.")]
    public void Catalogue_publishers_subclass_publisher_with_shared_members()
    {
        // Arrange
        var sharedMembers = new[]
        {
            nameof(Publisher.Name),
            nameof(Publisher.Description),
            nameof(Publisher.LatestReleased),
            nameof(Publisher.LastIndexed),
            nameof(Publisher.Language),
            nameof(Publisher.Removed),
            nameof(Publisher.PublisherName),
            nameof(Publisher.TwitterHandle),
            nameof(Publisher.BlueskyHandle),
            nameof(Publisher.HashTag),
            nameof(Publisher.EnrichmentHashTags),
            nameof(Publisher.IgnoredAssociatedSubjects),
            nameof(Publisher.IgnoredSubjects),
            nameof(Publisher.DefaultSubject),
            nameof(Publisher.SearchTerms),
            nameof(Publisher.KnownTerms)
        };

        // Act / Assert
        typeof(Podcast).Should().BeAssignableTo<Publisher>();
        typeof(TvShow).Should().BeAssignableTo<Publisher>();
        typeof(NewsOrganisation).Should().BeAssignableTo<Publisher>();
        typeof(Film).Should().BeAssignableTo<Publisher>();

        foreach (var member in sharedMembers)
        {
            typeof(Publisher).GetProperty(member).Should().NotBeNull(because: member);
        }

        typeof(Film).GetProperty("Title").Should().BeNull(
            "Film uses Publisher.Name as its display title; no separate Title member");

        new Podcast().ModelType.Should().Be(ModelType.Podcast);
        new Film(_fixture.Create<string>()).Name.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName =
        "Episode, TvShowEpisode, and NewsReport subclass Playable and implement IMediaProduction, IPlayable, " +
        "IPromotable, and IRemovable; Film implements IPlayable, IPromotable, and IRemovable via Publisher " +
        "but not IMediaProduction or Playable.")]
    public void Catalogue_playables_use_playable_base_and_capability_interfaces()
    {
        // Arrange / Act / Assert
        typeof(Episode).Should().BeAssignableTo<Playable>();
        typeof(TvShowEpisode).Should().BeAssignableTo<Playable>();
        typeof(NewsReport).Should().BeAssignableTo<Playable>();
        typeof(Film).Should().NotBeAssignableTo<Playable>();

        typeof(Episode).Should().BeAssignableTo<IMediaProduction>();
        typeof(TvShowEpisode).Should().BeAssignableTo<IMediaProduction>();
        typeof(NewsReport).Should().BeAssignableTo<IMediaProduction>();
        typeof(Film).Should().NotBeAssignableTo<IMediaProduction>();

        foreach (var type in new[] { typeof(Episode), typeof(TvShowEpisode), typeof(NewsReport), typeof(Film) })
        {
            type.Should().BeAssignableTo<IPlayable>(because: type.Name);
            type.Should().BeAssignableTo<IPromotable>(because: type.Name);
            type.Should().BeAssignableTo<IRemovable>(because: type.Name);
        }

        typeof(Podcast).Should().BeAssignableTo<IRemovable>();
        typeof(Podcast).Should().NotBeAssignableTo<IPlayable>();

        typeof(IPlayable).GetProperty(nameof(IPlayable.Description)).Should().NotBeNull();
        typeof(IPlayable).GetProperty(nameof(IPlayable.Services)).Should().NotBeNull();
        typeof(IPromotable).GetMethod(nameof(IPromotable.ClearBlueskyPostState)).Should().NotBeNull();
        typeof(IPromotable).GetProperty(nameof(IPromotable.BlueskyPosted)).Should().NotBeNull();

        new Episode().ModelType.Should().Be(ModelType.Episode);
        new Episode().IsRemoved().Should().BeFalse();
        new Film().IsRemoved().Should().BeFalse();
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
            propertyNames.Should().Contain("Guests", because: type.Name);
            type.GetProperty("Services")!.PropertyType
                .Should().Be(typeof(Dictionary<string, ServiceLink>), because: type.Name);
            type.GetProperty("Guests")!.PropertyType
                .Should().Be(typeof(string[]), because: type.Name);
        }
    }

    [Fact(DisplayName =
        "Film release may be year-only or a calendar date; TvShowEpisode and NewsReport release are calendar date — " +
        "JSON stores a bare year number, yyyy-MM-dd string, or ISO-8601 Zulu datetime (not a precision object).")]
    public void Non_podcast_playables_use_catalogue_release_json_scalars()
    {
        // Arrange — year/date literals assert JSON scalar contracts; datetime from relative helper
        var year = DateTime.UtcNow.Year - 1;
        var dateOnlyRelease = DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(10));
        var dateTimeUtc = DomainTestFixture.UtcAtTime(-10, new TimeSpan(12, 34, 56));
        var yearRelease = CatalogueRelease.FromYear(year);
        var dateRelease = CatalogueRelease.FromDate(dateOnlyRelease);
        var dateTimeRelease = CatalogueRelease.FromDateTimeUtc(dateTimeUtc);

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

        yearJson.Should().Be(year.ToString());
        dateJson.Should().Be($"\"{dateOnlyRelease:yyyy-MM-dd}\"");
        dateTimeJson.Should().Be($"\"{dateTimeUtc:yyyy-MM-ddTHH:mm:ss}Z\"");

        yearRoundTrip!.Precision.Should().Be(CatalogueReleasePrecision.Year);
        yearRoundTrip.Year.Should().Be(year);
        yearRoundTrip.Date.Should().BeNull();
        yearRoundTrip.DateTimeUtc.Should().BeNull();

        dateRoundTrip!.Precision.Should().Be(CatalogueReleasePrecision.Date);
        dateRoundTrip.Year.Should().Be(dateOnlyRelease.Year);
        dateRoundTrip.Date.Should().Be(dateOnlyRelease);
        dateRoundTrip.DateTimeUtc.Should().BeNull();

        dateTimeRoundTrip!.Precision.Should().Be(CatalogueReleasePrecision.DateTimeUtc);
        dateTimeRoundTrip.Year.Should().Be(dateTimeUtc.Year);
        dateTimeRoundTrip.Date.Should().Be(DateOnly.FromDateTime(dateTimeUtc));
        dateTimeRoundTrip.DateTimeUtc.Should().Be(dateTimeUtc);
    }

    [Fact(DisplayName =
        "CatalogueRelease has no public constructor: System.Text.Json still deserializes year, date, and Zulu " +
        "scalars via CatalogueReleaseJsonConverter factories, so Cosmos document round-trips remain valid.")]
    public void CatalogueRelease_private_constructor_still_json_round_trips_via_converter()
    {
        // Arrange — JSON literals assert converter contracts; datetime specimen from relative helper
        var publicConstructors = typeof(CatalogueRelease)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public);
        var year = DateTime.UtcNow.Year - 2;
        var dateOnly = DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(40));
        var dateTimeUtc = DomainTestFixture.UtcAtTime(-40, new TimeSpan(8, 15, 30));
        var yearJson = year.ToString();
        var dateJson = $"\"{dateOnly:yyyy-MM-dd}\"";
        var dateTimeJson = $"\"{dateTimeUtc:yyyy-MM-ddTHH:mm:ss}Z\"";

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
        fromYear.Year.Should().Be(year);
        fromYear.Date.Should().BeNull();
        fromYear.DateTimeUtc.Should().BeNull();
        yearWired.Should().Be(yearJson);

        fromDate.Should().NotBeNull();
        fromDate!.Precision.Should().Be(CatalogueReleasePrecision.Date);
        fromDate.Date.Should().Be(dateOnly);
        fromDate.DateTimeUtc.Should().BeNull();
        dateWired.Should().Be(dateJson);

        fromDateTime.Should().NotBeNull();
        fromDateTime!.Precision.Should().Be(CatalogueReleasePrecision.DateTimeUtc);
        fromDateTime.DateTimeUtc.Should().Be(dateTimeUtc);
        dateTimeWired.Should().Be(dateTimeJson);
    }

    [Fact(DisplayName =
        "Film.Release nested property serializes and deserializes CatalogueRelease scalars even though " +
        "CatalogueRelease has only a private constructor.")]
    public void Film_nested_release_json_round_trips_with_private_catalogue_release_ctor()
    {
        // Arrange
        var year = DateTime.UtcNow.Year - 3;
        var film = new Film(_fixture.Create<string>())
        {
            Release = CatalogueRelease.FromYear(year)
        };

        // Act
        var json = JsonSerializer.Serialize(film);
        var roundTrip = JsonSerializer.Deserialize<Film>(json);

        // Assert
        json.Should().Contain($"\"release\":{year}");
        roundTrip.Should().NotBeNull();
        roundTrip!.Release.Should().NotBeNull();
        roundTrip.Release!.Precision.Should().Be(CatalogueReleasePrecision.Year);
        roundTrip.Release.Year.Should().Be(year);
        roundTrip.Release.Date.Should().BeNull();
        roundTrip.Release.DateTimeUtc.Should().BeNull();
    }

    [Fact(DisplayName =
        "NewsReport.Release nested date scalar deserializes through CatalogueReleaseJsonConverter " +
        "without requiring a public CatalogueRelease constructor.")]
    public void NewsReport_nested_release_date_deserializes_via_converter()
    {
        // Arrange
        var dateOnly = DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(20));
        var report = new NewsReport
        {
            Title = _fixture.Create<string>(),
            Release = CatalogueRelease.FromDate(dateOnly)
        };

        // Act
        var json = JsonSerializer.Serialize(report);
        var roundTrip = JsonSerializer.Deserialize<NewsReport>(json);

        // Assert
        json.Should().Contain($"\"release\":\"{dateOnly:yyyy-MM-dd}\"");
        roundTrip.Should().NotBeNull();
        roundTrip!.Release.Should().NotBeNull();
        roundTrip.Release!.Precision.Should().Be(CatalogueReleasePrecision.Date);
        roundTrip.Release.Date.Should().Be(dateOnly);
        roundTrip.Release.DateTimeUtc.Should().BeNull();
    }

    [Fact(DisplayName =
        "ServiceLink carries an optional lang code (2-letter ISO 639-1, or 3-letter where none exists): " +
        "serialized as a JSON scalar, omitted when null, and lang alone does not make the link non-empty.")]
    public void Service_link_supports_optional_language_code()
    {
        // Arrange
        var twoLetter = new ServiceLink { Url = new Uri("https://example.com/watch"), Language = "de" };
        var threeLetter = new ServiceLink { Url = new Uri("https://example.com/watch"), Language = "haw" };
        var unspecified = new ServiceLink { Url = new Uri("https://example.com/watch") };
        var languageOnly = new ServiceLink { Language = "de" };

        // Act
        var twoLetterJson = JsonSerializer.Serialize(twoLetter);
        var threeLetterJson = JsonSerializer.Serialize(threeLetter);
        var unspecifiedJson = JsonSerializer.Serialize(unspecified);
        var roundTrip = JsonSerializer.Deserialize<ServiceLink>(twoLetterJson);

        // Assert
        twoLetterJson.Should().Contain("\"lang\":\"de\"");
        threeLetterJson.Should().Contain("\"lang\":\"haw\"");
        unspecifiedJson.Should().NotContain("lang");
        roundTrip!.Language.Should().Be("de");
        languageOnly.IsEmpty.Should().BeTrue("a lang code without a url or image is not a usable link");
    }

    [Fact(DisplayName =
        "Film, TvShow, and NewsOrganisation file keys use kind prefixes (film-/tvshow-/news-) so public JSON DB " +
        "and backups do not collide with unprefixed podcast series keys.")]
    public void Series_like_entities_use_prefixed_file_keys()
    {
        // Arrange
        var name = _fixture.Create<string>();

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
        "TvShowEpisode.SetTvShowProperties: when parent id, name, search terms, language, removed, and metadata " +
        "version differ, then all are denormalised (name/searchTerms/language trimmed), because playables mirror Episode.SetPodcastProperties.")]
    public void TvShowEpisode_SetTvShowProperties_denormalises_full_parent_projection()
    {
        // Arrange
        var tvShowId = _fixture.Create<Guid>();
        var tvShowName = _fixture.Create<string>();
        var searchTerms = _fixture.Create<string>();
        var language = _fixture.Create<string>();
        var metadataVersion = _fixture.Create<long>();
        var tvShow = new TvShow
        {
            Id = tvShowId,
            Name = $" {tvShowName} ",
            SearchTerms = $" {searchTerms} ",
            Language = $" {language} ",
            Removed = true,
            Timestamp = metadataVersion
        };
        var episode = new TvShowEpisode();

        // Act
        var (updated, updatedMetadata) = episode.SetTvShowProperties(tvShow);

        // Assert
        updated.Should().BeTrue();
        updatedMetadata.Should().BeTrue();
        episode.TvShowId.Should().Be(tvShowId);
        episode.TvShowName.Should().Be(tvShowName);
        episode.TvShowSearchTerms.Should().Be(searchTerms);
        episode.TvShowLanguage.Should().Be(language);
        episode.TvShowRemoved.Should().BeTrue();
        episode.TvShowMetadataVersion.Should().Be(metadataVersion);
    }

    [Fact(DisplayName =
        "TvShowEpisode.SetTvShowProperties: when parent projection already matches (including trimmed values), " +
        "then updated and updatedMetadata are both false.")]
    public void TvShowEpisode_SetTvShowProperties_returns_false_when_unchanged()
    {
        // Arrange
        var tvShowId = _fixture.Create<Guid>();
        var tvShowName = _fixture.Create<string>();
        var searchTerms = _fixture.Create<string>();
        var language = _fixture.Create<string>();
        var metadataVersion = _fixture.Create<long>();
        var tvShow = new TvShow
        {
            Id = tvShowId,
            Name = tvShowName,
            SearchTerms = searchTerms,
            Language = language,
            Removed = false,
            Timestamp = metadataVersion
        };
        var episode = new TvShowEpisode();
        episode.SetTvShowProperties(tvShow);

        // Act
        var (updated, updatedMetadata) = episode.SetTvShowProperties(tvShow);

        // Assert
        updated.Should().BeFalse();
        updatedMetadata.Should().BeFalse();
        episode.TvShowId.Should().Be(tvShowId);
        episode.TvShowName.Should().Be(tvShowName);
        episode.TvShowSearchTerms.Should().Be(searchTerms);
        episode.TvShowLanguage.Should().Be(language);
        episode.TvShowRemoved.Should().BeFalse();
        episode.TvShowMetadataVersion.Should().Be(metadataVersion);
    }

    [Fact(DisplayName =
        "NewsReport.SetNewsOrganisationProperties: when parent id, name, search terms, language, removed, and " +
        "metadata version differ, then all are denormalised (name/searchTerms/language trimmed), because News mirrors TvShow/Episode parent sync.")]
    public void NewsReport_SetNewsOrganisationProperties_denormalises_full_parent_projection()
    {
        // Arrange
        var organisationId = _fixture.Create<Guid>();
        var organisationName = _fixture.Create<string>();
        var searchTerms = _fixture.Create<string>();
        var language = _fixture.Create<string>();
        var metadataVersion = _fixture.Create<long>();
        var organisation = new NewsOrganisation
        {
            Id = organisationId,
            Name = $" {organisationName} ",
            SearchTerms = $" {searchTerms} ",
            Language = $" {language} ",
            Removed = true,
            Timestamp = metadataVersion
        };
        var report = new NewsReport();

        // Act
        var (updated, updatedMetadata) = report.SetNewsOrganisationProperties(organisation);

        // Assert
        updated.Should().BeTrue();
        updatedMetadata.Should().BeTrue();
        report.NewsOrganisationId.Should().Be(organisationId);
        report.NewsOrganisationName.Should().Be(organisationName);
        report.NewsOrganisationSearchTerms.Should().Be(searchTerms);
        report.NewsOrganisationLanguage.Should().Be(language);
        report.NewsOrganisationRemoved.Should().BeTrue();
        report.NewsOrganisationMetadataVersion.Should().Be(metadataVersion);
    }

    [Fact(DisplayName =
        "NewsReport.SetNewsOrganisationProperties: when parent projection already matches (including trimmed values), " +
        "then updated and updatedMetadata are both false.")]
    public void NewsReport_SetNewsOrganisationProperties_returns_false_when_unchanged()
    {
        // Arrange
        var organisationId = _fixture.Create<Guid>();
        var organisationName = _fixture.Create<string>();
        var searchTerms = _fixture.Create<string>();
        var language = _fixture.Create<string>();
        var metadataVersion = _fixture.Create<long>();
        var organisation = new NewsOrganisation
        {
            Id = organisationId,
            Name = organisationName,
            SearchTerms = searchTerms,
            Language = language,
            Removed = false,
            Timestamp = metadataVersion
        };
        var report = new NewsReport();
        report.SetNewsOrganisationProperties(organisation);

        // Act
        var (updated, updatedMetadata) = report.SetNewsOrganisationProperties(organisation);

        // Assert
        updated.Should().BeFalse();
        updatedMetadata.Should().BeFalse();
        report.NewsOrganisationId.Should().Be(organisationId);
        report.NewsOrganisationName.Should().Be(organisationName);
        report.NewsOrganisationSearchTerms.Should().Be(searchTerms);
        report.NewsOrganisationLanguage.Should().Be(language);
        report.NewsOrganisationRemoved.Should().BeFalse();
        report.NewsOrganisationMetadataVersion.Should().Be(metadataVersion);
    }
}
