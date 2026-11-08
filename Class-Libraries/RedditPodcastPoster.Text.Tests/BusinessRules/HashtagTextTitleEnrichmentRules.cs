using FluentAssertions;
using RedditPodcastPoster.Text.HashTags;

namespace RedditPodcastPoster.Text.Tests.BusinessRules;

public class HashtagTextTitleEnrichmentRules
{
    [Theory(DisplayName =
        "Title enrichment: a multi-word enrichment tag with MatchingText uses that compacted form after '#', because spaces on the phrase are not part of the platform hashtag body.")]
    [InlineData("Alpha Beta", "#AlphaBeta", "AlphaBeta")]
    [InlineData("Term One", "#TermOne", "TermOne")]
    public void multi_word_enrichment_uses_matching_text(string tag, string matchingText, string expected)
    {
        // Arrange / Act
        var result = HashtagText.GetTitleEnrichmentTagText(tag, matchingText);

        // Assert
        result.Should().Be(expected);
    }

    [Fact(DisplayName =
        "Title enrichment: a permitted single-word tag with no MatchingText is written as that tag body, because end and episode hashtags gate on the stored Tag.")]
    public void permitted_tag_without_matching_text_is_written()
    {
        // Arrange / Act
        var result = HashtagText.GetTitleEnrichmentTagText("Cult", null);

        // Assert
        result.Should().Be("Cult");
    }
}
