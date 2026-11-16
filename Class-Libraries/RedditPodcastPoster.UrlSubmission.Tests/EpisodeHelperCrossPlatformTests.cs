using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Text.Matchers;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Matching;
using RedditPodcastPoster.UrlSubmission.Models;
using Xunit;

namespace RedditPodcastPoster.UrlSubmission.Tests;

/// <summary>
/// #1445 regression: The Deep Dive Project, Spotify 3RRicdUGPZjGvpMd5bjvEG submitted against the
/// stored YouTube episode WARlofTXwzc (dc7488e3). Titles were edited between platforms (WRatio ~89),
/// releases 16 seconds apart, durations within a second.
/// </summary>
public class EpisodeHelperCrossPlatformTests
{
    private const string YouTubeTitle =
        "Celebrity exorcist who gave UNQUALIFIED \"medical advice\" SILENCED by bishop for ONE YEAR (pt.II)";

    private const string SpotifyTitle =
        "Celebrity exorcist who gave BAD medical \"advice\" PUBLICLY SILENCED by bishop for ONE YEAR (pt.II)";

    private const string YouTubeId = "WARlofTXwzc";
    private const string SpotifyId = "3RRicdUGPZjGvpMd5bjvEG";

    private static readonly DateTime YouTubeRelease = new(2026, 11, 15, 15, 0, 16, DateTimeKind.Utc);
    private static readonly DateTime SpotifyRelease = new(2026, 11, 15, 15, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan YouTubeLength = TimeSpan.FromSeconds(5621);
    private static readonly TimeSpan SpotifyLength = TimeSpan.FromSeconds(5620);

    private readonly AutoMocker _mocker = new();

    private IEpisodeHelper Sut => _mocker.CreateInstance<EpisodeHelper>();

    [Fact(DisplayName =
        "#1445: a Spotify submit matches the stored YouTube episode when the titles were lightly edited, " +
        "the releases are 16 seconds apart and the durations agree.")]
    public void IsMatchingEpisode_SpotifySubmit_EditedTitleSameDaySameDuration_MatchesYouTubeEpisode()
    {
        var episode = YouTubeEpisode(YouTubeTitle, YouTubeRelease, YouTubeLength);
        var item = SpotifySubmit(SpotifyTitle, SpotifyRelease, SpotifyLength);

        Sut.IsMatchingEpisode(episode, item).Should().BeTrue();
    }

    [Fact(DisplayName =
        "#1445: the edited titles alone stay below the title-only bar, so the cross-platform evidence is what matches.")]
    public void EditedTitles_ScoreBelowTitleOnlyBar_AndAboveCrossPlatformBar()
    {
        var score = FuzzyMatcher.Score(SpotifyTitle, YouTubeTitle);

        score.Should().BeLessThan(95);
        score.Should().BeGreaterThanOrEqualTo(EpisodeHelper.MinCrossPlatformFuzzyTitleMatch);
    }

    [Fact(DisplayName =
        "#1445 negative: the same edited titles on the same day do not match when the durations differ by more than the cross-platform band.")]
    public void IsMatchingEpisode_SpotifySubmit_EditedTitleSameDayDifferentDuration_DoesNotMatch()
    {
        var episode = YouTubeEpisode(YouTubeTitle, YouTubeRelease, YouTubeLength);
        var item = SpotifySubmit(SpotifyTitle, SpotifyRelease, YouTubeLength - TimeSpan.FromMinutes(20));

        Sut.IsMatchingEpisode(episode, item).Should().BeFalse();
    }

    [Fact(DisplayName =
        "#1445 negative: the same edited titles and duration do not match when the Spotify release is days away from the YouTube publish.")]
    public void IsMatchingEpisode_SpotifySubmit_EditedTitleSameDurationDifferentDay_DoesNotMatch()
    {
        var episode = YouTubeEpisode(YouTubeTitle, YouTubeRelease, YouTubeLength);
        var item = SpotifySubmit(SpotifyTitle, SpotifyRelease.AddDays(3), SpotifyLength);

        Sut.IsMatchingEpisode(episode, item).Should().BeFalse();
    }

    [Fact(DisplayName =
        "#1445 negative: an unrelated title on the same day with the same duration does not match.")]
    public void IsMatchingEpisode_SpotifySubmit_UnrelatedTitleSameDaySameDuration_DoesNotMatch()
    {
        var episode = YouTubeEpisode(
            "Is Pope Leo about to change everything? (pt. one)",
            YouTubeRelease,
            YouTubeLength);
        var item = SpotifySubmit(SpotifyTitle, SpotifyRelease, SpotifyLength);

        Sut.IsMatchingEpisode(episode, item).Should().BeFalse();
    }

    [Fact(DisplayName =
        "#1445 negative: the stored episode is not a YouTube↔audio pair when it already has a different Spotify id.")]
    public void IsMatchingEpisode_SpotifySubmit_StoredEpisodeHasOtherSpotifyId_DoesNotMatch()
    {
        var episode = YouTubeEpisode(YouTubeTitle, YouTubeRelease, YouTubeLength);
        episode.Ids = new EpisodeIds { YouTube = YouTubeId, Spotify = "45GdmMzJlqkMBijXHu0FcA" };
        var item = SpotifySubmit(SpotifyTitle, SpotifyRelease, SpotifyLength);

        Sut.IsMatchingEpisode(episode, item).Should().BeFalse();
    }

    [Fact(DisplayName =
        "#1445: a YouTube submit matches the stored Spotify episode with the edited title on the same day and duration.")]
    public void IsMatchingEpisode_YouTubeSubmit_EditedTitleSameDaySameDuration_MatchesSpotifyEpisode()
    {
        var episode = new Episode
        {
            Id = Guid.NewGuid(),
            Title = SpotifyTitle,
            ReleaseUtc = SpotifyRelease,
            Length = SpotifyLength,
            Ids = new EpisodeIds { Spotify = SpotifyId },
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.Spotify] = new() { Url = new Uri($"https://open.spotify.com/episode/{SpotifyId}") }
            }
        };
        var youTube = new CategorisedYouTubeItem(
            "channel", YouTubeId, "The Deep Dive Project", string.Empty, string.Empty, YouTubeTitle,
            string.Empty, YouTubeRelease, YouTubeLength,
            new Uri($"https://www.youtube.com/watch?v={YouTubeId}"), false, null, null);
        var item = new CategorisedItem(null, [episode], null, null, null, youTube, null, Service.YouTube);

