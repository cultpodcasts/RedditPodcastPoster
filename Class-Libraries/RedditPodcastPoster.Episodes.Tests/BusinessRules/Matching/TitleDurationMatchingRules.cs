using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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
    private static readonly Guid PostmormonExistingId = Guid.Parse("086b02d5-9ec7-432e-8e57-9279d32374da");

    private readonly EpisodeMatcher _matcher = new(NullLogger<EpisodeMatcher>.Instance);
    private readonly EpisodeMerger _merger = new(new EpisodeMatcher(NullLogger<EpisodeMatcher>.Instance));

    [Fact(DisplayName =
        "When titles differ by a typo but duration matches within tolerance, " +
        "episodes may be treated as the same.")]
    public void Typo_with_matching_duration_merges_onto_existing_episode()
    {
        // Given a Postmormon Postmortem episode stored from Spotify (Massacre vs Masscare typo on YouTube)
        var release = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var existingLength = TimeSpan.FromSeconds(878.503);
        var incomingLength = TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(39);
        var podcast = PodcastFixtures.Standard(name: "Postmormon Postmortem");
        var stored = new Episode
        {
            Id = PostmormonExistingId,
            PodcastId = podcast.Id,
            Title = "The Bear River Massacre and the Mormon History Behind Washakie Ward",
            Release = release,
            Length = existingLength,
            SpotifyId = SpotifyEpisodeId,
            Urls = new ServiceUrls { Spotify = SpotifyUrl }
        };
        var expected = EpisodeExpectation.From(stored)
            .WithYouTube("l_iHjZWIsXw", new Uri("https://www.youtube.com/watch?v=l_iHjZWIsXw"));

        // When YouTube returns a fuzzy-matching title with aligned duration
        var discovered = Episode.FromYouTube(
            "l_iHjZWIsXw",
            "The Bear River Masscare and the Mormon History Behind the Washakie Ward",
            "YouTube description",
            incomingLength,
            false,
            release,
            new Uri("https://www.youtube.com/watch?v=l_iHjZWIsXw"),
            null);

        // Then indexing merges onto the stored row and fills YouTube identity
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(PostmormonExistingId);
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "When titles differ by a typo but duration does not match, episodes are not the same.")]
    public void Typo_with_mismatched_duration_does_not_match()
    {
        // Given two episodes with fuzzy-matching titles but different durations
        var existing = CreateEpisode(
            "The Bear River Massacre and the Mormon History Behind Washakie Ward",
            TimeSpan.FromMinutes(45));
        var incoming = CreateEpisode(
            "The Bear River Masscare and the Mormon History Behind the Washakie Ward",
            TimeSpan.FromMinutes(30));

        // When the matcher evaluates the pair on a standard podcast
        var isMatch = _matcher.IsMatch(existing, incoming, episodeMatchRegex: null, PodcastFixtures.Standard());

        // Then they are not treated as the same episode
        isMatch.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When titles and duration differ but release and duration align within standard tolerance " +
        "on a non-YouTube-first podcast, episodes may be treated as the same.")]
    public void Release_and_duration_align_when_titles_differ_on_standard_podcast()
    {
        // Given a standard podcast and two episodes with different titles but aligned release and duration
        var release = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var length = TimeSpan.FromMinutes(45);
        var existing = CreateEpisode("Episode A", length, release);
        var incoming = CreateEpisode("Completely different title", length, release);

        // When the matcher evaluates the pair
        var isMatch = _matcher.IsMatch(existing, incoming, episodeMatchRegex: null, PodcastFixtures.Standard());

        // Then they may be treated as the same episode
        isMatch.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Custom EpisodeMatchRegex on the podcast may force a match when titles differ.")]
    public void EpisodeMatchRegex_forces_match_when_titles_differ()
    {
        // Given a podcast with an episode-number regex and two stored rows with different surface titles
        const string episodeMatchRegex = @"#(?'episodematch'\d+)\s";
        var podcast = PodcastFixtures.Standard();
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

        // When catalogue returns the same episode number with a different title and mismatched duration
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

        // Then indexing merges onto the stored row because the regex episodematch group aligns
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(stored.Id);
        stored.ShouldMatchExpectation(expected.WithSpotify(
            "regexForcedSpotify01",
            new Uri("https://open.spotify.com/episode/regexForcedSpotify01")));
    }

    private static Episode CreateEpisode(string title, TimeSpan length, DateTime? release = null) =>
        new()
        {
            Title = title,
            Length = length,
            Release = release ?? DateTime.UtcNow
        };
}
