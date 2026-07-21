using FluentAssertions;
using FuzzySharp;
using RedditPodcastPoster.Text.Matchers;

namespace RedditPodcastPoster.Text.Tests;

/// <summary>
/// Regression harness for <see cref="FuzzyMatcher"/> and its underlying FuzzySharp WeightedRatio.
/// <para>
/// These tests pin threshold behavior for known input pairs so a silent FuzzySharp upgrade —
/// or an internal wrapper change — that alters scoring will surface as an obvious failure.
/// The pairs are chosen to span the production thresholds used by matcher/finder code
/// (65 for platform finders, 70 for <c>EpisodePlatformMatcher.MinFuzzyTitleScore</c>, 75–80
/// for stricter finder configurations).
/// </para>
/// </summary>
public class FuzzyMatcherTests
{
    private sealed record TitleItem(string Value);

    private static readonly TitleItem[] Empty = [];

    // ------------------------------------------------------------------------------------------
    // IsMatch — threshold contract
    // ------------------------------------------------------------------------------------------

    [Theory(DisplayName = "IsMatch: identical strings score 100 and match at every production threshold.")]
    [InlineData(65)]
    [InlineData(70)]
    [InlineData(75)]
    [InlineData(80)]
    [InlineData(100)]
    public void IsMatch_WhenIdenticalStrings_MatchesAtEveryThreshold(int threshold)
    {
        // Arrange
        var item = new TitleItem("Deep Dive Interview Special Episode");

        // Act
        var isMatch = FuzzyMatcher.IsMatch(item.Value, item, x => x.Value, threshold);

        // Assert
        Fuzz.WeightedRatio(item.Value, item.Value).Should().Be(100);
        isMatch.Should().BeTrue();
    }

    [Theory(DisplayName = "IsMatch: wholly disjoint token sets fall below all production thresholds.")]
    [InlineData(65)]
    [InlineData(70)]
    [InlineData(75)]
    [InlineData(80)]
    public void IsMatch_WhenTitlesShareNoTokens_FailsAtEveryProductionThreshold(int threshold)
    {
        // Arrange
        const string query = "History Of Ancient Roman Politics";
        var item = new TitleItem("Modern Quantum Physics Research Highlights");

        // Act
        var isMatch = FuzzyMatcher.IsMatch(query, item, x => x.Value, threshold);

        // Assert
        Fuzz.WeightedRatio(query, item.Value)
            .Should().BeLessThan(65, "disjoint token sets should score below the lowest production threshold");
        isMatch.Should().BeFalse();
    }

    [Theory(DisplayName =
        "IsMatch: a one-word replacement in a short title stays above the 70 and 80 finder thresholds " +
        "but falls below the strictest 90 threshold — pins the score bucket for regression detection.")]
    [InlineData(65, true)]
    [InlineData(70, true)]
    [InlineData(80, true)]
    [InlineData(90, false)]
    public void IsMatch_WhenOneWordReplacedInShortTitle_RespectsThreshold(int threshold, bool expectedMatch)
    {
        // Arrange
        // A 5-word title with one middle word replaced (length-matched swap).
        const string query = "Deep Dive Interview Special Episode";
        var item = new TitleItem("Deep Dive Analysis Special Episode");

        // Act
        var isMatch = FuzzyMatcher.IsMatch(query, item, x => x.Value, threshold);

        // Assert
        var score = Fuzz.WeightedRatio(query, item.Value);
        score.Should().BeInRange(78, 88,
            "regression baseline for one-word replacement in a 5-word title (observed ~81)");
        isMatch.Should().Be(expectedMatch);
    }

    [Theory(DisplayName =
        "IsMatch: replacing three consecutive words in a short title drops WeightedRatio below the 70 " +
        "fuzzy-title threshold — pins the score bucket where fuzzy must reject the pair.")]
    [InlineData(65, false)]
    [InlineData(70, false)]
    [InlineData(80, false)]
    public void IsMatch_WhenThreeWordsReplacedInShortTitle_FailsAt70(int threshold, bool expectedMatch)
    {
        // Arrange
        const string query = "Deep Dive Interview Special Episode";
        var item = new TitleItem("History Ancient Roman Special Episode");

        // Act
        var isMatch = FuzzyMatcher.IsMatch(query, item, x => x.Value, threshold);

        // Assert
        var score = Fuzz.WeightedRatio(query, item.Value);
        score.Should().BeLessThan(65,
            "regression baseline: replacing three of five words must drop below all production thresholds");
        isMatch.Should().Be(expectedMatch);
    }

    [Theory(DisplayName =
        "IsMatch: adjacent-word swap in a short title stays at or above the 80 finder threshold — " +
        "WeightedRatio treats token-order variation as near-identical.")]
    [InlineData(65, true)]
    [InlineData(70, true)]
    [InlineData(80, true)]
    public void IsMatch_WhenAdjacentWordsSwapped_MatchesUpToStrictestFinderThreshold(int threshold, bool expectedMatch)
    {
        // Arrange
        const string query = "Deep Dive Interview Special Episode";
        var item = new TitleItem("Deep Interview Dive Special Episode");

        // Act
        var isMatch = FuzzyMatcher.IsMatch(query, item, x => x.Value, threshold);

        // Assert
        Fuzz.WeightedRatio(query, item.Value)
            .Should().BeGreaterThanOrEqualTo(80, "adjacent-word swap should stay high on WeightedRatio");
        isMatch.Should().Be(expectedMatch);
    }

    // ------------------------------------------------------------------------------------------
    // Match<T> — selector API smoke tests
    // ------------------------------------------------------------------------------------------

    [Fact(DisplayName = "Match: returns the item whose selector value scores highest against the query.")]
    public void Match_WhenCandidatesProvided_ReturnsClosestByScore()
    {
        // Arrange
        const string query = "Deep Dive Interview Special Episode";
        var candidates = new[]
        {
            new TitleItem("Modern Quantum Physics Research Highlights"),
            new TitleItem("Deep Dive Interview Special Episode Analysis"),
            new TitleItem("History Of Ancient Roman Politics")
        };

        // Act
        var match = FuzzyMatcher.Match(query, candidates, x => x.Value);

        // Assert
        match.Should().NotBeNull();
        match!.Value.Should().Be("Deep Dive Interview Special Episode Analysis");
    }

    [Fact(DisplayName = "Match with min threshold: returns null when the closest candidate scores below the floor.")]
    public void Match_WithMinThreshold_WhenAllCandidatesBelowFloor_ReturnsNull()
    {
        // Arrange
        const string query = "Deep Dive Interview Special Episode";
        var candidates = new[]
        {
            new TitleItem("Modern Quantum Physics Research Highlights"),
            new TitleItem("History Of Ancient Roman Politics")
        };

        // Act
        var match = FuzzyMatcher.Match(query, candidates, x => x.Value, min: 65);

        // Assert
        match.Should().BeNull();
    }

    [Fact(DisplayName = "Match with min threshold: returns the closest candidate when at least one meets the floor.")]
    public void Match_WithMinThreshold_WhenAtLeastOneCandidateAboveFloor_ReturnsClosest()
    {
        // Arrange
        const string query = "Deep Dive Interview Special Episode";
        var candidates = new[]
        {
            new TitleItem("Deep Dive Interview Special Episode"),
            new TitleItem("Modern Quantum Physics Research Highlights")
        };

        // Act
        var match = FuzzyMatcher.Match(query, candidates, x => x.Value, min: 70);

        // Assert
        match.Should().NotBeNull();
        match!.Value.Should().Be("Deep Dive Interview Special Episode");
    }
}
