using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// Title, duration, release, and regex matching heuristics before domain extraction.
/// </summary>
public class TitleDurationMatchingRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly EpisodeMatcher _matcher = EpisodeDomainTestServices.CreateMatcher();
    private readonly EpisodeMerger _merger = EpisodeDomainTestServices.CreateMerger();

    [Fact(DisplayName =
        "When titles differ by a typo but duration matches within tolerance, " +
        "episodes may be treated as the same.")]
    public void Typo_with_matching_duration_merges_onto_existing_episode()
    {
        // Arrange
        var release = DomainTestFixture.Incidents.PostmormonRelease;
        var existingLength = TimeSpan.FromSeconds(878.503);
        var incomingLength = TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(39);
        var podcast = _fixture.CreatePostmormonPodcast();
        var stored = _fixture.CreatePostmormonStoredEpisode(
            podcast,
            release: release,
            length: existingLength);
        var expected = EpisodeExpectation.From(stored)
            .WithYouTube(
                DomainTestFixture.Incidents.PostmormonYouTubeId,
                _fixture.DefaultYouTubeUrl(DomainTestFixture.Incidents.PostmormonYouTubeId));
        var discovered = _fixture.CreatePostmormonYouTubeIncoming(release: release, length: incomingLength);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(DomainTestFixture.Incidents.PostmormonExistingEpisodeId);
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "When titles differ by a typo but duration does not match, episodes are not the same.")]
    public void Typo_with_mismatched_duration_does_not_match()
    {
        // Arrange
        var existing = CreateEpisode(
            DomainTestFixture.Incidents.PostmormonStoredTitle,
            TimeSpan.FromMinutes(45));
        var incoming = CreateEpisode(
            DomainTestFixture.Incidents.PostmormonIncomingYouTubeTitle,
            TimeSpan.FromMinutes(30));

        // Act
        var isMatch = _matcher.IsMatch(existing, incoming, episodeMatchRegex: null, _fixture.CreatePodcast());

        // Assert
        isMatch.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When titles and duration differ but release and duration align within standard tolerance " +
        "on a non-YouTube-first podcast, episodes may be treated as the same.")]
    public void Release_and_duration_align_when_titles_differ_on_standard_podcast()
    {
        // Arrange
        var release = DomainTestFixture.Incidents.PostmormonRelease;
        var length = TimeSpan.FromMinutes(45);
        var existing = CreateEpisode("Episode A", length, release);
        var incoming = CreateEpisode("Completely different title", length, release);

        // Act
        var isMatch = _matcher.IsMatch(existing, incoming, episodeMatchRegex: null, _fixture.CreatePodcast());

        // Assert
        isMatch.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Custom EpisodeMatchRegex on the podcast may force a match when titles differ.")]
    public void EpisodeMatchRegex_forces_match_when_titles_differ()
    {
        // Arrange
        const string episodeMatchRegex = @"#(?'episodematch'\d+)\s";
        var podcast = _fixture.CreatePodcast();
        podcast.EpisodeMatchRegex = episodeMatchRegex;
        var sharedRelease = DomainTestFixture.UtcDaysAgo(124);
        var stored = _fixture.CreateEpisodeMatchRegexStoredEpisode(podcast, release: sharedRelease);
        var expected = EpisodeExpectation.From(stored);
        var discovered = _fixture.CreateEpisodeMatchRegexDiscoveredEpisode(release: sharedRelease.AddDays(7));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(stored.Id);
        stored.ShouldMatchExpectation(expected.WithSpotify(
            DomainTestFixture.Incidents.EpisodeMatchRegexSpotifyId,
            _fixture.DefaultSpotifyUrl(DomainTestFixture.Incidents.EpisodeMatchRegexSpotifyId)));
    }

    private Episode CreateEpisode(string title, TimeSpan length, DateTime? release = null) =>
        _fixture.CreateEpisode(e =>
        {
            e.Title = title;
            e.Length = length;
            e.Release = release ?? DateTime.UtcNow;
        });
}
