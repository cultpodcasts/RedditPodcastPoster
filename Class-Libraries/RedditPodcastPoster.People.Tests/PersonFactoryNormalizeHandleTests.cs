using FluentAssertions;
using RedditPodcastPoster.People.Factories;

namespace RedditPodcastPoster.People.Tests;

public class PersonFactoryNormalizeHandleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeHandle_Blank_ReturnsNull(string? input)
    {
        PersonFactory.NormalizeHandle(input).Should().BeNull();
    }

    [Theory]
    [InlineData("@MarkBunker4U", "@MarkBunker4U")]
    [InlineData("MarkBunker4U", "@MarkBunker4U")]
    [InlineData("  MarkBunker4U  ", "@MarkBunker4U")]
    public void NormalizeHandle_SingleHandle_EnsuresAtPrefix(string input, string expected)
    {
        PersonFactory.NormalizeHandle(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("@MarkBunker4U @XENUTV", "@MarkBunker4U @XENUTV")]
    [InlineData("MarkBunker4U XENUTV", "@MarkBunker4U @XENUTV")]
    [InlineData("@MarkBunker4U XENUTV", "@MarkBunker4U @XENUTV")]
    [InlineData("  MarkBunker4U   @XENUTV  ", "@MarkBunker4U @XENUTV")]
    public void NormalizeHandle_SpaceDelimited_NormalizesEachPart(string input, string expected)
    {
        PersonFactory.NormalizeHandle(input).Should().Be(expected);
    }

    [Fact]
    public void PersonFactory_Create_NormalizesSpaceDelimitedHandles()
    {
        var person = new PersonFactory().Create(
            "Mark Bunker",
            twitterHandle: "MarkBunker4U XENUTV",
            blueskyHandle: "@mark.bsky.social other.bsky.social");

        person.TwitterHandle.Should().Be("@MarkBunker4U @XENUTV");
        person.BlueskyHandle.Should().Be("@mark.bsky.social @other.bsky.social");
    }
}
