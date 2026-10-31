using AutoFixture;
using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>
/// The search document's <c>release</c> is built from the playable's UTC release and must not depend on the host's
/// time zone (#1191). These facts only catch a regression when the host is not on UTC: on a UTC host the implicit
/// <see cref="DateTime"/> to <see cref="DateTimeOffset"/> conversion applies a zero offset and passes anyway. CI runs
/// on UTC, so <see cref="SearchReleaseEastOfUtcHostGuard"/> re-runs this class under TZ=Pacific/Auckland on every build.
/// </summary>
public class SearchReleaseUtcRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly Fixture _specimens = new();

    private int DaysAgo() => _specimens.Create<int>() % 365 + 1;

    [Fact(DisplayName =
        "When a film with no release is pushed to search, then the document's release is DateTimeOffset.MinValue at offset zero on any host time zone, because the unset release sort is an unspecified-kind DateTime.MinValue and reading it as host-local time threw east of UTC, so the upload never ran.")]
    public void film_without_release_has_min_release_at_utc()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };

        // Act
        var document = CataloguePlayableSearchDocument.For(film);

        // Assert
        film.Release.Should().BeNull();
        document.Release.Should().Be(DateTimeOffset.MinValue);
        document.Release.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact(DisplayName =
        "When a news report or TV episode with no release is pushed to search, then the document is built with release DateTimeOffset.MinValue at offset zero, because the news and TV push shares the same release conversion as the film push.")]
    public void news_and_tv_without_release_have_min_release_at_utc()
    {
        // Arrange
        var report = new NewsReport(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };

        // Act
        var reportDocument = CataloguePlayableSearchDocument.For(report);
        var episodeDocument = CataloguePlayableSearchDocument.For(episode);

        // Assert
        reportDocument.Release.Should().Be(DateTimeOffset.MinValue);
        reportDocument.Release.Offset.Should().Be(TimeSpan.Zero);
        episodeDocument.Release.Should().Be(DateTimeOffset.MinValue);
        episodeDocument.Release.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact(DisplayName =
        "When a film with a UTC release time is pushed to search, then the document's release is that same instant at offset zero, because search stores UTC and the host's local offset must never be applied.")]
    public void film_release_is_the_same_utc_instant()
    {
        // Arrange
        var release = DomainTestFixture.UtcAtTime(-DaysAgo(), _fixture.CreateNonMidnightTimeOfDay());
        var film = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        film.SetRelease(CatalogueRelease.FromDateTimeUtc(release));

        // Act
        var document = CataloguePlayableSearchDocument.For(film);

        // Assert
        document.Release.Offset.Should().Be(TimeSpan.Zero);
        document.Release.UtcDateTime.Should().Be(release);
    }

    [Fact(DisplayName =
        "When a film with a date-only release is pushed to search, then the document's release is midnight UTC on that date, because a date-only release sorts at midnight UTC and must not move to the previous or next day by the host's offset.")]
    public void film_date_only_release_is_midnight_utc()
    {
        // Arrange
        var date = DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(DaysAgo()));
        var film = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        film.SetRelease(CatalogueRelease.FromDate(date));

        // Act
        var document = CataloguePlayableSearchDocument.For(film);

        // Assert
        document.Release.Should().Be(new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
    }

    [Fact(DisplayName =
        "When a podcast episode with no release is pushed to search, then the record's release is DateTimeOffset.MinValue at offset zero on any host time zone, because the episode search record shares the release conversion and an unset episode release is an unspecified-kind DateTime.MinValue too.")]
    public void podcast_episode_without_release_has_min_release_at_utc()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e => e.Release = null);

        // Act
        var record = new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord();

        // Assert
        episode.ReleaseSort.Should().Be(default);
        record.Release.Should().Be(DateTimeOffset.MinValue);
        record.Release.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact(DisplayName =
        "When a podcast episode's release is set from an unspecified-kind time, then the search record's release is that wall-clock time read as UTC at offset zero, because an unspecified release is UTC by contract and must not move by the host's offset.")]
    public void podcast_episode_unspecified_release_is_read_as_utc()
    {
        // Arrange
        var release = DomainTestFixture.UtcAtTime(-DaysAgo(), _fixture.CreateNonMidnightTimeOfDay());
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast,
            e => e.ReleaseUtc = DateTime.SpecifyKind(release, DateTimeKind.Unspecified));

        // Act
        var record = new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord();

        // Assert
        record.Release.Offset.Should().Be(TimeSpan.Zero);
        record.Release.UtcDateTime.Should().Be(release);
    }

    [SearchReleaseGuardChildFact(DisplayName =
        "When the search release rules run in the guard's child test host, then the host's local zone is Pacific/Auckland at +13 in January and +12 in July, because if the zone data is missing .NET silently falls back to UTC and every other rule here would pass without testing anything.")]
    public void child_host_is_on_pacific_auckland_time()
    {
        // Arrange
        var summer = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        var winter = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var summerOffset = TimeZoneInfo.Local.GetUtcOffset(summer);
        var winterOffset = TimeZoneInfo.Local.GetUtcOffset(winter);

        // Assert
        summerOffset.Should().Be(TimeSpan.FromHours(13),
            "TZ={0} should give NZDT; local zone was '{1}'", SearchReleaseGuard.EastOfUtcZone, TimeZoneInfo.Local.Id);
        winterOffset.Should().Be(TimeSpan.FromHours(12),
            "TZ={0} should give NZST; local zone was '{1}'", SearchReleaseGuard.EastOfUtcZone, TimeZoneInfo.Local.Id);
    }
}
