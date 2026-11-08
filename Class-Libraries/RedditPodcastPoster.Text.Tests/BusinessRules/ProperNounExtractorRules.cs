using FluentAssertions;
using RedditPodcastPoster.Text.Matchers;

namespace RedditPodcastPoster.Text.Tests.BusinessRules;

public class ProperNounExtractorRules
{
    [Fact(DisplayName =
        "Multi-word capitalized runs are extracted from descriptions and leading stopwords like The/Dr are stripped.")]
    public void extracts_from_description_stripping_leading_stopwords()
    {
        // Arrange
        const string description =
            "The journalist Gil Duran explains TESCREAL with Sam Altman after meeting Peter Thiel.";

        // Act
        var nouns = ProperNounExtractor.Extract(description);

        // Assert
        nouns.Should().Contain("Gil Duran");
        nouns.Should().Contain("Sam Altman");
        nouns.Should().Contain("Peter Thiel");
        nouns.Should().NotContain("The");
        nouns.Should().NotContain("TESCREAL");
    }

    [Fact(DisplayName =
        "Shared returns only description phrases present on both sides and excludes names supplied in the exclude list.")]
    public void shared_respects_exclude_list()
    {
        // Arrange — sentence-case descriptions (titles are not inputs to this API)
        const string left = "Conversation with Peter Thiel and Elon Musk about markets.";
        const string right = "Gil Duran reports on Peter Thiel.";

        // Act
        var shared = ProperNounExtractor.Shared(left, right, exclude: ["Peter Thiel"]);

        // Assert
        shared.Should().BeEmpty();
        ProperNounExtractor.Shared(left, right)
            .Should().Contain("Peter Thiel");
    }

    [Fact(DisplayName =
        "A Title-Case marketing title scanned as text yields many capitalized runs, which is why the catalogue " +
        "scorer must pass descriptions only and never episode titles into the extractor.")]
    public void title_case_marketing_strings_are_noisy_capitalized_runs()
    {
        // Arrange
        const string titleCaseTitle = "The Nerd Reich And Silicon Valley Power With Gil Duran";

        // Act
        var nouns = ProperNounExtractor.Extract(titleCaseTitle);

        // Assert — noise exists if titles were fed in; documents the casing hazard for callers
        nouns.Count.Should().BeGreaterThan(1);
        nouns.Should().Contain("Nerd Reich");
    }
}
