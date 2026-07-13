using FluentAssertions;
using FuzzySharp;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence;

namespace RedditPodcastPoster.Episodes.Tests.BusinessRules.Matching;

/// <summary>
/// Fuzzy-title stress coverage. Exercises each word-level edit strategy from
/// <see cref="DomainTestFixture.CreateFuzzyTitleVariant(string, FuzzyTitleVariantStrategy)"/>
/// against the production matcher, plus negative and duration-boundary cases so the
/// <see cref="EpisodePlatformMatcher"/> fuzzy path is not proven by happy-path examples alone.
/// <para>
/// Threshold contract: <c>EpisodePlatformMatcher.MinFuzzyTitleScore = 70</c> for FuzzySharp
/// WeightedRatio; standard duration tolerance 1 minute (strict &lt;) — cross-platform
/// YouTube release authority cross-platform duration tolerance: 5 minutes.
/// </para>
/// </summary>
public class FuzzyTitleMatchingRules
{
    private const int MinFuzzyTitleScore = 70;
    private static readonly TimeSpan StandardDurationTolerance = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CrossPlatformDurationTolerance = TimeSpan.FromMinutes(5);

    private readonly DomainTestFixture _fixture = new();
    private readonly EpisodeMatcher _matcher = EpisodeDomainTestServices.CreateMatcher();
    private readonly EpisodeMerger _merger = EpisodeDomainTestServices.CreateMerger();

    public static TheoryData<FuzzyTitleVariantStrategy> AllFuzzyVariantStrategies() =>
        new()
        {
            FuzzyTitleVariantStrategy.ReplaceWord,
            FuzzyTitleVariantStrategy.DropWord,
            FuzzyTitleVariantStrategy.AddFillerWord,
            FuzzyTitleVariantStrategy.SwapAdjacentWords
        };

    // --------------------------------------------------------------------------------------------
    // Matrix: 4 variant strategies × standard-podcast (short title).
    // Each strategy must produce a WeightedRatio ≥ threshold AND drive a merge with matching duration.
    // --------------------------------------------------------------------------------------------

