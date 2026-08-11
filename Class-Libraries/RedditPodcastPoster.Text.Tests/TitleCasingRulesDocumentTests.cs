using FluentAssertions;
using RedditPodcastPoster.Models.TitleCasing;

namespace RedditPodcastPoster.Text.Tests;

public class TitleCasingRulesDocumentTests
{
    [Fact(DisplayName = "NormaliseLanguage preserves the universal key '*'.")]
    public void NormaliseLanguage_WithUniversalKey_PreservesStar()
    {
        // Arrange
        // Act
        var result = TitleCasingRulesDocument.NormaliseLanguage(
            TitleCasingRulesDocument.UniversalLanguageKey);

        // Assert
        result.Should().Be(TitleCasingRulesDocument.UniversalLanguageKey);
    }

    [Fact(DisplayName = "IdForLanguage is stable for the universal key.")]
    public void IdForLanguage_WithUniversalKey_IsStable()
    {
        // Arrange
        // Act
        var first = TitleCasingRulesDocument.IdForLanguage(
            TitleCasingRulesDocument.UniversalLanguageKey);
        var second = TitleCasingRulesDocument.IdForLanguage("*");

        // Assert
        first.Should().Be(second);
        first.Should().NotBe(Guid.Empty);
    }

    [Fact(DisplayName = "IsUniversal is true only for the reserved '*' key.")]
    public void IsUniversal_WithStar_IsTrue()
    {
        // Arrange
        // Act
        // Assert
        TitleCasingRulesDocument.IsUniversal("*").Should().BeTrue();
        TitleCasingRulesDocument.IsUniversal("en").Should().BeFalse();
        TitleCasingRulesDocument.IsUniversal(null).Should().BeFalse();
    }
}
