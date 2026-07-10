using FluentAssertions;
using Moq;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.People.Factories;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.People.Tests;

public class PersonNameConflictTests
{
    [Fact]
    public async Task GetByName_UsesNormalizedKey_CaseInsensitive()
    {
        var existing = new PersonFactory().Create("Ilhan Omar");
        var repository = new Mock<IPersonRepository>();
        repository
            .Setup(x => x.GetByName("ilhan omar"))
            .ReturnsAsync(existing);

        var result = await repository.Object.GetByName("ilhan omar");

        result.Should().BeSameAs(existing);
        repository.Verify(x => x.GetByName("ilhan omar"), Times.Once);
    }

    [Fact]
    public async Task Match_FindsPerson_WhenCaseDiffers()
    {
        var existing = new PersonFactory().Create("Ilhan Omar");
        var repository = new Mock<IPersonRepository>();
        repository
            .Setup(x => x.GetAll())
            .Returns(new[] { existing }.ToAsyncEnumerable());

        var sut = new PersonService(repository.Object, Mock.Of<ITextSanitiser>());

        var result = await sut.Match("ilhan omar");

        result.Should().BeSameAs(existing);
    }

    [Fact]
    public void ConflictingNameKeys_AreDetected()
    {
        var existingKey = Person.NormalizeNameKey("Ilhan Omar");
        var incomingKey = Person.NormalizeNameKey("ilhan omar");
        var selfKey = Person.NormalizeNameKey("Someone Else");

        (existingKey == incomingKey).Should().BeTrue();
        (existingKey == selfKey).Should().BeFalse();
    }
}
