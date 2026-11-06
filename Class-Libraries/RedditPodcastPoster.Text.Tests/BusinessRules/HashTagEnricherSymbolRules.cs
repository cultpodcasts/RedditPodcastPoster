using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Text.Enrichers;

namespace RedditPodcastPoster.Text.Tests.BusinessRules;

public class HashTagEnricherSymbolRules
{
    private readonly AutoMocker _mocker = new();

    [Theory(DisplayName =
        "A stored tag that contains a regex symbol is matched as that exact text and written with one leading hash, because the tag is data, not a pattern.")]
    [InlineData("Notes on C++ today", "C++", "Notes on #C++ today")]
    [InlineData("Community LGBTQ+ meetup notes", "LGBTQ+", "Community #LGBTQ+ meetup notes")]
    [InlineData("The Q&A hour starts now", "Q&A", "The #Q&A hour starts now")]
    [InlineData("See a.b now please", "a.b", "See #a.b now please")]
    public void symbol_tag_is_matched_literally(string input, string hashTag, string expected)
    {
        // Arrange
        var sut = _mocker.CreateInstance<HashTagEnricher>();

        // Act
        var (result, added) = sut.AddHashTag(input, hashTag);

        // Assert
        added.Should().BeTrue();
        result.Should().Be(expected);
    }

    [Theory(DisplayName =
        "A stored tag that contains a regex symbol does not match a different word, because a quantifier or a dot in the tag must not widen the match.")]
    [InlineData("Talk about LGBTQQ today", "LGBTQ+")]
    [InlineData("Talk about axb today", "a.b")]
    public void symbol_tag_does_not_match_a_different_word(string input, string hashTag)
    {
        // Arrange
        var sut = _mocker.CreateInstance<HashTagEnricher>();

        // Act
        var (result, added) = sut.AddHashTag(input, hashTag);

        // Assert
        added.Should().BeFalse();
        result.Should().Be(input);
    }

    [Theory(DisplayName =
        "A stored tag containing $ is written unchanged apart from the leading #, because a dollar sign in the written tag is literal text and not a substitution backreference.")]
    [InlineData("Talk about Price$1 today", "Price$1", "Talk about #Price$1 today")]
    [InlineData("Talk about $$ today", "$$", "Talk about #$$ today")]
    public void dollar_tag_is_written_literally(string input, string hashTag, string expected)
    {
        // Arrange
        var sut = _mocker.CreateInstance<HashTagEnricher>();

        // Act
        var (result, added) = sut.AddHashTag(input, hashTag);

        // Assert
        added.Should().BeTrue();
        result.Should().Be(expected);
    }
}
