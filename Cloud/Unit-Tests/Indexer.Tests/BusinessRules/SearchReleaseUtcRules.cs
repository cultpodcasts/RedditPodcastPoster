using AutoFixture;
using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using Xunit;

namespace Indexer.Tests.BusinessRules;

/// <summary>
/// The search document's <c>release</c> is built from the playable's UTC release and must not depend on the host's
/// time zone (#1191). Run it under a non-UTC TZ (e.g. TZ=Pacific/Auckland) to prove that.
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
}
