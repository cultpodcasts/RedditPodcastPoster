using FluentAssertions;
using RedditPodcastPoster.Text.HashTags;

namespace RedditPodcastPoster.Text.Tests.BusinessRules;

public class HashtagTextTitleEnrichmentRules
{
    [Theory(DisplayName =
        "Title enrichment: a multi-word enrichment tag with MatchingText uses that compacted form after '#', because spaces on Tag cannot be written as-is but MatchingText already holds the hashtag body.")]
    [InlineData("Alpha Beta", "#AlphaBeta", "AlphaBeta")]
    [InlineData("Term's A", "#TermsA", "TermsA")]
    [InlineData("Term. A", "#TermA", "TermA")]
    public void multi_word_enrichment_uses_matching_text(string tag, string matchingText, string expected)
    {
        // Arrange / Act
        var result = HashtagText.GetTitleEnrichmentTagText(tag, matchingText);

        // Assert
        result.Should().Be(expected);
    }

    [Theory(DisplayName =
        "Title enrichment: a stored tag without whitespace that cannot be reproduced as-is is skipped even when MatchingText is a shortened form, because writing the shortened tag is not the stored tag.")]
    [InlineData("Some-Tag", "#SomeTag")]
    [InlineData("Q&A", "#Q&A")]
    [InlineData("C++", "#C")]
    public void non_whitespace_tag_that_shortens_is_skipped(string tag, string matchingText)
    {
        // Arrange / Act
        var result = HashtagText.GetTitleEnrichmentTagText(tag, matchingText);

        // Assert
        result.Should().BeNull();
    }

    [Fact(DisplayName =
        "Title enrichment: a reproducible single-word tag with no MatchingText is written as that tag body, because end and episode hashtags gate on the stored Tag.")]
    public void reproducible_tag_without_matching_text_is_written()
    {
        // Arrange / Act
        var result = HashtagText.GetTitleEnrichmentTagText("Cult", null);

        // Assert
        result.Should().Be("Cult");
    }
}
