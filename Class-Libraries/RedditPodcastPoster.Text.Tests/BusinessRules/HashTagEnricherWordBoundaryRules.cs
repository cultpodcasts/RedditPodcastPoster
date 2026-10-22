using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Text.Enrichers;

namespace RedditPodcastPoster.Text.Tests.BusinessRules;

public class HashTagEnricherWordBoundaryRules
{
    private readonly AutoMocker _mocker = new();

    [Theory(DisplayName =
        "A hashtag is inserted before a whole-word match that is followed by a dash, apostrophe or emoji, and the following text is left unchanged, because the Bluesky facet scanner and X's own parser end the tag at that character.")]
    [InlineData("Escaped a Term—and More", "Escaped a #Term—and More")]
    [InlineData("Escaped a Term–and More", "Escaped a #Term–and More")]
    [InlineData("Escaped a Term-and More", "Escaped a #Term-and More")]
    [InlineData("Inside a Term's Rules", "Inside a #Term's Rules")]
    [InlineData("Café 🙏 a Term🙏 Today", "Café 🙏 a #Term🙏 Today")]
    [InlineData("Escaped a Termite—and More", "Escaped a Termite—and More")]
    public void hashtag_inserted_before_word_followed_by_non_word_character(string input, string expected)
    {
        // Arrange
        const string hashTag = "Term";
        var sut = _mocker.CreateInstance<HashTagEnricher>();

        // Act
        var (result, _) = sut.AddHashTag(input, hashTag);

        // Assert
        result.Should().Be(expected);
    }
}
