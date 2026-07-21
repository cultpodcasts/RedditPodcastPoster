using FluentAssertions;
using Moq;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.People;

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
    public async Task Resolve_WithGuests_LooksUpPeople()
    {
        var episode = new Episode { Guests = ["Alice"] };
        _personService
            .Setup(x => x.GetByNames(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([
                new Person("Alice")
                {
                    TwitterHandle = "@FromPerson",
                    BlueskyHandle = "from.bsky.social"
                }
            ]);

        var (twitter, bluesky) = await _sut.Resolve(episode);

        twitter.Should().Equal("@FromPerson");
        bluesky.Should().Equal("@from.bsky.social");
        _personService.Verify(x => x.GetByNames(It.IsAny<IEnumerable<string>>()), Times.Once);
    }

    [Fact]
    public async Task Resolve_NoGuests_ReturnsEmpty()
    {
        var (twitter, bluesky) = await _sut.Resolve(new Episode());

        twitter.Should().BeEmpty();
        bluesky.Should().BeEmpty();
        _personService.Verify(x => x.GetByNames(It.IsAny<IEnumerable<string>>()), Times.Never);
    }
}
