using FluentAssertions;
using RedditPodcastPoster.People.Services;

namespace RedditPodcastPoster.People.Tests;

public class SocialHandleDeduplicatorTests
{
    [Fact]
    public void Deduplicate_Empty_ReturnsEmpty()
    {
        SocialHandleDeduplicator.Deduplicate([]).Should().BeEmpty();
    }

    [Fact]
    public void Deduplicate_CaseInsensitiveAndAtPrefix_KeepsFirstOccurrence()
    {
        var result = SocialHandleDeduplicator.Deduplicate(["@Foo", "foo", "@FOO", "Bar", "@bar"]);

        result.Should().Equal("@Foo", "@Bar");
    }

    [Fact]
    public void Deduplicate_SpaceDelimited_ExpandsAndDedupes()
    {
        var result = SocialHandleDeduplicator.Deduplicate(["@MarkBunker4U @XENUTV", "xenutv", "Other"]);

        result.Should().Equal("@MarkBunker4U", "@XENUTV", "@Other");
    }

    [Fact]
    public void Deduplicate_AlreadyTagged_ExcludesMatchesPreservingOrder()
    {
        var result = SocialHandleDeduplicator.Deduplicate(
            ["@Guest", "@Podcast", "@Other"],
            alreadyTagged: ["podcast", "@Already"]);

        result.Should().Equal("@Guest", "@Other");
    }

    [Fact]
    public void Deduplicate_AlreadyTaggedSpaceDelimited_ExcludesAllParts()
    {
        var result = SocialHandleDeduplicator.Deduplicate(
            ["@A", "@B", "@C"],
            alreadyTagged: ["@A @B"]);

        result.Should().Equal("@C");
    }

    [Fact]
    public void Deduplicate_WhitespaceAndNull_Ignored()
    {
        var result = SocialHandleDeduplicator.Deduplicate([null, "  ", "@Ok", null]);

        result.Should().Equal("@Ok");
    }
}
