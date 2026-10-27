using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// YouTube-authority shows with a fixed publishing delay: the probe's release is the YouTube publish shifted back
/// by the delay, which can land on the previous calendar day (Oct 2026: video at 04:00 UTC − 7h = 21:00 the day
/// before, audio at 09:00–13:00 UTC on the video's day). Audio that shares a calendar day with the unshifted
/// YouTube publish, and that the delay-adjusted one-day window already admits, counts as same-day.
/// </summary>
public class CatalogueMatchScorerYouTubePublishDayRules
{
    private static readonly TimeSpan PublishingDelay = TimeSpan.FromHours(7);
    private const string YouTubeTitle = "Can A Stranger Decide Who Raises Your Kids? Inside The Hidden Hearing Room";
    private const string DisjointAudioTitle =
        "The Trouble With Custody Tribunals With A Reporter Turned Advocate: On Patronage And Unchecked Benches";
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When the publishing delay pushes the expected audio release onto the previous day but the Apple episode shares " +
        "a calendar day with the unshifted YouTube publish and lengths are near-exact, the score meets the threshold " +
        "even with disjoint marketing titles, because audio and video went out the same day.")]
    public void Apple_same_day_as_raw_youtube_publish_with_near_exact_duration_meets_threshold()
    {
        // Arrange
        var youTubePublished = DomainTestFixture.UtcAtTime(-2, new TimeSpan(4, 0, 9));
        var youTubeLength = new TimeSpan(1, 0, 51);
        var probe = CreateProbe(youTubePublished - PublishingDelay, youTubeLength);
        var apple = CreateApple(
            DomainTestFixture.UtcAtTime(-2, new TimeSpan(13, 16, 0)),
            youTubeLength + TimeSpan.FromSeconds(5));

        // Act
        var withoutRawPublish = CatalogueMatchScorer.Score(probe, apple);
        var withRawPublish = CatalogueMatchScorer.Score(probe, apple, youTubePublished: youTubePublished);

        // Assert
        DateOnly.FromDateTime(probe.ReleaseUtc).Should().NotBe(DateOnly.FromDateTime(apple.ReleaseUtc));
        withoutRawPublish.Should().Be(
            CatalogueMatchScorer.DurationWithinBandPoints +
            CatalogueMatchScorer.NearExactDurationPoints +
            CatalogueMatchScorer.WeakInWindowReleasePoints);
        withoutRawPublish.Should().BeLessThan(CatalogueMatchScorer.MatchThreshold);
        withRawPublish.Should().Be(CatalogueMatchScorer.MatchThreshold);
    }

    [Fact(DisplayName =
        "When the delay-shifted probe falls on the previous day and the date-only Spotify episode is dated the unshifted " +
        "YouTube publish day with a near-exact length, the score meets the threshold, because Spotify's day is the " +
        "video's day.")]
    public void Spotify_date_only_on_raw_youtube_publish_day_with_near_exact_duration_meets_threshold()
    {
        // Arrange
        var youTubePublished = DomainTestFixture.UtcAtTime(-3, new TimeSpan(4, 0, 15));
        var youTubeLength = new TimeSpan(1, 2, 14);
        var probe = CreateProbe(youTubePublished - PublishingDelay, youTubeLength);
        var spotify = CreateSpotify(DomainTestFixture.UtcDateDaysAgo(3), youTubeLength + TimeSpan.FromSeconds(12));

        // Act
        var meets = CatalogueMatchScorer.MeetsMatchThreshold(probe, spotify, youTubePublished: youTubePublished);

        // Assert
        meets.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When a YouTube-only video is published the day after the audio episode with a near-exact length, the raw " +
        "publish day does not upgrade the release signal and the score stays below the threshold, because a " +
        "next-day video must not take the previous day's audio.")]
    public void Next_day_youtube_only_video_with_near_exact_duration_stays_below_threshold()
    {
        // Arrange
        var youTubePublished = DomainTestFixture.UtcAtTime(-4, new TimeSpan(16, 1, 18));
        var youTubeLength = new TimeSpan(1, 1, 11);
        var probe = CreateProbe(youTubePublished - PublishingDelay, youTubeLength);
        var apple = CreateApple(
            DomainTestFixture.UtcAtTime(-5, new TimeSpan(9, 0, 0)),
            youTubeLength - TimeSpan.FromSeconds(14));

        // Act
        var score = CatalogueMatchScorer.Score(probe, apple, youTubePublished: youTubePublished);

        // Assert
        score.Should().Be(
            CatalogueMatchScorer.DurationWithinBandPoints +
            CatalogueMatchScorer.NearExactDurationPoints +
            CatalogueMatchScorer.WeakInWindowReleasePoints);
        score.Should().BeLessThan(CatalogueMatchScorer.MatchThreshold);
    }

    [Fact(DisplayName =
        "When audio shares a calendar day with the raw YouTube publish but lengths are only in band (about two minutes " +
        "apart), the score stays below the threshold, because same-day release plus merely in-band duration is not enough.")]
    public void Same_day_as_raw_youtube_publish_with_in_band_not_near_exact_duration_stays_below_threshold()
    {
        // Arrange
        var youTubePublished = DomainTestFixture.UtcAtTime(-2, new TimeSpan(4, 0, 9));
        var youTubeLength = new TimeSpan(1, 0, 51);
        var probe = CreateProbe(youTubePublished - PublishingDelay, youTubeLength);
        var apple = CreateApple(
            DomainTestFixture.UtcAtTime(-2, new TimeSpan(13, 16, 0)),
            youTubeLength + TimeSpan.FromMinutes(2));

        // Act
        var score = CatalogueMatchScorer.Score(probe, apple, youTubePublished: youTubePublished);

        // Assert
        score.Should().Be(
            CatalogueMatchScorer.DurationWithinBandPoints +
            CatalogueMatchScorer.SameCalendarDayReleasePoints);
        score.Should().BeLessThan(CatalogueMatchScorer.MatchThreshold);
    }

    [Fact(DisplayName =
        "When the publishing delay is a week and audio shares a calendar day with the raw YouTube publish, the row is " +
        "outside the delay-adjusted window and scores zero, because the raw publish day only upgrades rows the " +
        "delay-adjusted window already admits.")]
    public void Long_delay_same_day_as_raw_youtube_publish_outside_adjusted_window_scores_zero()
    {
        // Arrange
        var weekDelay = TimeSpan.FromDays(7);
        var youTubePublished = DomainTestFixture.UtcAtTime(-2, new TimeSpan(15, 0, 0));
        var youTubeLength = new TimeSpan(1, 0, 51);
        var probe = CreateProbe(youTubePublished - weekDelay, youTubeLength);
        var sameDayAsVideo = CreateApple(DomainTestFixture.UtcAtTime(-2, new TimeSpan(9, 0, 0)), youTubeLength);

        // Act
        var score = CatalogueMatchScorer.Score(probe, sameDayAsVideo, youTubePublished: youTubePublished);

        // Assert
        score.Should().Be(0);
    }

    [Fact(DisplayName =
        "When selecting among a fortnight of audio episodes of near-identical length for a delay-shifted probe, " +
        "SelectBestMatch picks the one on the raw YouTube publish day, because neighbouring episodes are outside " +
        "the one-day window.")]
    public void Select_best_match_picks_audio_on_raw_youtube_publish_day()
    {
        // Arrange
        var youTubePublished = DomainTestFixture.UtcAtTime(-9, new TimeSpan(4, 0, 9));
        var youTubeLength = new TimeSpan(1, 0, 51);
        var probe = CreateProbe(youTubePublished - PublishingDelay, youTubeLength);
        var previousWeek = CreateApple(DomainTestFixture.UtcAtTime(-16, new TimeSpan(9, 0, 0)), youTubeLength);
        var sameDay = CreateApple(
            DomainTestFixture.UtcAtTime(-9, new TimeSpan(13, 16, 0)),
            youTubeLength + TimeSpan.FromSeconds(5));
        var nextWeek = CreateApple(DomainTestFixture.UtcAtTime(-2, new TimeSpan(9, 0, 0)), youTubeLength);

        // Act
        var best = CatalogueMatchScorer.SelectBestMatch(
            probe,
            [previousWeek, sameDay, nextWeek],
            youTubePublished: youTubePublished);

        // Assert
        best.Should().BeSameAs(sameDay);
    }

    [Fact(DisplayName =
        "When a next-day YouTube-only video is scored against the previous day's audio episode of near-identical " +
        "length, SelectBestMatch returns no match, because that audio belongs to the previous day's full episode.")]
    public void Select_best_match_returns_null_for_next_day_youtube_only_video()
    {
        // Arrange
        var youTubePublished = DomainTestFixture.UtcAtTime(-8, new TimeSpan(19, 1, 32));
        var youTubeLength = new TimeSpan(1, 1, 11);
        var probe = CreateProbe(youTubePublished - PublishingDelay, youTubeLength);
        var previousDayAudio = CreateApple(
            DomainTestFixture.UtcAtTime(-9, new TimeSpan(13, 16, 0)),
            youTubeLength - TimeSpan.FromSeconds(14));

        // Act
        var best = CatalogueMatchScorer.SelectBestMatch(probe, [previousDayAudio], youTubePublished: youTubePublished);

        // Assert
        best.Should().BeNull();
    }

    private Episode CreateProbe(DateTime delayAdjustedRelease, TimeSpan length) =>
        _fixture.CreateEpisode(e =>
        {
            e.Title = YouTubeTitle;
            e.Description = string.Empty;
            e.Length = length;
            e.ReleaseUtc = delayAdjustedRelease;
            e.YouTubeId = _fixture.CreateYouTubeId();
            e.Subjects = [];
        });

    private Episode CreateApple(DateTime release, TimeSpan length) =>
        _fixture.CreateEpisode(e =>
        {
            e.Title = DisjointAudioTitle;
            e.Description = string.Empty;
            e.Length = length;
            e.ReleaseUtc = release;
            e.AppleId = _fixture.CreateAppleId();
            e.Subjects = [];
        });

    private Episode CreateSpotify(DateTime release, TimeSpan length) =>
        _fixture.CreateEpisode(e =>
        {
            e.Title = DisjointAudioTitle;
            e.Description = string.Empty;
            e.Length = length;
            e.ReleaseUtc = release;
            e.SpotifyId = _fixture.CreateSpotifyId();
            e.Subjects = [];
        });
}
