using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Merging;

/// <summary>
/// Release-date merge rules characterize current EpisodeMerger behaviour before domain extraction.
/// </summary>
public class ReleaseDateMergingRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly EpisodeMerger _merger = EpisodeDomainTestServices.CreateMerger();

    [Fact(DisplayName =
        "When stored release is midnight UTC and YouTube provides a time on the same calendar date, " +
        "merge must backfill the time from YouTube.")]
    public void YouTube_same_UTC_date_backfills_midnight_release_time()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var dateOnlyRelease = DomainTestFixture.UtcDaysAgo(2);
        var youTubeRelease = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(12) + TimeSpan.FromMinutes(30));
        var stored = _fixture.CreateMidnightUtcSpotifyStoredEpisode(podcast, dateOnlyRelease);
        var expected = EpisodeExpectation.From(stored)
            .WithRelease(youTubeRelease)
            .WithYouTube("video-id", _fixture.DefaultYouTubeUrl("video-id"));

        var discovered = _fixture.CreateYouTubeCatalogueEpisode("video-id", release: youTubeRelease);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "When stored release is midnight UTC and Spotify provides a time on the same calendar date, " +
        "merge must not backfill the time — Spotify catalogue release is date-only.")]
    public void Spotify_same_date_does_not_backfill_midnight_release_time()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var dateOnlyRelease = DomainTestFixture.UtcDateDaysAgo(2);
        var spotifyRelease = DomainTestFixture.UtcDateDaysAgo(2);
        var stored = _fixture.CreateMidnightUtcSpotifyStoredEpisode(podcast, dateOnlyRelease);
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateSpotifyCatalogueEpisode(
            DomainTestFixture.Incidents.C2CAbuserSpotifyId,
            spotifyUrl: _fixture.DefaultSpotifyUrl(DomainTestFixture.Incidents.C2CAbuserSpotifyId),
            release: spotifyRelease);
        // Episode model may retain API time before normalization; merge must still treat Spotify as date-only.
        discovered.Release = dateOnlyRelease.AddHours(8);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().BeEmpty("Spotify catalogue merge must not backfill time-of-day");
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "For YouTube-authority podcasts, re-indexing from Spotify must not replace the YouTube publish datetime " +
        "with a newer Spotify catalogue date.")]
    public void YouTube_authority_preserves_YouTube_release_on_Spotify_reindex()
    {
        // Arrange
        var podcast = _fixture.CreateCultsToConsciousnessPodcast();
        var youTubeRelease = DomainTestFixture.Incidents.C2CAbuserYouTubeRelease;
        var spotifyCatalogueRelease = DomainTestFixture.Incidents.C2CAbuserSpotifyRelease;
        var stored = _fixture.CreateC2CYouTubeAuthorityStoredEpisode(
            podcast,
            release: youTubeRelease,
            length: TimeSpan.Parse("01:28:37"));
        var expected = EpisodeExpectation.From(stored);

        var discovered = _fixture.CreateC2CSpotifyIncoming(
            release: spotifyCatalogueRelease,
            length: TimeSpan.Parse("01:31:59.6990000"));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty("no fields changed when Spotify catalogue date is newer than YouTube publish");
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "When stored release is midnight UTC and YouTube provides a time on a different calendar date, " +
        "merge must not backfill the time.")]
    public void YouTube_different_UTC_date_does_not_backfill_midnight_release_time()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var dateOnlyRelease = DomainTestFixture.UtcDaysAgo(2);
        var youTubeRelease = DomainTestFixture.UtcAtTime(-1, TimeSpan.FromHours(12) + TimeSpan.FromMinutes(30));
        var stored = _fixture.CreateMidnightUtcSpotifyStoredEpisode(podcast, dateOnlyRelease);
        var expected = EpisodeExpectation.From(stored)
            .WithYouTube("video-id", _fixture.DefaultYouTubeUrl("video-id"));

        var discovered = _fixture.CreateYouTubeCatalogueEpisode("video-id", release: youTubeRelease);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "Apple may upgrade a date-only stored release to a full datetime when the calendar date matches.")]
    public void Apple_same_date_backfills_midnight_release_time()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        const long appleId = 1635013492;
        var dateOnlyRelease = DomainTestFixture.UtcDaysAgo(2);
        var appleRelease = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(15) + TimeSpan.FromMinutes(45));
        var stored = _fixture.CreateMidnightUtcSpotifyStoredEpisode(podcast, dateOnlyRelease);
        var expected = EpisodeExpectation.From(stored)
            .WithRelease(appleRelease)
            .WithApple(appleId, _fixture.DefaultAppleUrl(appleId));

        var discovered = _fixture.CreateAppleCatalogueEpisode(appleId, release: appleRelease);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }
}
