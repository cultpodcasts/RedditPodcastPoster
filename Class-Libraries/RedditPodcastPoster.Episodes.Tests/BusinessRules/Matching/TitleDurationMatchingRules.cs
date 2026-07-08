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
        var release = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        var existingLength = _fixture.CreateDuration();
        var incomingLength = existingLength + TimeSpan.FromSeconds(1);
        var storedTitle = _fixture.CreateShortTitle();
        var incomingTitle = DomainTestFixture.CreateTypoTitleVariant(storedTitle);
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.CreateStoredEpisodeWithSpotifyOnly(
            podcast,
            release: release,
            length: existingLength,
            title: storedTitle);
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithTitle(incomingTitle)
            .WithRelease(release)
            .WithDuration(incomingLength));
        var expected = EpisodeExpectation.From(stored)
            .WithYouTube(youTubeInput.YouTubeId, youTubeInput.YouTubeUrl);
        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId)
            .WithTitle(incomingTitle)
            .WithRelease(release)
            .WithDuration(incomingLength));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(stored.Id);
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "When titles differ by a typo but duration does not match, episodes are not the same.")]
    public void Typo_with_mismatched_duration_does_not_match()
    {
        // Arrange
        var storedTitle = _fixture.CreateShortTitle();
        var incomingTitle = DomainTestFixture.CreateTypoTitleVariant(storedTitle);
        var existingLength = _fixture.CreateDuration();
        var incomingLength = existingLength + TimeSpan.FromMinutes(20);
        var existing = CreateEpisode(storedTitle, existingLength);
        var incoming = CreateEpisode(incomingTitle, incomingLength);

        // Act
        var isMatch = _matcher.IsMatch(existing, incoming, episodeMatchRegex: null, _fixture.CreatePodcast());

        // Assert
        isMatch.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When titles and duration differ but release and duration align within standard tolerance " +
        "on a podcast without YouTube release authority, episodes may be treated as the same.")]
    public void Release_and_duration_align_when_titles_differ_on_standard_podcast()
    {
        // Arrange
        var release = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        var length = _fixture.CreateDuration();
        var existing = CreateEpisode(_fixture.CreateTitle(), length, release);
        var incoming = CreateEpisode(_fixture.CreateTitle(), length, release);

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
        const string storedTitle = "#42 Stored episode about the first topic";
        const string discoveredTitle = "#42 Catalogue title with completely different wording";
        var podcast = _fixture.CreatePodcast();
        podcast.EpisodeMatchRegex = episodeMatchRegex;
        var sharedRelease = DomainTestFixture.UtcDaysAgo(124);
        var stored = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Title = storedTitle;
            e.Release = sharedRelease;
            e.Length = _fixture.CreateDuration();
        });
        var expected = EpisodeExpectation.From(stored);
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput(b => b
            .WithTitle(discoveredTitle)
            .WithRelease(sharedRelease.AddDays(7)));
        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(spotifyInput.SpotifyId)
            .WithTitle(discoveredTitle)
            .WithSpotifyUrl(spotifyInput.SpotifyUrl)
            .WithRelease(spotifyInput.Release));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(stored.Id);
        stored.ShouldMatchExpectation(expected.WithSpotify(spotifyInput.SpotifyId, spotifyInput.SpotifyUrl));
    }

    [Fact(DisplayName =
        "When EpisodeMatchRegex captures a title group, symbol differences between stored and incoming titles " +
        "may still match via CompareOptions.IgnoreSymbols.")]
    public void EpisodeMatchRegex_title_group_ignores_symbol_differences()
    {
        // Arrange
        const string episodeMatchRegex = @"Episode\s+(?'title'[\w\s]+)";
        var release = DomainTestFixture.UtcDaysAgo(12);
        var length = _fixture.CreateDuration();
        var stored = CreateEpisode("Episode Part One!", length, release);
        var incoming = CreateEpisode("Episode Part One", length, release);
        var podcast = _fixture.CreatePodcast();
        podcast.EpisodeMatchRegex = episodeMatchRegex;

        // Act
        var isMatch = _matcher.IsMatch(
            stored,
            incoming,
            new System.Text.RegularExpressions.Regex(episodeMatchRegex),
            podcast);

        // Assert
        isMatch.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When two episodes have different YouTube video IDs, IsMatch returns false even if titles align.")]
    public void Different_YouTube_ids_do_not_match_via_IsMatch()
    {
        // Arrange
        var sharedTitle = _fixture.CreateTitle();
        var sharedRelease = DomainTestFixture.UtcDaysAgo(8);
        var sharedLength = _fixture.CreateDuration();
        var existing = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithTitle(sharedTitle)
            .WithRelease(sharedRelease)
            .WithDuration(sharedLength));
        var incoming = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithTitle(sharedTitle)
            .WithRelease(sharedRelease)
            .WithDuration(sharedLength));

        // Act
        var isMatch = _matcher.IsMatch(existing, incoming, episodeMatchRegex: null, _fixture.CreatePodcast());

        // Assert
        isMatch.Should().BeFalse();
    }

    private Episode CreateEpisode(string title, TimeSpan length, DateTime? release = null) =>
        _fixture.CreateEpisode(e =>
        {
            e.Title = title;
            e.Length = length;
            e.Release = release ?? DateTime.UtcNow;
        });
}
