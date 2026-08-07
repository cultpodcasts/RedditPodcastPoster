using FluentAssertions;
using RedditPodcastPoster.Models.TitleCasing;

namespace RedditPodcastPoster.Text.Tests;

public class LanguageTitleCasingRulesDocumentTests
{
    [Fact(DisplayName = "NormaliseLanguage preserves the universal key '*'.")]
    public void NormaliseLanguage_WithUniversalKey_PreservesStar()
    {
        // Arrange
        // Act
        var result = LanguageTitleCasingRulesDocument.NormaliseLanguage(
            LanguageTitleCasingRulesDocument.UniversalLanguageKey);

        // Assert
        result.Should().Be(LanguageTitleCasingRulesDocument.UniversalLanguageKey);
    }

    [Fact(DisplayName = "IdForLanguage is stable for the universal key.")]
    public void IdForLanguage_WithUniversalKey_IsStable()
    {
        // Arrange
        // Act
        var first = LanguageTitleCasingRulesDocument.IdForLanguage(
            LanguageTitleCasingRulesDocument.UniversalLanguageKey);
        var second = LanguageTitleCasingRulesDocument.IdForLanguage("*");

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
        LanguageTitleCasingRulesDocument.IsUniversal("*").Should().BeTrue();
        LanguageTitleCasingRulesDocument.IsUniversal("en").Should().BeFalse();
        LanguageTitleCasingRulesDocument.IsUniversal(null).Should().BeFalse();
    }
}
