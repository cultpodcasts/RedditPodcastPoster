using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// YouTube-authority shows with a fixed publishing delay: the probe's release is the YouTube publish shifted back
/// by the delay, which can land on the previous calendar day (Oct 2026: video at 04:00 UTC − 7h = 21:00 the day
/// before, audio at 09:00–13:00 UTC on the video's day). Audio that shares a calendar day with the unshifted
/// YouTube publish, and that the delay-adjusted one-day window already admits, counts as same-day — but only with
/// near-exact lengths and a positive delay that crossed midnight backwards. Negative cases sit in the risky
/// 00:00–07:00 UTC window on the audio's day, where the raw publish and the shifted probe fall on different days.
/// </summary>
public class CatalogueMatchScorerYouTubePublishDayRules
{
    private static readonly TimeSpan PublishingDelay = TimeSpan.FromHours(7);
    // Deliberately disjoint (no shared words) so title scores 0; fixture titles are random and not guaranteed disjoint.
    private const string YouTubeTitle = "Why Do Orchards Glow At Dusk? Inside A Moth Migration";
    private const string DisjointAudioTitle =
        "Canal Engineers Of Seventeenth Century Flanders With A Retired Surveyor: Locks, Tolls And Silt";
    private const string ClipTitle = "Lighthouse Keepers Of The Northern Coast Remember The Storm #shorts";
    private const string FullEpisodeTitle =
        "The Lighthouse Keepers Of The Northern Coast Remember: An Oral History Of The Great Storm";
    private const string SharedSubject = "Maritime History";
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
        "apart), the raw publish day earns no same-day credit and the score stays at in-band plus the weak window, " +
        "because the raw-day credit needs near-exact lengths.")]
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
            CatalogueMatchScorer.WeakInWindowReleasePoints);
        score.Should().BeLessThan(CatalogueMatchScorer.MatchThreshold);
    }

    [Fact(DisplayName =
        "When a 58-second YouTube-only clip with a similar title and one shared subject is published at 01:30 UTC on " +
        "the audio's day, the score stays below the threshold, because a clip has no length evidence and the raw " +
        "publish day must not credit it with same-day release.")]
    public void Short_clip_early_on_audio_day_with_similar_title_and_shared_subject_stays_below_threshold()
    {
        // Arrange
        var youTubePublished = DomainTestFixture.UtcAtTime(-2, new TimeSpan(1, 30, 0));
        var probe = CreateProbe(youTubePublished - PublishingDelay, TimeSpan.FromSeconds(58));
        probe.Title = ClipTitle;
        probe.Subjects = [SharedSubject];
        var apple = CreateApple(DomainTestFixture.UtcAtTime(-2, new TimeSpan(13, 0, 0)), new TimeSpan(1, 2, 14));
        apple.Title = FullEpisodeTitle;
        apple.Subjects = [SharedSubject];

        // Act
        var score = CatalogueMatchScorer.Score(probe, apple, youTubePublished: youTubePublished);

        // Assert
        DateOnly.FromDateTime(probe.ReleaseUtc).Should().NotBe(DateOnly.FromDateTime(youTubePublished));
        score.Should().Be(
            CatalogueMatchScorer.WeakInWindowReleasePoints +
            CatalogueMatchScorer.FuzzyTitlePoints +
            CatalogueMatchScorer.SingleSharedSubjectPoints);
        score.Should().BeLessThan(CatalogueMatchScorer.MatchThreshold);
    }

    [Fact(DisplayName =
        "When a YouTube-only clip whose title is contained in the audio title and that shares one subject is published " +
        "at 03:00 UTC on the audio's day, the score stays below the threshold, because substring title plus a subject " +
        "plus a raw-day release must not match without length evidence.")]
    public void Short_clip_early_on_audio_day_with_substring_title_and_shared_subject_stays_below_threshold()
    {
        // Arrange
        var youTubePublished = DomainTestFixture.UtcAtTime(-2, new TimeSpan(3, 0, 0));
        var probe = CreateProbe(youTubePublished - PublishingDelay, TimeSpan.FromSeconds(58));
        probe.Title = "Lighthouse Keepers Of The Northern Coast";
        probe.Subjects = [SharedSubject];
        var apple = CreateApple(DomainTestFixture.UtcAtTime(-2, new TimeSpan(13, 0, 0)), new TimeSpan(1, 2, 14));
        apple.Title = FullEpisodeTitle;
        apple.Subjects = [SharedSubject];

        // Act
        var score = CatalogueMatchScorer.Score(probe, apple, youTubePublished: youTubePublished);

        // Assert
        score.Should().Be(
            CatalogueMatchScorer.WeakInWindowReleasePoints +
            CatalogueMatchScorer.SubstringTitlePoints +
            CatalogueMatchScorer.SingleSharedSubjectPoints);
        score.Should().BeLessThan(CatalogueMatchScorer.MatchThreshold);
    }

    [Fact(DisplayName =
        "When a YouTube-only video published at 02:00 UTC on the audio's day is in band but not near-exact (three " +
        "minutes apart) with no title match, the score stays below the threshold, because the raw publish day needs " +
        "near-exact lengths.")]
    public void In_band_video_early_on_audio_day_without_title_match_stays_below_threshold()
    {
        // Arrange
        var youTubePublished = DomainTestFixture.UtcAtTime(-2, new TimeSpan(2, 0, 0));
        var youTubeLength = new TimeSpan(0, 59, 14);
        var probe = CreateProbe(youTubePublished - PublishingDelay, youTubeLength);
        var apple = CreateApple(
            DomainTestFixture.UtcAtTime(-2, new TimeSpan(13, 0, 0)),
            youTubeLength + TimeSpan.FromMinutes(3));

        // Act
        var score = CatalogueMatchScorer.Score(probe, apple, youTubePublished: youTubePublished);

        // Assert
        score.Should().Be(
            CatalogueMatchScorer.DurationWithinBandPoints +
            CatalogueMatchScorer.WeakInWindowReleasePoints);
        score.Should().BeLessThan(CatalogueMatchScorer.MatchThreshold);
    }

    [Fact(DisplayName =
        "When the publishing delay is negative (YouTube first, audio later) and the shifted probe crosses midnight " +
        "forwards, the previous episode's near-exact-length audio on the raw YouTube publish day earns no same-day " +
        "credit and stays below the threshold, because the raw-day credit only applies to positive delays.")]
    public void Negative_delay_previous_episode_audio_on_raw_publish_day_stays_below_threshold()
    {
        // Arrange
        var negativeDelay = TimeSpan.FromHours(-7);
        var youTubePublished = DomainTestFixture.UtcAtTime(-3, new TimeSpan(20, 0, 0));
        var youTubeLength = new TimeSpan(0, 45, 0);
        var probe = CreateProbe(youTubePublished - negativeDelay, youTubeLength);
        var previousEpisodeAudio = CreateApple(
            DomainTestFixture.UtcAtTime(-3, new TimeSpan(3, 0, 0)),
            youTubeLength + TimeSpan.FromSeconds(4));

        // Act
        var score = CatalogueMatchScorer.Score(probe, previousEpisodeAudio, youTubePublished: youTubePublished);

        // Assert
        DateOnly.FromDateTime(probe.ReleaseUtc).Should().BeAfter(DateOnly.FromDateTime(youTubePublished));
        score.Should().Be(
            CatalogueMatchScorer.DurationWithinBandPoints +
            CatalogueMatchScorer.NearExactDurationPoints +
            CatalogueMatchScorer.WeakInWindowReleasePoints);
        score.Should().BeLessThan(CatalogueMatchScorer.MatchThreshold);
    }

    [Fact(DisplayName =
        "When a daily show's previous-day audio (same day as the shifted probe) and the raw-publish-day audio both score " +
        "the threshold with equal length gaps, SelectBestMatch prefers the raw-publish-day audio regardless of order, " +
        "because closeness to the shifted probe usually points at the previous day's episode.")]
    public void Select_best_match_tie_prefers_raw_publish_day_audio_over_shifted_probe_day()
    {
        // Arrange
        var youTubePublished = DomainTestFixture.UtcAtTime(-5, new TimeSpan(4, 0, 9));
        var youTubeLength = new TimeSpan(1, 0, 51);
        var probe = CreateProbe(youTubePublished - PublishingDelay, youTubeLength);
        var previousDay = CreateApple(
            DomainTestFixture.UtcAtTime(-6, new TimeSpan(9, 0, 0)),
            youTubeLength + TimeSpan.FromSeconds(5));
        var rawPublishDay = CreateApple(
            DomainTestFixture.UtcAtTime(-5, new TimeSpan(13, 16, 0)),
            youTubeLength + TimeSpan.FromSeconds(5));

        // Act
        var previousFirst = CatalogueMatchScorer.SelectBestMatch(
            probe, [previousDay, rawPublishDay], youTubePublished: youTubePublished);
        var rawFirst = CatalogueMatchScorer.SelectBestMatch(
            probe, [rawPublishDay, previousDay], youTubePublished: youTubePublished);

        // Assert
        CatalogueMatchScorer.Score(probe, previousDay, youTubePublished: youTubePublished)
            .Should().Be(CatalogueMatchScorer.Score(probe, rawPublishDay, youTubePublished: youTubePublished));
        previousFirst.Should().BeSameAs(rawPublishDay);
        rawFirst.Should().BeSameAs(rawPublishDay);
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
