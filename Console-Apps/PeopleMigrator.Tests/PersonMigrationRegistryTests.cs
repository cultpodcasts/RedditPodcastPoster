using FluentAssertions;
using RedditPodcastPoster.People.Factories;
using Xunit;

namespace PeopleMigrator.Tests;

public class PersonMigrationRegistryTests
{
    [Fact]
    public void Resolve_merges_twitter_only_and_bluesky_only_with_same_local_part()
    {
        var registry = new PersonMigrationRegistry(new PersonFactory());

        var fromTwitter = registry.Resolve("@alice", null);
        var fromBluesky = registry.Resolve(null, "@alice.bsky.social");

        fromTwitter.Person.Id.Should().Be(fromBluesky.Person.Id);
        fromTwitter.Person.TwitterHandle.Should().Be("@alice");
        fromBluesky.Person.BlueskyHandle.Should().Be("@alice.bsky.social");
    }

    [Fact]
    public void LinkPair_merges_different_stems_when_episode_pairs_them_by_index()
    {
        var registry = new PersonMigrationRegistry(new PersonFactory());

        var twitterOnly = registry.Resolve("@AliceSmith", null);
        var blueskyOnly = registry.Resolve(null, "@alice.bsky.social");
        twitterOnly.Person.Id.Should().NotBe(blueskyOnly.Person.Id);

        registry.LinkPair("@AliceSmith", "@alice.bsky.social");

        var mergedTwitter = registry.Resolve("@AliceSmith", null);
        var mergedBluesky = registry.Resolve(null, "@alice.bsky.social");
        mergedTwitter.Person.Id.Should().Be(mergedBluesky.Person.Id);
        mergedTwitter.Person.TwitterHandle.Should().Be("@AliceSmith");
        mergedBluesky.Person.BlueskyHandle.Should().Be("@alice.bsky.social");
    }

    [Fact]
    public void Resolve_does_not_merge_unrelated_handles_that_share_an_episode_field()
    {
        var registry = new PersonMigrationRegistry(new PersonFactory());

        var jennifer = registry.Resolve("JenniferKeyte", null);
        var emily = registry.Resolve("@maitlis", null);
        var amy = registry.Resolve("@msamywallace", null);
        var shelagh = registry.Resolve(null, "shelaghfogarty.bsky.social");
        var emilyBsky = registry.Resolve(null, "@maitlis.bsky.social");

        jennifer.Person.Id.Should().NotBe(emily.Person.Id);
        jennifer.Person.Id.Should().NotBe(amy.Person.Id);
        jennifer.Person.Id.Should().NotBe(shelagh.Person.Id);
        emily.Person.Id.Should().Be(emilyBsky.Person.Id);
        shelagh.Person.Id.Should().NotBe(emily.Person.Id);

        jennifer.Person.TwitterHandle.Should().Be("@JenniferKeyte");
        emily.Person.TwitterHandle.Should().Be("@maitlis");
        emily.Person.BlueskyHandle.Should().Be("@maitlis.bsky.social");
        amy.Person.TwitterHandle.Should().Be("@msamywallace");
        shelagh.Person.BlueskyHandle.Should().Be("@shelaghfogarty.bsky.social");
    }
}

public class PersonHandleNormalizerTests
{
    [Theory]
    [InlineData("@alice.bsky.social", "alice")]
    [InlineData("@AliceSmith", "alicesmith")]
    public void ToMatchToken_normalizes_for_cross_lookup(string input, string expected)
    {
        PersonHandleNormalizer.ToMatchToken(input).Should().Be(expected);
    }

    [Fact]
    public void SplitHandleField_splits_space_separated_handles_and_ignores_hashtags()
    {
        var handles = PersonHandleNormalizer.SplitHandleField("JenniferKeyte @maitlis #CarelessPeople").ToArray();

        handles.Should().Equal("JenniferKeyte", "@maitlis");
    }

    [Fact]
    public void ExpandHandles_splits_each_array_entry()
    {
        var handles = PersonHandleNormalizer.ExpandHandles([
            "JenniferKeyte @maitlis",
            "@msamywallace",
            "shelaghfogarty.bsky.social @maitlis.bsky.social"
        ]).ToArray();

        handles.Should().Equal(
            "JenniferKeyte",
            "@maitlis",
            "@msamywallace",
            "shelaghfogarty.bsky.social",
            "@maitlis.bsky.social");
    }
}
