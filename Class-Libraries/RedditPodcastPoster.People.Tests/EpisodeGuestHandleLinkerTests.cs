using FluentAssertions;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.People.Tests;

public class EpisodeGuestHandleLinkerTests
{
    [Fact]
    public void BuildHandleToNameMap_ExpandsSpaceDelimitedAndNormalizesAt()
    {
        var people = new[]
        {
            new Person("Alice") { TwitterHandle = "alice @AliceAlt", BlueskyHandle = "alice.bsky.social" },
            new Person("Bob") { TwitterHandle = "@bob" }
        };

        var map = EpisodeGuestHandleLinker.BuildHandleToNameMap(people);

        map["alice"].Should().Be("Alice");
        map["AliceAlt"].Should().Be("Alice");
        map["alice.bsky.social"].Should().Be("Alice");
        map["bob"].Should().Be("Bob");
    }

    [Fact]
    public void BuildHandleToNameMap_Conflict_KeepsFirstAndNotifies()
    {
        var conflicts = new List<(string Handle, string First, string Second)>();
        var people = new[]
        {
            new Person("First") { TwitterHandle = "@shared" },
            new Person("Second") { TwitterHandle = "shared" }
        };

        var map = EpisodeGuestHandleLinker.BuildHandleToNameMap(
            people,
            (handle, first, second) => conflicts.Add((handle, first, second)));

        map["shared"].Should().Be("First");
        conflicts.Should().ContainSingle();
        conflicts[0].First.Should().Be("First");
        conflicts[0].Second.Should().Be("Second");
    }

    [Fact]
    public void Match_UnionsCanonicalNames_NeverRemovesExisting()
    {
        var map = EpisodeGuestHandleLinker.BuildHandleToNameMap(
        [
            new Person("Janja Lalich") { TwitterHandle = "@janja" },
            new Person("Steven Hassan") { BlueskyHandle = "steven.bsky.social" }
        ]);

        var episode = new Episode { Guests = ["Existing Guest"] };

        var result = EpisodeGuestHandleLinker.Match(
            episode,
            ["janja", "@other"],
            ["steven.bsky.social"],
            map);

        result.HasAdditions.Should().BeTrue();
        result.GuestsToAdd.Should().Equal("Janja Lalich", "Steven Hassan");
        result.MatchedHandles.Should().BeEquivalentTo(["@janja", "@steven.bsky.social"]);
        result.ResultingGuests.Should().Equal("Existing Guest", "Janja Lalich", "Steven Hassan");
        episode.Guests.Should().Equal("Existing Guest");
    }

    [Fact]
    public void Match_DeduplicatesGuestsCaseInsensitive()
    {
        var map = EpisodeGuestHandleLinker.BuildHandleToNameMap(
        [
            new Person("Janja Lalich") { TwitterHandle = "@janja" }
        ]);

        var episode = new Episode { Guests = ["janja lalich"] };

        var result = EpisodeGuestHandleLinker.Match(episode, ["@Janja"], null, map);

        result.HasAdditions.Should().BeFalse();
        result.GuestsToAdd.Should().BeEmpty();
        result.MatchedHandles.Should().Equal("@Janja");
        result.ResultingGuests.Should().Equal("janja lalich");
    }

    [Fact]
    public void Match_SpaceDelimitedEpisodeHandles_ExpandAndMatch()
    {
        var map = EpisodeGuestHandleLinker.BuildHandleToNameMap(
        [
            new Person("Mark Bunker") { TwitterHandle = "@MarkBunker4U" },
            new Person("Xenu TV") { TwitterHandle = "@XENUTV" }
        ]);

        var episode = new Episode();

        var result = EpisodeGuestHandleLinker.Match(
            episode,
            ["@MarkBunker4U @XENUTV"],
            null,
            map);

        result.GuestsToAdd.Should().Equal("Mark Bunker", "Xenu TV");
        result.MatchedHandles.Should().Equal("@MarkBunker4U", "@XENUTV");
    }

    [Fact]
    public void Match_NoHandles_ReturnsEmpty()
    {
        var map = EpisodeGuestHandleLinker.BuildHandleToNameMap(
        [
            new Person("Alice") { TwitterHandle = "@alice" }
        ]);

        var result = EpisodeGuestHandleLinker.Match(
            new Episode { Guests = ["Keep"] },
            null,
            null,
            map);

        result.Should().Be(EpisodeGuestLinkMatch.Empty);
    }

    [Fact]
    public void Match_HandlesWithoutPerson_NoAdditions()
    {
        var map = EpisodeGuestHandleLinker.BuildHandleToNameMap(
        [
            new Person("Alice") { TwitterHandle = "@alice" }
        ]);

        var episode = new Episode();
        var result = EpisodeGuestHandleLinker.Match(episode, ["@unknown"], null, map);

        result.HasAdditions.Should().BeFalse();
        result.GuestsToAdd.Should().BeEmpty();
        result.MatchedHandles.Should().BeEmpty();
    }
}
