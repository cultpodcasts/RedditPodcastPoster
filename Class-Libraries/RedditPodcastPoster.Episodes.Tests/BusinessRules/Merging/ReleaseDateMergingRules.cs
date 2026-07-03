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
        var sharedTitle = _fixture.Create<string>();
        var sharedLength = _fixture.CreateDuration();
        var stored = _fixture.CreateMidnightUtcStoredEpisode(
            podcast, dateOnlyRelease, sharedTitle, sharedLength);
        var youTubeInput = _fixture.CreateYouTubeCatalogueInputSameDayAs(
            stored,
            configure: b => b.WithTitle(sharedTitle).WithDuration(sharedLength));
        var expected = EpisodeExpectation.From(stored)
            .WithRelease(youTubeInput.Release)
            .WithYouTube(youTubeInput.YouTubeId, youTubeInput.YouTubeUrl);

        var discovered = _fixture.CreateYouTubeCatalogueEpisodeSameDayAs(
            stored,
            youTubeInput.Release.TimeOfDay,
            configure: b => b
                .WithYouTubeId(youTubeInput.YouTubeId)
                .WithTitle(sharedTitle)
                .WithDuration(sharedLength));

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
        var stored = _fixture.CreateMidnightUtcStoredEpisode(podcast, dateOnlyRelease);
        var expected = EpisodeExpectation.From(stored);
        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(stored.SpotifyId)
            .WithSpotifyUrl(stored.Urls.Spotify!)
            .WithRelease(dateOnlyRelease));
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
        var podcast = _fixture.CreateYouTubeFirstPodcastWithNegativeDelay();
        var (storedTemplate, discovered, spotifyId) = _fixture.CreateCrossPlatformYouTubeFirstPair(podcast);
        var stored = _fixture.CreateStoredEpisodeWithYouTubeAndSpotify(
            podcast,
            spotifyId,
            storedTemplate.YouTubeId,
            storedTemplate.Release,
            storedTemplate.Length,
            storedTemplate.Title);
        var expected = EpisodeExpectation.From(stored);

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
        var youTubeRelease = DomainTestFixture.UtcAtTime(-1, _fixture.CreateNonMidnightTimeOfDay());
        var sharedTitle = _fixture.Create<string>();
        var sharedLength = _fixture.CreateDuration();
        var stored = _fixture.CreateMidnightUtcStoredEpisode(
            podcast, dateOnlyRelease, sharedTitle, sharedLength);
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithTitle(sharedTitle)
            .WithRelease(youTubeRelease)
            .WithDuration(sharedLength));
        var expected = EpisodeExpectation.From(stored)
            .WithYouTube(youTubeInput.YouTubeId, youTubeInput.YouTubeUrl);

        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId)
            .WithTitle(sharedTitle)
            .WithRelease(youTubeRelease)
            .WithDuration(sharedLength));

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
        var dateOnlyRelease = DomainTestFixture.UtcDaysAgo(2);
        var sharedTitle = _fixture.Create<string>();
        var sharedLength = _fixture.CreateDuration();
        var stored = _fixture.CreateMidnightUtcStoredEpisode(
            podcast, dateOnlyRelease, sharedTitle, sharedLength);
        var appleInput = _fixture.CreateAppleCatalogueInputSameDayAs(
            stored,
            configure: b => b.WithTitle(sharedTitle).WithDuration(sharedLength));
        var expected = EpisodeExpectation.From(stored)
            .WithRelease(appleInput.Release)
            .WithApple(appleInput.AppleId, appleInput.AppleUrl);

        var discovered = _fixture.CreateAppleCatalogueEpisodeSameDayAs(
            stored,
            appleInput.Release.TimeOfDay,
            configure: b => b
                .WithAppleId(appleInput.AppleId)
                .WithTitle(sharedTitle)
                .WithDuration(sharedLength));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }
}
