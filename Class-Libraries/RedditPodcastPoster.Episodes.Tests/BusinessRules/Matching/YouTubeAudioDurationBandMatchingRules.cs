using FluentAssertions;
using RedditPodcastPoster.Episodes.Matching;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Text.Matchers;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// #1470: YouTube often omits mid-roll ads, so a same-day YouTube↔audio pair can be six or seven
/// minutes apart — just outside the five-minute title-free band. After exact title was gated on
/// duration and release (1d219589), those pairs were indexed as two episodes. Regression data is
/// the six Leaving Eden weeks from 13 Oct to 17 Nov 2026. 13 Oct–10 Nov have the same title on
/// both sides and match under the title-supported 12% band. 17 Nov has a divergent marketing
/// title and stays unmatched for the curator merge (#1471).
/// </summary>
public class YouTubeAudioDurationBandMatchingRules
{
    // Real Leaving Eden lengths, titles and UTC release times (audio from the Acast/Apple
    // feed; YouTube from the channel listing and Atom publish times).
    private static readonly Week Oct13 = new(
        "13 Oct 2026",
        "How did Sadie's Family Become Fundie? That and other questions! (October Q&A Part 2)",
        new TimeSpan(1, 15, 46),
        new TimeSpan(1, 9, 3),
        new DateTime(2026, 10, 13, 9, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 10, 13, 15, 30, 6, DateTimeKind.Utc));

