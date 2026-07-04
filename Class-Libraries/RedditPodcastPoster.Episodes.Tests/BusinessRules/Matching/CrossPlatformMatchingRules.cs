using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// Cross-platform matching rules for YouTube-first podcasts and ambiguous merge detection.
/// </summary>
public class CrossPlatformMatchingRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly EpisodeMerger _merger = EpisodeDomainTestServices.CreateMerger();

    [Fact(DisplayName =
        "For YouTube-first podcasts, a Spotify catalogue episode may match a YouTube-only stored episode " +
        "when title and duration fuzzy-match and catalogue release aligns after publishing-delay adjustment.")]
    public void YouTube_first_Spotify_catalogue_matches_YouTube_only_stored_episode()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeFirstPodcastWithNegativeDelay();
        var (stored, discovered, spotifyId) = _fixture.CreateCrossPlatformYouTubeFirstPair(podcast);
        var expected = EpisodeExpectation.From(stored)
            .WithSpotify(spotifyId, _fixture.DefaultSpotifyUrl(spotifyId));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(stored.Id);
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "For YouTube-first podcasts with negative publishing delay, episodes must not merge on " +
        "release-and-duration alone when titles clearly refer to different episodes.")]
    public void Negative_delay_does_not_merge_on_release_and_duration_when_titles_differ()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeFirstPodcastWithNegativeDelay();
        var (stored, discovered) = _fixture.CreateNegativeDelayNonMatchingPair(podcast);
        var expected = EpisodeExpectation.From(stored);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.AddedEpisodes.Should().ContainSingle();
        result.AddedEpisodes.Single().Id.Should().NotBe(stored.Id);
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "When two stored episodes could both match an incoming episode, indexing must record merge failure " +
        "— not pick arbitrarily.")]
    public void Ambiguous_match_records_failed_episodes_instead_of_picking_one()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var sharedRelease = DomainTestFixture.UtcDaysAgo(32);
        var sharedLength = _fixture.CreateDuration();
        var sharedTitle = _fixture.CreateTitle();
        var (youTubeOnly, appleOnly) = _fixture.CreateAmbiguousMatchStoredEpisodes(
            podcast,
            sharedRelease,
            sharedLength,
            sharedTitle);
        var discovered = _fixture.CreateAmbiguousMatchSpotifyIncoming(
            sharedRelease,
            sharedLength,
            sharedTitle);

        // Act
        var result = _merger.MergeEpisodes(podcast, [youTubeOnly, appleOnly], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().ContainSingle();
        var failedCandidates = result.FailedEpisodes.Single().ToList();
        failedCandidates.Should().HaveCount(2);
        failedCandidates.Should().Contain(x => x.Id == youTubeOnly.Id);
        failedCandidates.Should().Contain(x => x.Id == appleOnly.Id);
    }

    [Fact(DisplayName =
        "For YouTube-first podcasts with positive publishing delay, an incoming YouTube episode " +
        "may match a stored audio episode when release aligns after delay adjustment.")]
    public void Positive_YouTube_delay_matches_incoming_YouTube_to_stored_audio_episode()
    {
        // Arrange
        var publishingDelay = TimeSpan.FromDays(1);
        var podcast = _fixture.CreateYouTubeFirstPodcast(
            channelId: _fixture.CreateYouTubeChannelId(),
            youTubePublicationOffsetTicks: publishingDelay.Ticks);
        var audioRelease = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        var length = _fixture.CreateDuration();
        var stored = _fixture.CreatePositiveDelayAudioStoredEpisode(
            podcast,
            audioRelease: audioRelease,
            length: length);
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithRelease(audioRelease.Add(publishingDelay))
            .WithDuration(length));
        var expected = EpisodeExpectation.From(stored)
            .WithYouTube(youTubeInput.YouTubeId, youTubeInput.YouTubeUrl);
        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId)
            .WithTitle(_fixture.CreateTitle())
            .WithRelease(youTubeInput.Release)
            .WithDuration(length));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(stored.Id);
        stored.ShouldMatchExpectation(expected);
    }
}
