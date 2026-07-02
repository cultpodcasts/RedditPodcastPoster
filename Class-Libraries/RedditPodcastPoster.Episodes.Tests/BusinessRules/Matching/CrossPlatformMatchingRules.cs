using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
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
    private const string C2CSpotifyId = "6O1Z1s7ca0PI8Gq1rdt3j4";
    private static readonly Uri C2CSpotifyUrl = new($"https://open.spotify.com/episode/{C2CSpotifyId}");
    private static readonly Guid C2CExistingId = Guid.Parse("7dd136da-84ae-4c02-81be-9baa5f4c3362");

    private readonly EpisodeMerger _merger = new(new EpisodeMatcher(NullLogger<EpisodeMatcher>.Instance));

    [Fact(DisplayName =
        "For YouTube-first podcasts, a Spotify catalogue episode may match a YouTube-only stored episode " +
        "when title and duration fuzzy-match and catalogue release aligns after publishing-delay adjustment.")]
    public void YouTube_first_Spotify_catalogue_matches_YouTube_only_stored_episode()
    {
        // Given a Cults to Consciousness episode stored from YouTube only (C2C abuser incident)
        var podcast = PodcastFixtures.CultsToConsciousness();
        var youTubeRelease = new DateTime(2026, 6, 4, 13, 8, 6, DateTimeKind.Utc);
        var youTubeLength = TimeSpan.Parse("01:28:37");
        var youTubeUrl = new Uri("https://www.youtube.com/watch?v=UsqC0L9He2g");
        var stored = new Episode
        {
            Id = C2CExistingId,
            PodcastId = podcast.Id,
            Title = "I Confronted My Ab*ser 30 Years Later. Everything Changed",
            Release = youTubeRelease,
            Length = youTubeLength,
            YouTubeId = "UsqC0L9He2g",
            Urls = new ServiceUrls { YouTube = youTubeUrl }
        };
        var expected = EpisodeExpectation.From(stored)
            .WithSpotify(C2CSpotifyId, C2CSpotifyUrl);

        // When Spotify catalogue returns a fuzzy-matching title with aligned catalogue release
        var discovered = EpisodeFixtures.FromSpotifyCatalogue(
            C2CSpotifyId,
            "I Confronted My Abuser 30 Years Later… Everything Changed",
            C2CSpotifyUrl,
            new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc),
            TimeSpan.Parse("01:31:59.6990000"));

        // Then indexing merges onto the YouTube-only row and fills Spotify identity without changing release
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        result.AddedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(C2CExistingId);
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "For YouTube-first podcasts with negative publishing delay, episodes must not merge on " +
        "release-and-duration alone when titles clearly refer to different episodes.")]
    public void Negative_delay_does_not_merge_on_release_and_duration_when_titles_differ()
    {
        // Given a YouTube-first podcast and a YouTube-only stored episode (C2C negative-delay incident)
        var podcast = PodcastFixtures.CultsToConsciousness();
        var stored = new Episode
        {
            Id = Guid.Parse("53ba0c64-58a7-4292-b7fe-ba135d4d3160"),
            PodcastId = podcast.Id,
            Title = "Why He Thinks Daughters Should Parent Their Siblings  (ft. Tia Levings)",
            Release = new DateTime(2026, 5, 31, 21, 15, 27, DateTimeKind.Utc),
            Length = TimeSpan.FromMinutes(61) + TimeSpan.FromSeconds(35),
            YouTubeId = "u6ZF-2sWQQc",
            Urls = new ServiceUrls { YouTube = new Uri("https://www.youtube.com/watch?v=u6ZF-2sWQQc") }
        };
        var expected = EpisodeExpectation.From(stored);

        // When Spotify returns an episode with aligned release and similar duration but a different title
        var discovered = EpisodeFixtures.FromSpotifyCatalogue(
            "1BTQKaev5KLjScdwHII14B",
            "Becoming a Fundamentalist Trad Wife Almost Killed Me",
            new Uri("https://open.spotify.com/episode/1BTQKaev5KLjScdwHII14B"),
            new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromMinutes(61) + TimeSpan.FromSeconds(30));

        // Then indexing adds a new episode rather than merging onto the YouTube-only row
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

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
        // Given two stored episodes with the same title, release, and duration on different platforms
        var podcast = PodcastFixtures.Standard();
        var sharedTitle = "Shared episode title";
        var sharedRelease = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var sharedLength = TimeSpan.FromMinutes(45);
        var youTubeOnly = new Episode
        {
            Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            PodcastId = podcast.Id,
            Title = sharedTitle,
            Release = sharedRelease,
            Length = sharedLength,
            YouTubeId = "youtube-video-id",
            Urls = new ServiceUrls { YouTube = new Uri("https://www.youtube.com/watch?v=youtube-video-id") }
        };
        var appleOnly = new Episode
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            PodcastId = podcast.Id,
            Title = sharedTitle,
            Release = sharedRelease,
            Length = sharedLength,
            AppleId = 1234567890,
            Urls = new ServiceUrls { Apple = new Uri("https://podcasts.apple.com/us/podcast/episode/id1234567890") }
        };

        // When Spotify catalogue returns an episode that title-matches both stored rows
        const string incomingSpotifyId = "incomingSpotifyId01";
        var discovered = EpisodeFixtures.FromSpotifyCatalogue(
            incomingSpotifyId,
            sharedTitle,
            new Uri($"https://open.spotify.com/episode/{incomingSpotifyId}"),
            sharedRelease,
            sharedLength);

        // Then indexing records a merge failure containing both candidates
        var result = _merger.MergeEpisodes(podcast, [youTubeOnly, appleOnly], [discovered]);

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
        // Given a YouTube-first podcast with a one-day publishing delay and a Spotify-only stored row
        var podcast = PodcastFixtures.YouTubeFirst(
            channelId: "delayed-channel",
            youTubePublicationOffsetTicks: TimeSpan.FromDays(1).Ticks);
        var audioRelease = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var youTubeRelease = audioRelease.AddDays(1);
        var length = TimeSpan.FromHours(1);
        var stored = new Episode
        {
            Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
            PodcastId = podcast.Id,
            Title = "Episode A",
            Release = audioRelease,
            Length = length,
            Urls = new ServiceUrls { Spotify = new Uri("https://open.spotify.com/episode/delayedAudio01") }
        };
        var expected = EpisodeExpectation.From(stored)
            .WithYouTube("delayedYouTube01", new Uri("https://www.youtube.com/watch?v=delayedYouTube01"));

        // When YouTube returns a different title on the delayed publish date with the same duration
        var discovered = EpisodeFixtures.FromYouTubeVideo(
            "delayedYouTube01",
            "Completely different title",
            youTubeRelease,
            length);

        // Then indexing merges onto the stored audio row and fills YouTube identity
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(stored.Id);
        stored.ShouldMatchExpectation(expected);
    }
}
