using FluentAssertions;
using Moq;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.People.Tests;

public class PersonGuestHandleResolverTests
{
    private readonly Mock<IPersonService> _personService = new();
    private readonly PersonGuestHandleResolver _sut;

    public PersonGuestHandleResolverTests()
    {
        _sut = new PersonGuestHandleResolver(_personService.Object);
    }

    [Fact]
    public async Task Resolve_EpisodeHandles_DeduplicatesCaseInsensitively()
    {
        var episode = new Episode
        {
            TwitterHandles = ["@Foo", "foo", "@Bar"],
            BlueskyHandles = ["@a.bsky.social", "A.bsky.social", "@b.bsky.social"]
        };

        var (twitter, bluesky) = await _sut.Resolve(episode);

        twitter.Should().Equal("@Foo", "@Bar");
        bluesky.Should().Equal("@a.bsky.social", "@b.bsky.social");
        _personService.Verify(x => x.GetByNames(It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task Resolve_FromGuests_DeduplicatesSharedAndSpaceDelimitedHandles()
    {
        var episode = new Episode { Guests = ["Alice", "Bob"] };
        _personService
            .Setup(x => x.GetByNames(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([
                new Person("Alice")
                {
                    TwitterHandle = "@Shared @Extra",
                    BlueskyHandle = "@alice.bsky.social"
                },
                new Person("Bob")
                {
                    TwitterHandle = "@shared",
                    BlueskyHandle = "@bob.bsky.social @alice.bsky.social"
                }
            ]);

        var (twitter, bluesky) = await _sut.Resolve(episode);

        twitter.Should().Equal("@Shared", "@Extra");
        bluesky.Should().Equal("@alice.bsky.social", "@bob.bsky.social");
    }

    [Fact]
    public async Task Resolve_EpisodeHandlesPresent_DoesNotLookupGuests()
    {
        var episode = new Episode
        {
            Guests = ["Alice"],
            TwitterHandles = ["@FromEpisode"],
            BlueskyHandles = ["from.bsky.social"]
        };

        var (twitter, bluesky) = await _sut.Resolve(episode);

        twitter.Should().Equal("@FromEpisode");
        bluesky.Should().Equal("@from.bsky.social");
        _personService.Verify(x => x.GetByNames(It.IsAny<IEnumerable<string>>()), Times.Never);
    }

    [Fact]
    public async Task Resolve_NoHandlesOrGuests_ReturnsEmpty()
    {
        var (twitter, bluesky) = await _sut.Resolve(new Episode());

        twitter.Should().BeEmpty();
        bluesky.Should().BeEmpty();
    }
}
