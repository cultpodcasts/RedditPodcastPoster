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
    private const string SpotifyEpisodeId = "1UncRhHtmojlTq2mO0Gntz";
    private static readonly Uri SpotifyUrl = new($"https://open.spotify.com/episode/{SpotifyEpisodeId}");

    private readonly DomainTestFixture _fixture = new();
    private readonly EpisodeMatcher _matcher = EpisodeDomainTestServices.CreateMatcher();
    private readonly EpisodeMerger _merger = EpisodeDomainTestServices.CreateMerger();

    [Fact(DisplayName =
        "When titles differ by a typo but duration matches within tolerance, " +
        "episodes may be treated as the same.")]
    public void Typo_with_matching_duration_merges_onto_existing_episode()
    {
        // Arrange
        var release = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var existingLength = TimeSpan.FromSeconds(878.503);
        var incomingLength = TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(39);
        var podcast = _fixture.CreatePodcast(p => p.Name = "Postmormon Postmortem");
        var stored = _fixture.CreateEpisode(e =>
        {
            e.Id = DomainTestFixture.Incidents.PostmormonExistingEpisodeId;
            e.PodcastId = podcast.Id;
            e.Title = "The Bear River Massacre and the Mormon History Behind Washakie Ward";
            e.Release = release;
            e.Length = existingLength;
            e.SpotifyId = SpotifyEpisodeId;
            e.Urls = new ServiceUrls { Spotify = SpotifyUrl };
        });
        var expected = EpisodeExpectation.From(stored)
            .WithYouTube("l_iHjZWIsXw", new Uri("https://www.youtube.com/watch?v=l_iHjZWIsXw"));

        var discovered = Episode.FromYouTube(
            "l_iHjZWIsXw",
            "The Bear River Masscare and the Mormon History Behind the Washakie Ward",
            "YouTube description",
            incomingLength,
            false,
            release,
            new Uri("https://www.youtube.com/watch?v=l_iHjZWIsXw"),
            null);

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
            "The Bear River Massacre and the Mormon History Behind Washakie Ward",
            TimeSpan.FromMinutes(45));
        var incoming = CreateEpisode(
            "The Bear River Masscare and the Mormon History Behind the Washakie Ward",
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
        var release = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
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
        var sharedRelease = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var stored = new Episode
        {
            Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            PodcastId = podcast.Id,
            Title = "#42 Stored episode about the first topic",
            Release = sharedRelease,
            Length = TimeSpan.FromMinutes(30)
        };
        var expected = EpisodeExpectation.From(stored);

        var discovered = new Episode
        {
            Title = "#42 Catalogue title with completely different wording",
            Release = sharedRelease.AddDays(7),
            Length = TimeSpan.FromHours(2),
            SpotifyId = "regexForcedSpotify01",
            Urls = new ServiceUrls
            {
                Spotify = new Uri("https://open.spotify.com/episode/regexForcedSpotify01")
            }
        };

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(stored.Id);
        stored.ShouldMatchExpectation(expected.WithSpotify(
            "regexForcedSpotify01",
            new Uri("https://open.spotify.com/episode/regexForcedSpotify01")));
    }

    private Episode CreateEpisode(string title, TimeSpan length, DateTime? release = null) =>
        _fixture.CreateEpisode(e =>
        {
            e.Title = title;
            e.Length = length;
            e.Release = release ?? DateTime.UtcNow;
        });
}
