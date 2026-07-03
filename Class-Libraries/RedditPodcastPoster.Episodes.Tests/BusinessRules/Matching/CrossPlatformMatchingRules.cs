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
        var podcast = _fixture.CreateCultsToConsciousnessPodcast();
        var youTubeRelease = DomainTestFixture.Incidents.C2CAbuserYouTubeRelease;
        var youTubeLength = DomainTestFixture.Incidents.C2CAbuserYouTubeLength;
        var stored = _fixture.CreateC2CYouTubeOnlyStoredEpisode(podcast, release: youTubeRelease, length: youTubeLength);
        var expected = EpisodeExpectation.From(stored)
            .WithSpotify(
                DomainTestFixture.Incidents.C2CAbuserSpotifyId,
                _fixture.DefaultSpotifyUrl(DomainTestFixture.Incidents.C2CAbuserSpotifyId));

        var discovered = _fixture.CreateC2CSpotifyIncoming(
            release: DomainTestFixture.Incidents.C2CAbuserSpotifyRelease,
            length: DomainTestFixture.Incidents.C2CAbuserSpotifyLength);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(DomainTestFixture.Incidents.C2CAbuserEpisodeId);
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "For YouTube-first podcasts with negative publishing delay, episodes must not merge on " +
        "release-and-duration alone when titles clearly refer to different episodes.")]
    public void Negative_delay_does_not_merge_on_release_and_duration_when_titles_differ()
    {
        // Arrange
        var podcast = _fixture.CreateCultsToConsciousnessPodcast();
        var stored = _fixture.CreateC2CNegativeDelayStoredEpisode(podcast);
        var expected = EpisodeExpectation.From(stored);
        var discovered = _fixture.CreateC2CNegativeDelaySpotifyIncoming();

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
        var sharedLength = TimeSpan.FromMinutes(45);
        var (youTubeOnly, appleOnly) = _fixture.CreateAmbiguousMatchStoredEpisodes(
            podcast,
            sharedRelease,
            sharedLength);
        var discovered = _fixture.CreateAmbiguousMatchSpotifyIncoming(sharedRelease, sharedLength);

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
        var podcast = _fixture.CreateYouTubeFirstPodcast(
            channelId: "delayed-channel",
            youTubePublicationOffsetTicks: TimeSpan.FromDays(1).Ticks);
        var audioRelease = DomainTestFixture.UtcAtTime(-2, TimeSpan.FromHours(12));
        var youTubeRelease = audioRelease.AddDays(1);
        var length = TimeSpan.FromHours(1);
        var stored = _fixture.CreatePositiveDelayAudioStoredEpisode(
            podcast,
            audioRelease: audioRelease,
            length: length);
        var expected = EpisodeExpectation.From(stored)
            .WithYouTube(
                DomainTestFixture.Incidents.PositiveDelayIncomingYouTubeId,
                _fixture.DefaultYouTubeUrl(DomainTestFixture.Incidents.PositiveDelayIncomingYouTubeId));

        var discovered = _fixture.CreateYouTubeCatalogueEpisode(
            DomainTestFixture.Incidents.PositiveDelayIncomingYouTubeId,
            "Completely different title",
            youTubeRelease,
            length);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(stored.Id);
        stored.ShouldMatchExpectation(expected);
    }
}
