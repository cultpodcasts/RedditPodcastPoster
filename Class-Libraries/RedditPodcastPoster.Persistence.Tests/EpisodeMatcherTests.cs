using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;
using Xunit;

namespace RedditPodcastPoster.Persistence.Tests;

public class EpisodeMatcherTests
{
    private static readonly Podcast DefaultPodcast = new();

    private readonly EpisodeMatcher _sut = new(NullLogger<EpisodeMatcher>.Instance);

    [Fact]
    public void IsMatch_WhenTitlesDifferByTypoButDurationMatches_ReturnsTrue()
    {
        var existing = CreateEpisode("The Bear River Massacre and the Mormon History Behind Washakie Ward", TimeSpan.FromSeconds(878.503));
        var incoming = CreateEpisode("The Bear River Masscare and the Mormon History Behind the Washakie Ward", TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(39));
        _sut.IsMatch(existing, incoming, episodeMatchRegex: null, DefaultPodcast).Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WhenFuzzyTitleMatchesButDurationDiffers_ReturnsFalse()
    {
        var existing = CreateEpisode("The Bear River Massacre and the Mormon History Behind Washakie Ward", TimeSpan.FromMinutes(45));
        var incoming = CreateEpisode("The Bear River Masscare and the Mormon History Behind the Washakie Ward", TimeSpan.FromMinutes(30));
        _sut.IsMatch(existing, incoming, episodeMatchRegex: null, DefaultPodcast).Should().BeFalse();
    }

    [Fact]
    public void IsMatch_WhenTitlesAndDurationDifferButReleaseMatches_ReturnsTrue()
    {
        var release = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var existing = CreateEpisode("Episode A", TimeSpan.FromMinutes(45), release);
        var incoming = CreateEpisode("Completely different title", TimeSpan.FromMinutes(45), release);
        _sut.IsMatch(existing, incoming, episodeMatchRegex: null, DefaultPodcast).Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WhenReleasesDifferByYouTubePublishingDelayAndIncomingHasYouTubeIdentity_ReturnsTrue()
    {
        var podcast = new Podcast { YouTubePublicationOffset = TimeSpan.FromDays(1).Ticks };
        var audioRelease = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
        var existing = CreateEpisode("Episode A", TimeSpan.FromHours(1), audioRelease);
        existing.Urls.Spotify = new Uri("https://open.spotify.com/episode/test");
        var incoming = CreateEpisode("Completely different title", TimeSpan.FromHours(1), audioRelease.AddDays(1));
        incoming.YouTubeId = "test-video";
        incoming.Urls.YouTube = new Uri("https://www.youtube.com/watch?v=test-video");
        _sut.IsMatch(existing, incoming, episodeMatchRegex: null, podcast).Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WhenNegativeDelayAndTitlesDiffer_DoesNotMatchByReleaseAndDurationAlone()
    {
        var podcast = new Podcast
        {
            ReleaseAuthority = Service.YouTube,
            YouTubePublicationOffset = TimeSpan.FromDays(-33).Add(TimeSpan.FromHours(-12)).Ticks
        };
        var existing = CreateEpisode(
            "Why He Thinks Daughters Should Parent Their Siblings",
            TimeSpan.FromMinutes(61) + TimeSpan.FromSeconds(35),
            new DateTime(2026, 5, 31, 21, 15, 27, DateTimeKind.Utc));
        existing.YouTubeId = "u6ZF-2sWQQc";
        existing.Urls.YouTube = new Uri("https://www.youtube.com/watch?v=u6ZF-2sWQQc");
        var incoming = CreateEpisode(
            "Becoming a Fundamentalist Trad Wife Almost Killed Me",
            TimeSpan.FromMinutes(61) + TimeSpan.FromSeconds(30),
            new DateTime(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc));
        incoming.SpotifyId = "1BTQKaev5KLjScdwHII14B";
        incoming.Urls.Spotify = new Uri("https://open.spotify.com/episode/1BTQKaev5KLjScdwHII14B");

        _sut.IsMatch(existing, incoming, episodeMatchRegex: null, podcast).Should().BeFalse();
    }

    private static Episode CreateEpisode(string title, TimeSpan length, DateTime? release = null) =>
        new() { Title = title, Length = length, Release = release ?? DateTime.UtcNow };
}
