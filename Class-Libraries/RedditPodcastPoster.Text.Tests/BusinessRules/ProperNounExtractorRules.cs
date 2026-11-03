using FluentAssertions;
using RedditPodcastPoster.Text.Matchers;

namespace RedditPodcastPoster.Text.Tests.BusinessRules;

public class ProperNounExtractorRules
{
    [Fact(DisplayName =
        "Multi-word capitalized runs are extracted from title and description, and leading stopwords like The/Dr are stripped.")]
    public void extracts_multi_word_runs_stripping_leading_stopwords()
    {
        // Arrange
        const string title = "The Nerd Reich and Peter Thiel";
        const string description = "Journalist Gil Duran explains TESCREAL with Sam Altman.";

        // Act
        var nouns = ProperNounExtractor.Extract(title, description);

        // Assert
        nouns.Should().Contain("Nerd Reich");
        nouns.Should().Contain("Peter Thiel");
        nouns.Should().Contain("Gil Duran");
        nouns.Should().Contain("Sam Altman");
        nouns.Should().NotContain("The");
        nouns.Should().NotContain("TESCREAL");
    }

    [Fact(DisplayName =
        "Shared returns only phrases present on both sides and excludes names supplied in the exclude list.")]
    public void shared_respects_exclude_list()
    {
        // Arrange
        const string leftTitle = "Peter Thiel & Elon Musk";
        const string rightTitle = "Gil Duran on Peter Thiel";

        // Act
        var shared = ProperNounExtractor.Shared(leftTitle, null, rightTitle, null, exclude: ["Peter Thiel"]);

        // Assert
        shared.Should().BeEmpty();
        ProperNounExtractor.Shared(leftTitle, null, rightTitle, null)
            .Should().Contain("Peter Thiel");
    }
}
