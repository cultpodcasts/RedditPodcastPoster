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
/// duration and release (1d219589), those pairs were indexed as two episodes. Real shapes from
/// 10 Nov 2026 (same title, YouTube 6:13 shorter) and 17 Nov 2026 (divergent marketing title,
/// YouTube 5:22 shorter, plus a 1:18 same-day promo).
/// </summary>
public class YouTubeAudioDurationBandMatchingRules
{
    // 10 Nov 2026 audio vs YouTube of the same episode.
    private static readonly TimeSpan SameTitleAudioLength = new(1, 8, 44);
    private static readonly TimeSpan SameTitleYouTubeLength = new(1, 2, 31);
    private static readonly DateTime SameTitleAudioRelease = new(2026, 11, 10, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SameTitleYouTubeRelease = new(2026, 11, 10, 16, 45, 7, DateTimeKind.Utc);
    private const string SameTitle = "The Blue Wave and the Rise of the Christian Left";

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
        "A YouTube cut about six minutes shorter than same-title audio on the same calendar day " +
        "is a certain match, because title and release already agree and a 10% YouTube↔audio band " +
        "covers an ad-stripped YouTube file without lifting the five-minute cap for title-free pairs.")]
    public void Same_title_same_day_youtube_cut_just_over_five_minutes_is_certain()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = YouTube(podcast, SameTitle, SameTitleYouTubeLength, SameTitleYouTubeRelease);
        var incoming = Audio(podcast, SameTitle, SameTitleAudioLength, SameTitleAudioRelease);

        // Act
        var kind = _matcher.Classify(stored, incoming, episodeMatchRegex: null, podcast, [stored]);

        // Assert
        kind.Should().Be(EpisodeMatchKind.Certain);
    }

    [Fact(DisplayName =
        "A YouTube-only stored episode merges a same-title same-day audio catalogue item whose length " +
        "is about six minutes longer, because the indexer must attach the audio ids instead of creating " +
        "a second episode.")]
    public void Same_title_same_day_youtube_cut_merges_incoming_audio()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = YouTube(podcast, SameTitle, SameTitleYouTubeLength, SameTitleYouTubeRelease);
        var (discovered, appleId) = Apple(
            SameTitle,
            SameTitleAudioLength,
            SameTitleAudioRelease);
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
        "A YouTube marketing title that does not fuzzy-match the same-day audio title, with a cut " +
        "just over five minutes, is not an automatic match, because widening the title-free band " +
        "would also attach a same-day promo or a second long episode; a curator merge is required.")]
    public void Divergent_titles_just_over_five_minutes_do_not_match()
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
        "is far outside both the five-minute title-free band and the title-supported 10% band.")]
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
        "because the 10% title-supported band must not override the release-strategy gate that stops " +
        "recycled titles attaching to the wrong week.")]
    public void Same_title_youtube_cut_with_misaligned_release_is_none()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = YouTube(podcast, SameTitle, SameTitleYouTubeLength, SameTitleYouTubeRelease);
        var incoming = Audio(podcast, SameTitle, SameTitleAudioLength, SameTitleAudioRelease.AddDays(-30));

        // Act
        var kind = _matcher.Classify(stored, incoming, episodeMatchRegex: null, podcast, [stored]);

        // Assert
        kind.Should().Be(EpisodeMatchKind.None);
    }

    [Fact(DisplayName =
        "Two same-day YouTube↔audio episodes with wholly different titles and a six-minute length " +
        "gap are not a match, because the 10% band is only used when the titles already support the " +
        "pair, so the five-minute title-free cap still protects two long episodes on the same day.")]
    public void Divergent_titles_six_minute_gap_do_not_use_the_wider_band()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var stored = YouTube(
            podcast,
            "First long episode recorded that morning",
            TimeSpan.FromMinutes(62),
            SameTitleYouTubeRelease);
        var incoming = Audio(
            podcast,
            "A completely different evening interview",
            TimeSpan.FromMinutes(68),
            SameTitleAudioRelease);

        // Act
        var kind = _matcher.Classify(stored, incoming, episodeMatchRegex: null, podcast, [stored]);

        // Assert
        kind.Should().Be(EpisodeMatchKind.None);
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
}
