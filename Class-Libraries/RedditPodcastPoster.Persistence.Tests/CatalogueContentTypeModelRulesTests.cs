using System.Globalization;
using System.Reflection;
using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
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
using RedditPodcastPoster.Persistence.Abstractions.Episodes;

namespace RedditPodcastPoster.Persistence.Tests;

public class CatalogueContentTypeModelRulesTests
{
    private readonly Fixture _fixture = new();
    private readonly DomainTestFixture _domain = new();

    /// <summary>
    /// Provider identity that only podcast <see cref="Episode"/> carries (nested <c>ids</c>, Spotify/Apple ids).
    /// Film, TvShowEpisode and NewsReport carry a compact <c>youTubeId</c> only (docs/episode-services.md).
    /// </summary>
    private static readonly string[] ForbiddenProviderIdProperties =
        ["Ids", "SpotifyId", "AppleId", "PodcastAppleId", "YoutubeId"];

    private static readonly string[] ForbiddenProviderIdJsonKeys =
        ["ids", "spotifyId", "appleId", "podcastAppleId", "youtubeId"];

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
        "and IPromotable; Film implements IPlayable, IPromotable, and IFilmCanonical via Publisher dual-role " +
        "but not IMediaProduction or Playable. IsRemoved is on IPlayable and Publisher.")]
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
        }

        typeof(Podcast).Should().NotBeAssignableTo<IPlayable>();
        typeof(Podcast).GetMethod(nameof(Publisher.IsRemoved)).Should().NotBeNull();

        typeof(IPlayable).GetMethod(nameof(IPlayable.IsRemoved)).Should().NotBeNull();
        typeof(IPlayable).GetProperty(nameof(IPlayable.Description)).Should().NotBeNull();
        typeof(IPlayable).GetProperty(nameof(IPlayable.Services)).Should().NotBeNull();
        typeof(IPlayable).GetProperty(nameof(IPlayable.Matches)).Should().NotBeNull();
        typeof(IPlayable).GetProperty(nameof(IPlayable.Release)).Should().NotBeNull();
        typeof(IPlayable).GetProperty(nameof(IPlayable.ReleaseSort)).Should().NotBeNull();
        typeof(IPromotable).GetMethod(nameof(IPromotable.ClearBlueskyPostState)).Should().NotBeNull();
        typeof(IPromotable).GetProperty(nameof(IPromotable.BlueskyPosted)).Should().NotBeNull();
        typeof(IPromotable).GetProperty(nameof(IPromotable.HashTag)).Should().NotBeNull();

        typeof(Film).Should().BeAssignableTo<IFilmCanonical>();
        typeof(TvShow).Should().BeAssignableTo<ITvCanonical>();
        typeof(TvShowEpisode).Should().BeAssignableTo<ITvCanonical>();
        typeof(TvShow).Should().NotBeAssignableTo<IFilmCanonical>();
        typeof(Film).Should().NotBeAssignableTo<ITvCanonical>();
        typeof(Podcast).Should().NotBeAssignableTo<IFilmCanonical>();
        typeof(Podcast).Should().NotBeAssignableTo<ITvCanonical>();

        new Episode().ModelType.Should().Be(ModelType.Episode);
        new Episode().IsRemoved().Should().BeFalse();
        new Film().IsRemoved().Should().BeFalse();
        new Film().HashTag.Should().BeNull();
        new Episode().HashTag.Should().BeNull();
    }

    [Fact(DisplayName =
        "All IPlayable types expose DateTime ReleaseSort for Cosmos >= / <= filters; SetRelease syncs it from " +
        "CatalogueRelease (year→1 Jan UTC, date→midnight UTC, datetime→instant), because Release itself is not LINQ-comparable.")]
    public void Playable_ReleaseSort_is_synced_from_CatalogueRelease_for_cosmos_range_filters()
    {
        // Arrange
        var year = DateTime.UtcNow.Year - 2;
        var dateOnly = DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(5));
        var dateTimeUtc = DomainTestFixture.UtcAtTime(-3, new TimeSpan(14, 15, 16));
        var episode = new Episode();
        var tvShowEpisode = new TvShowEpisode();
        var newsReport = new NewsReport();
        var film = new Film();

        // Act
        episode.SetRelease(CatalogueRelease.FromDateTimeUtc(dateTimeUtc));
        tvShowEpisode.SetRelease(CatalogueRelease.FromDate(dateOnly));
        newsReport.SetRelease(CatalogueRelease.FromDate(dateOnly));
        film.SetRelease(CatalogueRelease.FromYear(year));

        // Assert
        foreach (var type in new[] { typeof(Episode), typeof(TvShowEpisode), typeof(NewsReport), typeof(Film) })
        {
            type.GetProperty(nameof(IPlayable.ReleaseSort))!.PropertyType.Should().Be(typeof(DateTime), because: type.Name);
        }

        episode.ReleaseSort.Should().Be(dateTimeUtc);
        tvShowEpisode.ReleaseSort.Should().Be(dateOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        newsReport.ReleaseSort.Should().Be(dateOnly.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        film.ReleaseSort.Should().Be(new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var from = DomainTestFixture.UtcDaysAgo(10);
        var to = DateTime.UtcNow;
        (episode.ReleaseSort >= from && episode.ReleaseSort <= to).Should().BeTrue();
        (tvShowEpisode.ReleaseSort >= from && tvShowEpisode.ReleaseSort <= to).Should().BeTrue();
        (film.ReleaseSort >= new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc) &&
         film.ReleaseSort <= to).Should().BeTrue();
    }

    [Fact(DisplayName =
        "INTEGRITY InMemoryEpisodeRepository must rewrite Cosmos IsDefined before Compile so " +
        "EpisodeCosmosFilters.ReleasedOnOrAfter and ParentNotRemoved (combined via And) include/exclude by " +
        "ReleaseSort and ParentRemoved — without the rewrite, invoking the raw ParentNotRemoved predicate throws.")]
    public async Task InMemory_episode_filters_rewrite_IsDefined_before_compile()
    {
        // Arrange
        var podcast = _domain.CreatePodcast();
        var since = DomainTestFixture.UtcDaysAgo(7);
        var included = _domain.CreateStoredEpisode(podcast, e =>
        {
            e.ReleaseUtc = DomainTestFixture.UtcAtTime(-3, new TimeSpan(10, 0, 0));
            e.ParentRemoved = false;
        });
        var tooOld = _domain.CreateStoredEpisode(podcast, e =>
        {
            e.ReleaseUtc = DomainTestFixture.UtcAtTime(-14, new TimeSpan(10, 0, 0));
            e.ParentRemoved = false;
        });
        var parentRemoved = _domain.CreateStoredEpisode(podcast, e =>
        {
            e.ReleaseUtc = DomainTestFixture.UtcAtTime(-2, new TimeSpan(10, 0, 0));
            e.ParentRemoved = true;
        });
        var repo = new InMemoryEpisodeRepository();
        repo.Seed(included, tooOld, parentRemoved);
        var filter = EpisodeCosmosFilters.And(
            EpisodeCosmosFilters.ReleasedOnOrAfter(since),
            EpisodeCosmosFilters.ParentNotRemoved);
        var rawParentNotRemoved = EpisodeCosmosFilters.ParentNotRemoved.Compile();

        // Act
        var matches = new List<Episode>();
        await foreach (var episode in repo.GetByPodcastId(podcast.Id, filter))
        {
            matches.Add(episode);
        }

        // Assert
        var invokeRaw = () => rawParentNotRemoved(included);
        invokeRaw.Should().Throw<NotImplementedException>(
            "Cosmos IsDefined must not run on CLR without the in-memory rewrite");
        matches.Select(e => e.Id).Should().BeEquivalentTo([included.Id]);
    }

    [Fact(DisplayName =
        "INTEGRITY Episode JSON with only release (no releaseSort) deserializes so ReleaseSort equals " +
        "that UTC instant, because the Release setter syncs ReleaseSort in-memory and the next save writes releaseSort.")]
    public void Legacy_release_only_json_hydrates_ReleaseSort_in_memory()
    {
        // Arrange
        var releaseUtc = DomainTestFixture.UtcAtTime(-4, new TimeSpan(9, 10, 11));
        var releaseIso = releaseUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var legacyJson = $"{{\"release\":\"{releaseIso}\"}}";

        // Act
        var episode = JsonSerializer.Deserialize<Episode>(legacyJson, options);

        // Assert
        episode!.ReleaseSort.Should().Be(releaseUtc);
        episode.Release!.DateTimeUtc.Should().Be(releaseUtc);
    }

    [Fact(DisplayName =
        "Episode JSON carrying the retired podcastRemoved key no longer sets ParentRemoved, because the GATE 5 " +
        "bridge drop reads parentRemoved only (the GATE 1 wire rewrite removed podcastRemoved from stored episodes).")]
    public void Retired_podcastRemoved_key_does_not_set_ParentRemoved()
    {
        // Arrange
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var retiredJson = "{\"podcastRemoved\":true}";

        // Act
        var episode = JsonSerializer.Deserialize<Episode>(retiredJson, options);

        // Assert
        episode!.ParentRemoved.Should().BeNull();
    }

    [Fact(DisplayName =
        "Playable.CosmosParentNotRemovedSql filters on parentRemoved only, treats absent, null and false as not removed, " +
        "and no longer mentions podcastRemoved, because Function and console queries read the new wire name after the " +
        "GATE 5 bridge drop and must agree with the LINQ EpisodeCosmosFilters.ParentNotRemoved (null is not removed).")]
    public void Playable_cosmos_parent_not_removed_sql_uses_parentRemoved_only()
    {
        // Arrange / Act
        var parentFragment = Playable.CosmosParentNotRemovedSql;

        // Assert
        parentFragment.Should().Be("(NOT IS_DEFINED(e.parentRemoved) OR IS_NULL(e.parentRemoved) OR e.parentRemoved=false)");
        parentFragment.Should().NotContain("podcastRemoved");
        typeof(Playable).GetField("CosmosReleaseSortOrReleaseSql").Should().BeNull();
    }

    [Fact(DisplayName =
        "INTEGRITY CatalogueRelease DateTimeUtc round-trip preserves fractional seconds: deserialize fractional " +
        "Zulu → serialize → deserialize retains the same UTC instant.")]
    public void CatalogueRelease_datetime_round_trip_preserves_fractional_seconds()
    {
        // Arrange
        var baseUtc = DomainTestFixture.UtcAtTime(-2, new TimeSpan(13, 14, 15));
        var fractionalUtc = new DateTime(baseUtc.Ticks + TimeSpan.TicksPerMillisecond * 123 + 4567, DateTimeKind.Utc);
        var source = CatalogueRelease.FromDateTimeUtc(fractionalUtc);
        var sourceJson = $"\"{fractionalUtc:yyyy-MM-dd'T'HH:mm:ss.FFFFFFF}Z\"";

        // Act
        var fromJson = JsonSerializer.Deserialize<CatalogueRelease>(sourceJson);
        var rewritten = JsonSerializer.Serialize(fromJson);
        var roundTrip = JsonSerializer.Deserialize<CatalogueRelease>(rewritten);

        // Assert
        fromJson!.DateTimeUtc.Should().Be(fractionalUtc);
        rewritten.Should().Contain(".");
        roundTrip!.DateTimeUtc.Should().Be(fractionalUtc);
    }

    [Fact(DisplayName =
        "INTEGRITY CatalogueRelease.FromDateTimeUtc treats Unspecified Kind as already-UTC " +
        "(does not ToUniversalTime), because Spotify/date parses and Episode.ReleaseUtc round-trips " +
        "must not shift by the local offset (e.g. UK BST).")]
    public void CatalogueRelease_FromDateTimeUtc_unspecified_is_utc_not_local_conversion()
    {
        // Arrange — pick a relative calendar day whose Europe/London local midnight differs from
        // SpecifyKind-as-UTC so the NotBe is fail-closed on UTC agents (not only BST machines).
        var london = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "GMT Standard Time" : "Europe/London");
        DateTime unspecifiedMidnight = default;
        DateTime londonLocalThenUtc = default;
        var foundBstShift = false;
        for (var daysAgo = 1; daysAgo <= 250; daysAgo++)
        {
            var calendarDay = DomainTestFixture.UtcDateDaysAgo(daysAgo);
            var candidate = DateTime.ParseExact(
                calendarDay.ToString("yyyy-MM-dd"),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);
            DateTime shifted;
            try
            {
                shifted = TimeZoneInfo.ConvertTimeToUtc(candidate, london);
            }
            catch (ArgumentException)
            {
                // Skip DST spring-forward gaps / ambiguous fall-back instants.
                continue;
            }

            var asUtc = DateTime.SpecifyKind(candidate, DateTimeKind.Utc);
            if (shifted != asUtc)
            {
                unspecifiedMidnight = candidate;
                londonLocalThenUtc = shifted;
                foundBstShift = true;
                break;
            }
        }

        foundBstShift.Should().BeTrue(
            "need a relative day where Europe/London local midnight differs from SpecifyKind-as-UTC");

        // Act
        var release = CatalogueRelease.FromDateTimeUtc(unspecifiedMidnight);

        // Assert
        unspecifiedMidnight.Kind.Should().Be(DateTimeKind.Unspecified);
        var expectedUtc = DateTime.SpecifyKind(unspecifiedMidnight, DateTimeKind.Utc);
        release.DateTimeUtc.Should().Be(expectedUtc);
        release.ToSortUtc().Should().Be(expectedUtc);
        release.DateTimeUtc.Should().NotBe(londonLocalThenUtc,
            "Unspecified must not be treated as Europe/London local (BST/GMT shift)");
    }

    [Fact(DisplayName =
        "ReleaseSort is private-set and has a distinct JsonPropertyOrder from Release so callers cannot " +
        "desync the Cosmos range key, and Release is the only member mapped to JSON release (the LINQ-only " +
        "release fallback was dropped with the GATE 5 bridges).")]
    public void ReleaseSort_is_sync_only_with_distinct_property_order_and_no_release_fallback()
    {
        // Arrange
        var releaseSort = typeof(Playable).GetProperty(nameof(Playable.ReleaseSort))!;
        var release = typeof(Playable).GetProperty(nameof(Playable.Release))!;
        var releaseJsonMembers = typeof(Playable).GetProperties()
            .Where(p => p.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>()?.Name == "release")
            .Select(p => p.Name)
            .ToList();

        // Act
        var releaseOrder = release.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyOrderAttribute>()!.Order;
        var sortOrder = releaseSort.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyOrderAttribute>()!.Order;

        // Assert
        releaseSort.SetMethod.Should().NotBeNull();
        releaseSort.SetMethod!.IsPublic.Should().BeFalse();
        sortOrder.Should().NotBe(releaseOrder);
        releaseJsonMembers.Should().Equal(nameof(Playable.Release));
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
        "Film, TvShowEpisode, and NewsReport carry a compact YouTubeId (ICatalogueYouTubeItem, JSON youTubeId) " +
        "but no other provider ids (no ids/Spotify/Apple): platform presence is otherwise services only.")]
    public void Non_podcast_playables_carry_youtube_id_but_no_other_provider_id_fields()
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

            type.Should().Implement<ICatalogueYouTubeItem>(because: type.Name);
            var youTubeId = type.GetProperty(nameof(ICatalogueYouTubeItem.YouTubeId));
            youTubeId.Should().NotBeNull(because: type.Name);
            youTubeId!.PropertyType.Should().Be(typeof(string), because: type.Name);
            youTubeId.DeclaringType.Should().Be(type, because: $"{type.Name} declares youTubeId itself, not via Playable");
            youTubeId.GetCustomAttribute<System.Text.Json.Serialization.JsonPropertyNameAttribute>()!.Name
                .Should().Be("youTubeId", because: type.Name);
        }

        // The compact id is not on the shared playable surface, and podcast episodes do not take it.
        typeof(Playable).GetProperty("YouTubeId").Should().BeNull();
        typeof(IPlayable).GetProperty("YouTubeId").Should().BeNull();
        typeof(Episode).Should().NotImplement<ICatalogueYouTubeItem>();
    }

    [Fact(DisplayName =
        "Film, TvShowEpisode, and NewsReport serialize youTubeId when set, omit it when blank, " +
        "and never write ids/spotifyId/appleId.")]
    public void Non_podcast_playables_serialize_youtube_id_only()
    {
        // Arrange
        var youTubeIdValue = _domain.CreateYouTubeId();
        ICatalogueYouTubeItem[] withId =
        [
            new Film(_fixture.Create<string>()) { YouTubeId = youTubeIdValue },
            new TvShowEpisode(_fixture.Create<string>()) { YouTubeId = youTubeIdValue },
            new NewsReport(_fixture.Create<string>()) { YouTubeId = youTubeIdValue }
        ];
        ICatalogueYouTubeItem[] blank =
        [
            new Film(_fixture.Create<string>()) { YouTubeId = "  " },
            new TvShowEpisode(_fixture.Create<string>()) { YouTubeId = string.Empty },
            new NewsReport(_fixture.Create<string>()) { YouTubeId = null }
        ];

        // Act
        var withIdJson = withId
            .Select(item => (item.GetType().Name, JsonDocument.Parse(JsonSerializer.Serialize(item, item.GetType()))))
            .ToArray();
        var blankJson = blank
            .Select(item => (item.GetType().Name, JsonDocument.Parse(JsonSerializer.Serialize(item, item.GetType()))))
            .ToArray();

        // Assert
        foreach (var (name, doc) in withIdJson)
        {
            using (doc)
            {
                doc.RootElement.GetProperty("youTubeId").GetString().Should().Be(youTubeIdValue, because: name);
                doc.RootElement.EnumerateObject().Select(p => p.Name)
                    .Should().NotContain(ForbiddenProviderIdJsonKeys, because: name);
            }
        }

        foreach (var item in blank)
        {
            item.YouTubeId.Should().BeNull(because: item.GetType().Name);
        }

        foreach (var (name, doc) in blankJson)
        {
            using (doc)
            {
                var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
                keys.Should().NotContain("youTubeId", because: name);
                keys.Should().NotContain(ForbiddenProviderIdJsonKeys, because: name);
            }
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
        typeof(Playable).GetProperty(nameof(Playable.Release))!.PropertyType.Should().Be(typeof(CatalogueRelease));
        typeof(Film).GetProperty(nameof(Film.Release))!.PropertyType.Should().Be(typeof(CatalogueRelease));
        typeof(Episode).Should().BeAssignableTo<Playable>();
        typeof(TvShowEpisode).Should().BeAssignableTo<Playable>();
        typeof(NewsReport).Should().BeAssignableTo<Playable>();

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
        "Film stores an IMDb id and a TMDB id and no TheTVDB id; TvShow and TvShowEpisode store an IMDb id, " +
        "a TMDB id, and a TheTVDB id, because those are the three authority ids we collect.")]
    public void film_tv_show_and_tv_show_episode_store_canonical_authority_ids()
    {
        // Arrange
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var imdbId = _domain.CreateImdbId();
        var tvdbId = _domain.CreateTvdbId();
        var tmdbId = _domain.CreateTmdbId();
        var film = new Film(_domain.CreateTitle()) { ImdbId = imdbId, TmdbId = tmdbId };
        var show = new TvShow(_domain.CreateTitle())
        {
            ImdbId = imdbId,
            TvdbId = tvdbId,
            TmdbId = tmdbId
        };
        var episode = new TvShowEpisode(_domain.CreateTitle())
        {
            ImdbId = imdbId,
            TvdbId = tvdbId,
            TmdbId = tmdbId
        };

        // Act
        var filmJson = JsonSerializer.Serialize(film, options);
        var showJson = JsonSerializer.Serialize(show, options);
        var episodeJson = JsonSerializer.Serialize(episode, options);
        var filmRoundTrip = JsonSerializer.Deserialize<Film>(filmJson, options);
        var showRoundTrip = JsonSerializer.Deserialize<TvShow>(showJson, options);
        var episodeRoundTrip = JsonSerializer.Deserialize<TvShowEpisode>(episodeJson, options);

        // Assert
        typeof(IFilmCanonical).GetProperty("Imdb").Should().BeNull();
        typeof(ITvCanonical).GetProperty("Imdb").Should().BeNull();
        typeof(ITvCanonical).GetProperty("Tvdb").Should().BeNull();
        typeof(Film).GetProperty("Imdb").Should().BeNull();
        typeof(TvShow).GetProperty("Imdb").Should().BeNull();
        typeof(TvShow).GetProperty("Tvdb").Should().BeNull();
        typeof(TvShowEpisode).GetProperty("Imdb").Should().BeNull();
        typeof(TvShowEpisode).GetProperty("Tvdb").Should().BeNull();
        typeof(IFilmCanonical).GetProperty("Tvdb").Should().BeNull();
        typeof(Film).GetProperty("Tvdb").Should().BeNull();
        typeof(IFilmCanonical).GetProperty(nameof(IFilmCanonical.ImdbId))!.PropertyType.Should().Be(typeof(string));
        typeof(IFilmCanonical).GetProperty(nameof(IFilmCanonical.TmdbId))!.PropertyType.Should().Be(typeof(int?));
        typeof(IFilmCanonical).GetProperty("TvdbId").Should().BeNull();
        typeof(Film).GetProperty("TvdbId").Should().BeNull();
        typeof(ITvCanonical).GetProperty(nameof(ITvCanonical.ImdbId))!.PropertyType.Should().Be(typeof(string));
        typeof(ITvCanonical).GetProperty(nameof(ITvCanonical.TmdbId))!.PropertyType.Should().Be(typeof(int?));
        typeof(ITvCanonical).GetProperty(nameof(ITvCanonical.TvdbId))!.PropertyType.Should().Be(typeof(long?));
        typeof(TvShow).GetProperty(nameof(TvShow.TmdbId))!.PropertyType.Should().Be(typeof(int?));
        typeof(TvShowEpisode).GetProperty(nameof(TvShowEpisode.TmdbId))!.PropertyType.Should().Be(typeof(int?));
        filmJson.Should().Contain($"\"imdbId\":\"{imdbId}\"");
        filmJson.Should().Contain($"\"tmdbId\":{tmdbId.ToString(CultureInfo.InvariantCulture)}");
        filmJson.Should().NotContain("\"imdb\"");
        filmJson.Should().NotContain("\"tvdb\"");
        filmJson.Should().NotContain("tvdbId");
        showJson.Should().Contain($"\"imdbId\":\"{imdbId}\"");
        showJson.Should().Contain($"\"tvdbId\":{tvdbId.ToString(CultureInfo.InvariantCulture)}");
        showJson.Should().Contain($"\"tmdbId\":{tmdbId.ToString(CultureInfo.InvariantCulture)}");
        showJson.Should().NotContain("\"imdb\"");
        showJson.Should().NotContain("\"tvdb\"");
        episodeJson.Should().Contain($"\"imdbId\":\"{imdbId}\"");
        episodeJson.Should().Contain($"\"tvdbId\":{tvdbId.ToString(CultureInfo.InvariantCulture)}");
        episodeJson.Should().Contain($"\"tmdbId\":{tmdbId.ToString(CultureInfo.InvariantCulture)}");
        episodeJson.Should().NotContain("\"imdb\"");
        episodeJson.Should().NotContain("\"tvdb\"");
        JsonSerializer.Serialize(new Film(_domain.CreateTitle()), options).Should().NotContain("\"tmdbId\"");
        JsonSerializer.Serialize(new TvShow(_domain.CreateTitle()), options).Should().NotContain("\"tmdbId\"");
        JsonSerializer.Serialize(new TvShowEpisode(_domain.CreateTitle()), options).Should().NotContain("\"tmdbId\"");
        filmRoundTrip!.ImdbId.Should().Be(imdbId);
        filmRoundTrip.TmdbId.Should().Be(tmdbId);
        showRoundTrip!.ImdbId.Should().Be(imdbId);
        showRoundTrip.TvdbId.Should().Be(tvdbId);
        showRoundTrip.TmdbId.Should().Be(tmdbId);
        episodeRoundTrip!.ImdbId.Should().Be(imdbId);
        episodeRoundTrip.TvdbId.Should().Be(tvdbId);
        episodeRoundTrip.TmdbId.Should().Be(tmdbId);
        IFilmCanonical asFilm = film;
        ITvCanonical asShow = show;
        ITvCanonical asEpisode = episode;
        asFilm.ImdbId.Should().Be(imdbId);
        asFilm.TmdbId.Should().Be(tmdbId);
        asShow.ImdbId.Should().Be(imdbId);
        asShow.TvdbId.Should().Be(tvdbId);
        asShow.TmdbId.Should().Be(tmdbId);
        asEpisode.ImdbId.Should().Be(imdbId);
        asEpisode.TvdbId.Should().Be(tvdbId);
        asEpisode.TmdbId.Should().Be(tmdbId);
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
        episode.PublisherSearchTerms.Should().Be(searchTerms);
        episode.PublisherLanguage.Should().Be(language);
        episode.ParentRemoved.Should().BeTrue();
        episode.ParentMetadataVersion.Should().Be(metadataVersion);
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
        episode.PublisherSearchTerms.Should().Be(searchTerms);
        episode.PublisherLanguage.Should().Be(language);
        episode.ParentRemoved.Should().BeFalse();
        episode.ParentMetadataVersion.Should().Be(metadataVersion);
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
        report.PublisherSearchTerms.Should().Be(searchTerms);
        report.PublisherLanguage.Should().Be(language);
        report.ParentRemoved.Should().BeTrue();
        report.ParentMetadataVersion.Should().Be(metadataVersion);
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
        report.PublisherSearchTerms.Should().Be(searchTerms);
        report.PublisherLanguage.Should().Be(language);
        report.ParentRemoved.Should().BeFalse();
        report.ParentMetadataVersion.Should().Be(metadataVersion);
    }

    [Fact(DisplayName =
        "INTEGRITY PublisherSearchTerms JSON: Episode, TvShowEpisode, and NewsReport all serialize " +
        "Playable publisherSearchTerms/publisherLanguage only; never podcast*, tvShow*, newsOrganisation*, " +
        "or PascalCase; values round-trip through the shared Playable members.")]
    public void Publisher_denormalised_fields_serialize_with_correct_wire_names_only()
    {
        // Arrange
        var searchTerms = _fixture.Create<string>();
        var language = _fixture.Create<string>();
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var episode = new Episode
        {
            PublisherSearchTerms = searchTerms,
            PublisherLanguage = language
        };
        var tvShowEpisode = new TvShowEpisode
        {
            PublisherSearchTerms = searchTerms,
            PublisherLanguage = language
        };
        var newsReport = new NewsReport
        {
            PublisherSearchTerms = searchTerms,
            PublisherLanguage = language
        };

        // Act
        var episodeJson = JsonSerializer.Serialize(episode, options);
        var tvJson = JsonSerializer.Serialize(tvShowEpisode, options);
        var newsJson = JsonSerializer.Serialize(newsReport, options);
        var episodeRoundTrip = JsonSerializer.Deserialize<Episode>(episodeJson, options);
        var tvRoundTrip = JsonSerializer.Deserialize<TvShowEpisode>(tvJson, options);
        var newsRoundTrip = JsonSerializer.Deserialize<NewsReport>(newsJson, options);

        // Assert — all three kinds: publisher* only
        foreach (var json in new[] { episodeJson, tvJson, newsJson })
        {
            json.Should().Contain("\"publisherSearchTerms\"");
            json.Should().Contain("\"publisherLanguage\"");
            json.Should().NotContain("podcastSearchTerms");
            json.Should().NotContain("podcastLanguage");
            json.Should().NotContain("tvShowSearchTerms");
            json.Should().NotContain("tvShowLanguage");
            json.Should().NotContain("newsOrganisationSearchTerms");
            json.Should().NotContain("newsOrganisationLanguage");
            json.Should().NotContain("PublisherSearchTerms");
            json.Should().NotContain("PublisherLanguage");
        }

        episodeRoundTrip!.PublisherSearchTerms.Should().Be(searchTerms);
        episodeRoundTrip.PublisherLanguage.Should().Be(language);
        tvRoundTrip!.PublisherSearchTerms.Should().Be(searchTerms);
        tvRoundTrip.PublisherLanguage.Should().Be(language);
        newsRoundTrip!.PublisherSearchTerms.Should().Be(searchTerms);
        newsRoundTrip.PublisherLanguage.Should().Be(language);
    }

    [Fact(DisplayName =
        "INTEGRITY PublisherSearchTerms JSON: deserializing publisher* JSON populates PublisherSearchTerms/" +
        "PublisherLanguage on Episode, TvShowEpisode, and NewsReport.")]
    public void Publisher_denormalised_fields_deserialize_from_publisher_wire_names()
    {
        // Arrange
        var searchTerms = _fixture.Create<string>();
        var language = _fixture.Create<string>();
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json =
            $"{{\"publisherSearchTerms\":\"{searchTerms}\",\"publisherLanguage\":\"{language}\"}}";

        // Act
        var episode = JsonSerializer.Deserialize<Episode>(json, options);
        var tvShowEpisode = JsonSerializer.Deserialize<TvShowEpisode>(json, options);
        var newsReport = JsonSerializer.Deserialize<NewsReport>(json, options);

        // Assert
        episode!.PublisherSearchTerms.Should().Be(searchTerms);
        episode.PublisherLanguage.Should().Be(language);
        tvShowEpisode!.PublisherSearchTerms.Should().Be(searchTerms);
        tvShowEpisode.PublisherLanguage.Should().Be(language);
        newsReport!.PublisherSearchTerms.Should().Be(searchTerms);
        newsReport.PublisherLanguage.Should().Be(language);
    }

    [Fact(DisplayName =
        "Episode JSON carrying the retired podcastSearchTerms/podcastLanguage keys no longer fills " +
        "PublisherSearchTerms/PublisherLanguage, and a subsequent serialize emits neither retired key, because " +
        "the GATE 5 bridge drop reads publisher* only.")]
    public void Episode_retired_podcast_wire_names_are_ignored()
    {
        // Arrange
        var searchTerms = _fixture.Create<string>();
        var language = _fixture.Create<string>();
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var retiredJson =
            $"{{\"podcastSearchTerms\":\"{searchTerms}\",\"podcastLanguage\":\"{language}\"}}";

        // Act
        var episode = JsonSerializer.Deserialize<Episode>(retiredJson, options);
        var rewrittenJson = JsonSerializer.Serialize(episode, options);

        // Assert
        episode!.PublisherSearchTerms.Should().BeNull();
        episode.PublisherLanguage.Should().BeNull();
        rewrittenJson.Should().NotContain(searchTerms);
        rewrittenJson.Should().NotContain(language);
        rewrittenJson.Should().NotContain("podcastSearchTerms");
        rewrittenJson.Should().NotContain("podcastLanguage");
    }

    [Fact(DisplayName =
        "INTEGRITY Parent* JSON: Episode, TvShowEpisode, and NewsReport serialize parentMetadataVersion/" +
        "parentRemoved only; Episode JSON with the retired podcastMetadataVersion/podcastRemoved keys leaves " +
        "Parent* unset and rewrites without them.")]
    public void Parent_denormalised_fields_use_shared_wire_names()
    {
        // Arrange
        var metadataVersion = _fixture.Create<long>();
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        var episode = new Episode { ParentMetadataVersion = metadataVersion, ParentRemoved = true };
        var tvShowEpisode = new TvShowEpisode { ParentMetadataVersion = metadataVersion, ParentRemoved = true };
        var newsReport = new NewsReport { ParentMetadataVersion = metadataVersion, ParentRemoved = true };
        var legacyJson =
            $"{{\"podcastMetadataVersion\":{metadataVersion},\"podcastRemoved\":true}}";

        // Act
        var episodeJson = JsonSerializer.Serialize(episode, options);
        var tvJson = JsonSerializer.Serialize(tvShowEpisode, options);
        var newsJson = JsonSerializer.Serialize(newsReport, options);
        var fromLegacy = JsonSerializer.Deserialize<Episode>(legacyJson, options);
        var rewritten = JsonSerializer.Serialize(fromLegacy, options);

        // Assert
        foreach (var json in new[] { episodeJson, tvJson, newsJson })
        {
            json.Should().Contain("\"parentMetadataVersion\"");
            json.Should().Contain("\"parentRemoved\"");
            json.Should().NotContain("podcastMetadataVersion");
            json.Should().NotContain("podcastRemoved");
            json.Should().NotContain("tvShowMetadataVersion");
            json.Should().NotContain("tvShowRemoved");
            json.Should().NotContain("newsOrganisationMetadataVersion");
            json.Should().NotContain("newsOrganisationRemoved");
        }

        fromLegacy!.ParentMetadataVersion.Should().BeNull();
        fromLegacy.ParentRemoved.Should().BeNull();
        rewritten.Should().NotContain("parentMetadataVersion");
        rewritten.Should().NotContain("parentRemoved");
        rewritten.Should().NotContain("podcastMetadataVersion");
        rewritten.Should().NotContain("podcastRemoved");
    }
}