    [Theory(DisplayName =
        "For a standard podcast with a short title and matching duration, " +
        "each fuzzy variant strategy (replace/drop/add-filler/swap) drives a merge.")]
    [MemberData(nameof(AllFuzzyVariantStrategies))]
    public void Fuzzy_variant_with_matching_duration_merges_on_standard_podcast(
        FuzzyTitleVariantStrategy strategy)
    {
        // Arrange
        var storedTitle = _fixture.CreateShortTitle();
        var incomingTitle = DomainTestFixture.CreateFuzzyTitleVariant(storedTitle, strategy);
        var storedLength = _fixture.CreateDuration();
        var incomingLength = storedLength + TimeSpan.FromSeconds(30);
        var release = DomainTestFixture.UtcAtTime(-3, _fixture.CreateNonMidnightTimeOfDay());
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.CreateStoredEpisodeWithSpotifyOnly(
            podcast, release, storedLength, storedTitle);
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithTitle(incomingTitle)
            .WithRelease(release)
            .WithDuration(incomingLength));
        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId)
            .WithTitle(incomingTitle)
            .WithRelease(release)
            .WithDuration(incomingLength));
        var expected = EpisodeExpectation.From(stored)
            .WithYouTube(youTubeInput.YouTubeId, youTubeInput.YouTubeUrl);

        AssertFuzzyScoreAboveThreshold(storedTitle, incomingTitle, strategy);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        result.MergedEpisodes.Single().Existing.Id.Should().Be(stored.Id);
        stored.ShouldMatchExpectation(expected);
    }

    // --------------------------------------------------------------------------------------------
    // Matrix: 4 variant strategies × standard-podcast (long title).
    // Long titles give the matcher more character overlap; all four strategies must still merge.
    // --------------------------------------------------------------------------------------------

    [Theory(DisplayName =
        "For a standard podcast with a long title and matching duration, " +
        "each fuzzy variant strategy (replace/drop/add-filler/swap) drives a merge.")]
    [MemberData(nameof(AllFuzzyVariantStrategies))]
    public void Fuzzy_variant_with_long_title_and_matching_duration_merges_on_standard_podcast(
        FuzzyTitleVariantStrategy strategy)
    {
        // Arrange
        var storedTitle = _fixture.CreateLongTitle();
        var incomingTitle = DomainTestFixture.CreateFuzzyTitleVariant(storedTitle, strategy);
        var storedLength = _fixture.CreateDuration();
        var incomingLength = storedLength + TimeSpan.FromSeconds(45);
        var release = DomainTestFixture.UtcAtTime(-4, _fixture.CreateNonMidnightTimeOfDay());
        var podcast = _fixture.CreatePodcast();
        var stored = _fixture.CreateStoredEpisodeWithSpotifyOnly(
            podcast, release, storedLength, storedTitle);
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithTitle(incomingTitle)
            .WithRelease(release)
            .WithDuration(incomingLength));
        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId)
            .WithTitle(incomingTitle)
            .WithRelease(release)
            .WithDuration(incomingLength));
        var expected = EpisodeExpectation.From(stored)
            .WithYouTube(youTubeInput.YouTubeId, youTubeInput.YouTubeUrl);

        AssertFuzzyScoreAboveThreshold(storedTitle, incomingTitle, strategy);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    // --------------------------------------------------------------------------------------------
    // Matrix: 4 variant strategies × YouTube release authority podcast with negative publishing delay.
    // Fuzzy title still contributes score on the release-and-duration path; these cases also pass
    // via MatchesByFuzzyTitleAndDuration when duration is within band.
    // --------------------------------------------------------------------------------------------

    [Theory(DisplayName =
        "For a YouTube release authority podcast with negative publishing delay, each fuzzy variant strategy " +
        "drives a cross-platform merge when release and duration also align.")]
    [MemberData(nameof(AllFuzzyVariantStrategies))]
    public void Fuzzy_variant_on_negative_delay_podcast_merges_cross_platform(
        FuzzyTitleVariantStrategy strategy)
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var storedTitle = _fixture.CreateShortTitle();
        var incomingTitle = DomainTestFixture.CreateFuzzyTitleVariant(storedTitle, strategy);
        var youTubeRelease = DomainTestFixture.UtcAtTime(-30, _fixture.CreateNonMidnightTimeOfDay());
        var spotifyRelease = DomainTestFixture.SpotifyCatalogueReleaseDaysAfterYouTube(youTubeRelease, 28);
        var storedLength = _fixture.CreateDuration();
        var incomingLength = storedLength + TimeSpan.FromMinutes(2);
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast, youTubeRelease, storedLength, storedTitle);
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput(b => b
            .WithTitle(incomingTitle)
            .WithRelease(spotifyRelease)
            .WithDuration(incomingLength));
        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(spotifyInput.SpotifyId)
            .WithTitle(incomingTitle)
            .WithSpotifyUrl(spotifyInput.SpotifyUrl)
            .WithRelease(spotifyRelease)
            .WithDuration(incomingLength));
        var expected = EpisodeExpectation.From(stored)
            .WithSpotify(spotifyInput.SpotifyId, spotifyInput.SpotifyUrl);

        AssertFuzzyScoreAboveThreshold(storedTitle, incomingTitle, strategy);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    // --------------------------------------------------------------------------------------------
    // Duration boundary: fuzzy match holds but duration is exactly at the standard 1-minute
    // tolerance — production uses strict `<` so this must NOT merge.
    // --------------------------------------------------------------------------------------------

    [Fact(DisplayName =
        "When the fuzzy title matches but duration differs by exactly the standard tolerance (1 minute), " +
        "episodes must not merge — the tolerance is strict less-than.")]
    public void Fuzzy_match_with_duration_exactly_at_standard_tolerance_does_not_merge()
    {
        // Arrange
        var storedTitle = _fixture.CreateShortTitle();
        var incomingTitle = DomainTestFixture.CreateFuzzyTitleVariant(
            storedTitle, FuzzyTitleVariantStrategy.SwapAdjacentWords);
        var storedLength = TimeSpan.FromMinutes(30);
        var incomingLength = storedLength + StandardDurationTolerance;
        var podcast = _fixture.CreatePodcast();
        var existing = CreateEpisode(storedTitle, storedLength);
        var incoming = CreateEpisode(incomingTitle, incomingLength);

        AssertFuzzyScoreAboveThreshold(storedTitle, incomingTitle, FuzzyTitleVariantStrategy.SwapAdjacentWords);

        // Act
        var isMatch = _matcher.IsMatch(existing, incoming, episodeMatchRegex: null, podcast);

        // Assert
        isMatch.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the fuzzy title matches and duration differs by less than the standard tolerance (59 seconds), " +
        "episodes may be treated as the same.")]
    public void Fuzzy_match_with_duration_just_inside_standard_tolerance_merges()
    {
        // Arrange
        var storedTitle = _fixture.CreateShortTitle();
        var incomingTitle = DomainTestFixture.CreateFuzzyTitleVariant(
            storedTitle, FuzzyTitleVariantStrategy.SwapAdjacentWords);
        var storedLength = TimeSpan.FromMinutes(30);
        var incomingLength = storedLength + TimeSpan.FromSeconds(59);
        var podcast = _fixture.CreatePodcast();
        var existing = CreateEpisode(storedTitle, storedLength);
        var incoming = CreateEpisode(incomingTitle, incomingLength);

        AssertFuzzyScoreAboveThreshold(storedTitle, incomingTitle, FuzzyTitleVariantStrategy.SwapAdjacentWords);

        // Act
        var isMatch = _matcher.IsMatch(existing, incoming, episodeMatchRegex: null, podcast);

        // Assert
        isMatch.Should().BeTrue();
    }

    // --------------------------------------------------------------------------------------------
    // Cross-platform (YouTube release authority, negative publishing delay) duration boundary: tolerance widens to 5 minutes.
    // --------------------------------------------------------------------------------------------

    [Fact(DisplayName =
        "For YouTube release authority podcasts with negative publishing delay, when the fuzzy title matches and " +
        "duration differs by exactly the cross-platform tolerance (5 minutes), episodes must not merge.")]
    public void Fuzzy_match_with_duration_exactly_at_cross_platform_tolerance_does_not_merge()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var storedTitle = _fixture.CreateShortTitle();
        var incomingTitle = DomainTestFixture.CreateFuzzyTitleVariant(
            storedTitle, FuzzyTitleVariantStrategy.ReplaceWord);
        var youTubeRelease = DomainTestFixture.UtcAtTime(-30, _fixture.CreateNonMidnightTimeOfDay());
        var spotifyRelease = DomainTestFixture.SpotifyCatalogueReleaseDaysAfterYouTube(youTubeRelease, 28);
        var storedLength = TimeSpan.FromMinutes(30);
        var incomingLength = storedLength + CrossPlatformDurationTolerance;
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast, youTubeRelease, storedLength, storedTitle);
        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithTitle(incomingTitle)
            .WithRelease(spotifyRelease)
            .WithDuration(incomingLength));
        var expected = EpisodeExpectation.From(stored);

        AssertFuzzyScoreAboveThreshold(storedTitle, incomingTitle, FuzzyTitleVariantStrategy.ReplaceWord);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().BeEmpty();
        result.FailedEpisodes.Should().BeEmpty();
        result.AddedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    [Fact(DisplayName =
        "For YouTube release authority podcasts with negative publishing delay, when the fuzzy title matches and " +
        "duration differs by less than 5 minutes, episodes may be treated as the same.")]
    public void Fuzzy_match_with_duration_just_inside_cross_platform_tolerance_merges()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        var storedTitle = _fixture.CreateShortTitle();
        var incomingTitle = DomainTestFixture.CreateFuzzyTitleVariant(
            storedTitle, FuzzyTitleVariantStrategy.ReplaceWord);
        var youTubeRelease = DomainTestFixture.UtcAtTime(-30, _fixture.CreateNonMidnightTimeOfDay());
        var spotifyRelease = DomainTestFixture.SpotifyCatalogueReleaseDaysAfterYouTube(youTubeRelease, 28);
        var storedLength = TimeSpan.FromMinutes(30);
        var incomingLength = storedLength + TimeSpan.FromMinutes(4);
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast, youTubeRelease, storedLength, storedTitle);
        var spotifyInput = _fixture.CreateSpotifyCatalogueInput(b => b
            .WithTitle(incomingTitle)
            .WithRelease(spotifyRelease)
            .WithDuration(incomingLength));
        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(spotifyInput.SpotifyId)
            .WithTitle(incomingTitle)
            .WithSpotifyUrl(spotifyInput.SpotifyUrl)
            .WithRelease(spotifyRelease)
            .WithDuration(incomingLength));
        var expected = EpisodeExpectation.From(stored)
            .WithSpotify(spotifyInput.SpotifyId, spotifyInput.SpotifyUrl);

        AssertFuzzyScoreAboveThreshold(storedTitle, incomingTitle, FuzzyTitleVariantStrategy.ReplaceWord);

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.AddedEpisodes.Should().BeEmpty();
        result.MergedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    // --------------------------------------------------------------------------------------------
    // Negative fuzzy: wholly unrelated titles must fall below the threshold and not merge —
    // even when the duration would otherwise be within tolerance. Guards against
    // "fuzzy matches everything" regressions in FuzzySharp or the wrapper.
    // --------------------------------------------------------------------------------------------

    [Fact(DisplayName =
        "When two titles share no meaningful word overlap and releases differ, the fuzzy score " +
        "falls below the threshold and episodes must not merge — even with an identical duration.")]
    public void Unrelated_titles_with_matching_duration_and_different_releases_do_not_merge()
    {
        // Arrange
        // Hand-picked titles with disjoint token sets; WeightedRatio must sit well below 70.
        // Different releases so the release-and-duration path cannot rescue a merge —
        // this isolates the fuzzy-title branch as the sole guard.
        const string storedTitle = "History Of Ancient Roman Politics";
        const string incomingTitle = "Modern Quantum Physics Research Highlights";
        var length = _fixture.CreateDuration();
        var storedRelease = DomainTestFixture.UtcAtTime(-10, _fixture.CreateNonMidnightTimeOfDay());
        var incomingRelease = storedRelease.AddDays(30);
        var podcast = _fixture.CreatePodcast();
        var existing = CreateEpisode(storedTitle, length, storedRelease);
        var incoming = CreateEpisode(incomingTitle, length, incomingRelease);

        Fuzz.WeightedRatio(storedTitle, incomingTitle)
            .Should().BeLessThan(MinFuzzyTitleScore, "titles have disjoint token sets");

        // Act
        var isMatch = _matcher.IsMatch(existing, incoming, episodeMatchRegex: null, podcast);

        // Assert
        isMatch.Should().BeFalse();
    }

    [Fact(DisplayName =
        "For a standard podcast, when the fuzzy score falls below threshold but release and " +
        "duration align exactly, episodes still merge via the release-and-duration path.")]
    public void Fuzzy_below_threshold_but_release_and_duration_align_still_merges()
    {
        // Arrange
        // Standard podcast: release+duration fallback does NOT require fuzzy match (only
        // negative-delay YouTube release authority podcasts do).
        var release = DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay());
        var length = _fixture.CreateDuration();
        const string storedTitle = "History Of Ancient Roman Politics";
        const string incomingTitle = "Modern Quantum Physics Research Highlights";
        var podcast = _fixture.CreatePodcast();
        var existing = CreateEpisode(storedTitle, length, release);
        var incoming = CreateEpisode(incomingTitle, length, release);

        Fuzz.WeightedRatio(storedTitle, incomingTitle)
            .Should().BeLessThan(MinFuzzyTitleScore, "titles have disjoint token sets");

        // Act
        var isMatch = _matcher.IsMatch(existing, incoming, episodeMatchRegex: null, podcast);

        // Assert
        isMatch.Should().BeTrue();
    }

    [Fact(DisplayName =
        "For a YouTube release authority podcast with negative publishing delay, when titles share no fuzzy " +
        "or substring relationship, episodes must not merge on release and duration alignment alone.")]
    public void Disjoint_titles_on_negative_delay_podcast_blocks_release_and_duration_match()
    {
        // Arrange
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcastWithNegativeDelay();
        const string storedTitle = "History Of Ancient Roman Politics";
        const string incomingTitle = "Modern Quantum Physics Research Highlights";
        var youTubeRelease = DomainTestFixture.UtcAtTime(-30, _fixture.CreateNonMidnightTimeOfDay());
        var spotifyRelease = DomainTestFixture.SpotifyCatalogueReleaseDaysAfterYouTube(youTubeRelease, 28);
        var length = _fixture.CreateDuration();
        var stored = _fixture.CreateStoredEpisodeWithYouTubeOnly(
            podcast, youTubeRelease, length, storedTitle);
        var discovered = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithTitle(incomingTitle)
            .WithRelease(spotifyRelease)
            .WithDuration(length));
        var expected = EpisodeExpectation.From(stored);

        Fuzz.WeightedRatio(storedTitle, incomingTitle)
            .Should().BeLessThan(MinFuzzyTitleScore, "titles have disjoint token sets");

        // Act
        var result = _merger.MergeEpisodes(podcast, [stored], [discovered]);

        // Assert
        result.MergedEpisodes.Should().BeEmpty();
        result.AddedEpisodes.Should().ContainSingle();
        stored.ShouldMatchExpectation(expected);
    }

    private static void AssertFuzzyScoreAboveThreshold(
        string storedTitle,
        string incomingTitle,
        FuzzyTitleVariantStrategy strategy)
    {
        Fuzz.WeightedRatio(storedTitle, incomingTitle)
            .Should().BeGreaterThanOrEqualTo(
                MinFuzzyTitleScore,
                "variant strategy {0} must produce a title within FuzzySharp threshold — " +
                "stored=\"{1}\" incoming=\"{2}\"",
                strategy, storedTitle, incomingTitle);
    }

    private Episode CreateEpisode(string title, TimeSpan length, DateTime? release = null) =>
        _fixture.CreateEpisode(e =>
        {
            e.Title = title;
            e.Length = length;
            e.Release = release ?? DateTime.UtcNow;
        });
}