        Sut.IsMatchingEpisode(episode, item).Should().BeTrue();
    }

    [Fact(DisplayName =
        "#1445: a secondary Apple id already on a different episode is not accepted when that episode's title, " +
        "release and duration do not look like the submitted Spotify episode.")]
    public void IsMatchingEpisode_SpotifySubmit_SecondaryAppleIdOnUnrelatedEpisode_DoesNotMatch()
    {
        const long appleId = 1000700000001;
        var wrongEpisode = new Episode
        {
            Id = Guid.NewGuid(),
            Title = "Is Pope Leo about to change everything? (pt. one)",
            ReleaseUtc = new DateTime(2026, 11, 14, 1, 47, 25, DateTimeKind.Utc),
            Length = SpotifyLength + TimeSpan.FromMinutes(13) + TimeSpan.FromSeconds(44),
            Ids = new EpisodeIds { YouTube = "otherVideo1", Spotify = "45GdmMzJlqkMBijXHu0FcA", Apple = appleId }
        };
        var item = SpotifySubmit(SpotifyTitle, SpotifyRelease, SpotifyLength) with
        {
            ResolvedAppleItem = AppleItem(appleId, SpotifyTitle)
        };

        Sut.IsMatchingEpisode(wrongEpisode, item).Should().BeFalse();
    }

    [Fact(DisplayName =
        "#1445: a secondary Apple id on the episode that matches the submitted Spotify episode is still accepted.")]
    public void IsMatchingEpisode_SpotifySubmit_SecondaryAppleIdOnMatchingEpisode_Matches()
    {
        const long appleId = 1000700000002;
        var episode = YouTubeEpisode(YouTubeTitle, YouTubeRelease, YouTubeLength);
        episode.Ids = new EpisodeIds { YouTube = YouTubeId, Apple = appleId };
        var item = SpotifySubmit(SpotifyTitle, SpotifyRelease, SpotifyLength) with
        {
            ResolvedAppleItem = AppleItem(appleId, SpotifyTitle)
        };

        Sut.IsMatchingEpisode(episode, item).Should().BeTrue();
    }

    private static Episode YouTubeEpisode(string title, DateTime release, TimeSpan length) =>
        new()
        {
            Id = Guid.Parse("dc7488e3-9428-4e2d-83d8-bb5b11e0230a"),
            Title = title,
            ReleaseUtc = release,
            Length = length,
            Ids = new EpisodeIds { YouTube = YouTubeId },
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = new Uri($"https://www.youtube.com/watch?v={YouTubeId}") }
            }
        };

    private static CategorisedItem SpotifySubmit(string title, DateTime release, TimeSpan duration)
    {
        var spotify = new CategorisedSpotifyItem(
            "show", SpotifyId, "The Deep Dive Project", string.Empty, string.Empty, title, string.Empty,
            release, duration, new Uri($"https://open.spotify.com/episode/{SpotifyId}"), false, null);
        return new CategorisedItem(null, [], null, spotify, null, null, null, Service.Spotify);
    }

    private static CategorisedAppleItem AppleItem(long appleId, string title) =>
        new(1, appleId, "The Deep Dive Project", string.Empty, string.Empty, title, string.Empty,
            SpotifyRelease, SpotifyLength, new Uri($"https://podcasts.apple.com/podcast/id1?i={appleId}"), false,
            null);
}
