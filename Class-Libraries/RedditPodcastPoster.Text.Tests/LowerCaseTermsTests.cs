using FluentAssertions;
using RedditPodcastPoster.Text.Models;

namespace RedditPodcastPoster.Text.Tests;

public class LowerCaseTermsTests
{
    [Fact(DisplayName = "English Expressions dictionary builds without throwing.")]
    public void Expressions_WhenEvaluated_IsCorrect()
    {
        // Arrange
        // Act
        var act = () =>
        {
            var x = LowerCaseTerms.Expressions;
        };
        // Assert
        act.Should().NotThrow();
    }

    [Theory(DisplayName = "NormaliseLanguageKey: null/empty/en/en-* map to en.")]
    [InlineData(null, "en")]
    [InlineData("", "en")]
    [InlineData("en", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("EN-us", "en")]
    [InlineData("pl", "pl")]
    [InlineData("fr-FR", "fr")]
    [InlineData("*", "*")]
    public void NormaliseLanguageKey_MapsAsExpected(string? language, string expected)
    {
        // Arrange
        // Act
        var result = LowerCaseTerms.NormaliseLanguageKey(language);
        // Assert
        result.Should().Be(expected);
    }

    [Fact(DisplayName = "BuildExpressions: mid-title lookbehind words and etc are present.")]
    public void BuildExpressions_IncludesWordsAndEtc()
    {
        // Arrange
        // Act
        var result = LowerCaseTerms.BuildExpressions(["the", "etc"], includeOrdinals: true);
        // Assert
        result.Should().ContainKey("the");
        result.Should().ContainKey("etc");
        result.Should().ContainKey("th");
    }
}
