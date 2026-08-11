using Api.Dtos.Mapping;
using FluentAssertions;
using RedditPodcastPoster.Models.TitleCasing;
using Xunit;

namespace FunctionHost.Tests.Api.Dtos;

public class TitleCasingRulesResponseBuilderTests
{
    [Fact(DisplayName =
        "Title-casing GET response: when the document is NonEnglish, ignoredSubjects are surfaced, because admins manage language-level subject ignores from that list.")]
    public void build_non_english_includes_ignored_subjects()
    {
        // Arrange
        var document = new NonEnglishTitleCasingRulesDocument("es")
        {
            LowerCaseTerms = ["de", "la"],
            IgnoredSubjects = ["Hoy", "Casa"]
        };

        // Act
        var response = TitleCasingRulesResponseBuilder.Build(document, isDefault: false);

        // Assert
        response.Language.Should().Be("es");
        response.IsDefault.Should().BeFalse();
        response.IgnoredSubjects.Should().Equal("Hoy", "Casa");
        response.LowerCaseTerms.Should().Equal("de", "la");
    }

    [Fact(DisplayName =
        "Title-casing GET response: when the document is English or Universal, ignoredSubjects is empty, because those document types do not store subject ignores.")]
    public void build_english_and_universal_omit_ignored_subjects()
    {
        // Arrange
        var english = new EnglishTitleCasingRulesDocument
        {
            LowerCaseTerms = ["the", "and"]
        };
        var universal = new UniversalTitleCasingRulesDocument();

        // Act
        var englishResponse = TitleCasingRulesResponseBuilder.Build(english, isDefault: true);
        var universalResponse = TitleCasingRulesResponseBuilder.Build(universal, isDefault: false);

        // Assert
        englishResponse.IgnoredSubjects.Should().BeEmpty();
        universalResponse.IgnoredSubjects.Should().BeEmpty();
    }
}