    private static readonly Week Oct20 = new(
        "20 Oct 2026",
        "Karissa Collins AI Slop Music: Why Do Fundies Love AI?",
        new TimeSpan(0, 57, 41),
        new TimeSpan(0, 52, 17),
        new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 10, 20, 15, 30, 39, DateTimeKind.Utc));

    private static readonly Week Oct27 = new(
        "27 Oct 2026",
        "J. P. Miller's WEIRD Theology (With Nikki Park)",
        new TimeSpan(1, 9, 39),
        new TimeSpan(1, 3, 22),
        new DateTime(2026, 10, 27, 9, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 10, 27, 16, 0, 9, DateTimeKind.Utc));

    private static readonly Week Nov3 = new(
        "3 Nov 2026",
        "Domestic Abuse Expert Reacts To \"You Can See Everything\"",
        new TimeSpan(1, 19, 6),
        new TimeSpan(1, 12, 8),
        new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 11, 3, 15, 30, 6, DateTimeKind.Utc));

    private static readonly Week Nov10 = new(
        "10 Nov 2026",
        "The Blue Wave and the Rise of the Christian Left",
        new TimeSpan(1, 8, 44),
        new TimeSpan(1, 2, 31),
        new DateTime(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 11, 10, 16, 45, 7, DateTimeKind.Utc));

    // 17 Nov 2026: audio title, YouTube marketing title, and a short same-day promo.
    private static readonly TimeSpan DivergentAudioLength = new(0, 58, 0);
    private static readonly TimeSpan DivergentYouTubeLength = new(0, 52, 38);
    private static readonly DateTime DivergentAudioRelease = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DivergentYouTubeRelease = new(2026, 11, 17, 16, 45, 5, DateTimeKind.Utc);
    private const string AudioTitle = "Is there a Deconstruction to Left-Wing Authoritarianism Pipeline?";
    private const string YouTubeMarketingTitle = "New study shows Ex-believers are at risk of left-wing authoritarianism!";
    private const string PromoTitle = "Introducing the Jordan Harbinger Show";
    private static readonly TimeSpan PromoLength = new(0, 1, 18);
    private static readonly DateTime PromoRelease = new(2026, 11, 17, 9, 0, 0, DateTimeKind.Utc);

    private readonly DomainTestFixture _fixture = new();
    private readonly IEpisodePlatformMatcher _matcher = EpisodeDomainTestServices.CreatePlatformMatcher();
    private readonly EpisodeMerger _merger = EpisodeDomainTestServices.CreateMerger();

    [Fact(DisplayName =
        "The 13 Oct 2026 Leaving Eden YouTube cut, 6:43 shorter than same-title same-day audio, " +
        "is a certain match, because titles already agree and the title-supported 12% band covers " +
        "an ad-stripped YouTube file.")]
    public void Leaving_Eden_13_Oct_same_title_is_certain() => AssertSameTitleWeekCertain(Oct13);

    [Fact(DisplayName =
        "The 20 Oct 2026 Leaving Eden YouTube cut, 5:24 shorter than same-title same-day audio, " +
        "is a certain match, because titles already agree and the title-supported 12% band covers " +
        "the cut that only just cleared a 10% band.")]
    public void Leaving_Eden_20_Oct_same_title_is_certain() => AssertSameTitleWeekCertain(Oct20);

    [Fact(DisplayName =
        "The 27 Oct 2026 Leaving Eden YouTube cut, 6:17 shorter than same-title same-day audio, " +
        "is a certain match, because titles already agree and the title-supported 12% band covers " +
        "an ad-stripped YouTube file.")]
    public void Leaving_Eden_27_Oct_same_title_is_certain() => AssertSameTitleWeekCertain(Oct27);

    [Fact(DisplayName =
        "The 3 Nov 2026 Leaving Eden YouTube cut, 6:58 shorter than same-title same-day audio, " +
        "is a certain match, because titles already agree and the title-supported 12% band covers " +
        "an ad-stripped YouTube file.")]
    public void Leaving_Eden_3_Nov_same_title_is_certain() => AssertSameTitleWeekCertain(Nov3);

    [Fact(DisplayName =
        "The 10 Nov 2026 Leaving Eden YouTube cut, 6:13 shorter than same-title same-day audio, " +
        "is a certain match, because titles already agree and the title-supported 12% band covers " +
        "an ad-stripped YouTube file without lifting the five-minute cap for title-free pairs.")]
    public void Leaving_Eden_10_Nov_same_title_is_certain() => AssertSameTitleWeekCertain(Nov10);

    [Fact(DisplayName =
        "A YouTube-only stored episode merges a same-title same-day audio catalogue item whose length " +
        "is about six minutes longer, because the indexer must attach the audio ids instead of creating " +
        "a second episode.")]
    public void Same_title_same_day_youtube_cut_merges_incoming_audio()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = YouTube(podcast, Nov10.Title, Nov10.YouTubeLength, Nov10.YouTubeRelease);
        var (discovered, appleId) = Apple(Nov10.Title, Nov10.AudioLength, Nov10.AudioRelease);
        var expected = EpisodeExpectation.From(stored)
            .WithApple(appleId, _fixture.DefaultAppleUrl(appleId));

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(stored.Id);
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "The 17 Nov 2026 Leaving Eden YouTube marketing title that does not fuzzy-match the " +
        "same-day audio title, with a cut just over five minutes, is not an automatic match, " +
        "because widening the title-free band would also attach a same-day promo or a second " +
        "long episode; a curator merge is required (#1471).")]
    public void Leaving_Eden_17_Nov_divergent_titles_do_not_match()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = YouTube(podcast, YouTubeMarketingTitle, DivergentYouTubeLength, DivergentYouTubeRelease);
        var incoming = Audio(podcast, AudioTitle, DivergentAudioLength, DivergentAudioRelease);

        // Act
        var kind = _matcher.Classify(stored, incoming, episodeMatchRegex: null, podcast, [stored]);
        var titleScore = FuzzyMatcher.Score(YouTubeMarketingTitle, AudioTitle);

        // Assert
        titleScore.Should().BeLessThan(70);
        kind.Should().Be(EpisodeMatchKind.None);
    }

    [Fact(DisplayName =
        "A one-minute same-day promo does not match a fifty-minute YouTube cut, because length " +
        "is far outside both the five-minute title-free band and the title-supported 12% band.")]
    public void Same_day_short_promo_does_not_match_long_youtube_cut()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = YouTube(podcast, YouTubeMarketingTitle, DivergentYouTubeLength, DivergentYouTubeRelease);
        var incoming = Audio(podcast, PromoTitle, PromoLength, PromoRelease);

        // Act
        var kind = _matcher.Classify(stored, incoming, episodeMatchRegex: null, podcast, [stored]);

        // Assert
        kind.Should().Be(EpisodeMatchKind.None);
    }

    [Fact(DisplayName =
        "An exact title with a six-minute YouTube cut but a release thirty days apart is not a match, " +
        "because the 12% title-supported band must not override the release-strategy gate that stops " +
        "recycled titles attaching to the wrong week.")]
    public void Same_title_youtube_cut_with_misaligned_release_is_none()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = YouTube(podcast, Nov10.Title, Nov10.YouTubeLength, Nov10.YouTubeRelease);
        var incoming = Audio(podcast, Nov10.Title, Nov10.AudioLength, Nov10.AudioRelease.AddDays(-30));

        // Act
        var kind = _matcher.Classify(stored, incoming, episodeMatchRegex: null, podcast, [stored]);

        // Assert
        kind.Should().Be(EpisodeMatchKind.None);
    }

    [Fact(DisplayName =
        "Two same-day YouTube↔audio episodes with wholly different titles and a six-minute length " +
        "gap are not a match, because the 12% band is only used when the titles already support the " +
        "pair, so the five-minute title-free cap still protects two long episodes on the same day.")]
    public void Divergent_titles_six_minute_gap_do_not_use_the_wider_band()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = YouTube(
            podcast,
            "First long episode recorded that morning",
            TimeSpan.FromMinutes(62),
            Nov10.YouTubeRelease);
        var incoming = Audio(
            podcast,
            "A completely different evening interview",
            TimeSpan.FromMinutes(68),
            Nov10.AudioRelease);

        // Act
        var kind = _matcher.Classify(stored, incoming, episodeMatchRegex: null, podcast, [stored]);

        // Assert
        kind.Should().Be(EpisodeMatchKind.None);
    }

    private void AssertSameTitleWeekCertain(Week week)
    {
        var podcast = _fixture.CreatePodcast();
        var stored = YouTube(podcast, week.Title, week.YouTubeLength, week.YouTubeRelease);
        var incoming = Audio(podcast, week.Title, week.AudioLength, week.AudioRelease);

        var kind = _matcher.Classify(stored, incoming, episodeMatchRegex: null, podcast, [stored]);
        var delta = week.AudioLength - week.YouTubeLength;
        var titleSupported = EpisodeDurationTolerance.TitleSupportedYouTubeAudio(
            week.AudioLength,
            week.YouTubeLength);

        delta.Should().BeGreaterThan(EpisodeDurationTolerance.CrossPlatform, week.Label);
        delta.Should().BeLessThan(titleSupported, week.Label);
        kind.Should().Be(EpisodeMatchKind.Certain, week.Label);
    }

    private Episode YouTube(Podcast podcast, string title, TimeSpan length, DateTime release) =>
        _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Title = title;
            e.Length = length;
            e.ReleaseUtc = release;
            EpisodeServicePresence.SetYouTubeIdentity(e, _fixture.CreateYouTubeId());
        });

    private Episode Audio(Podcast podcast, string title, TimeSpan length, DateTime release) =>
        _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Title = title;
            e.Length = length;
            e.ReleaseUtc = release;
            EpisodeServicePresence.SetSpotifyIdentity(e, _fixture.CreateSpotifyId());
            EpisodeServicePresence.SetAppleIdentity(e, _fixture.CreateAppleId());
        });

    private (Episode Episode, long AppleId) Apple(string title, TimeSpan length, DateTime release)
    {
        var episode = _fixture.CreateAppleCatalogueEpisode(b => b
            .WithTitle(title)
            .WithDuration(length)
            .WithRelease(release));
        return (episode, EpisodeServicePresence.AppleEpisodeId(episode)!.Value);
    }

    private sealed record Week(
        string Label,
        string Title,
        TimeSpan AudioLength,
        TimeSpan YouTubeLength,
        DateTime AudioRelease,
        DateTime YouTubeRelease);
}
