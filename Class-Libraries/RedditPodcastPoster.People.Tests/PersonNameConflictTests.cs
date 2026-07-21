using FluentAssertions;
using Moq;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.People.Factories;
using RedditPodcastPoster.Text;

namespace RedditPodcastPoster.People.Tests;

public class PersonNameConflictTests
{
    [Fact]
    public async Task GetByName_UsesNormalizedKey_CaseInsensitive()
    {
        var existing = new PersonFactory().Create("Ada Example");
        var repository = new Mock<IPersonRepository>();
        repository
            .Setup(x => x.GetByName("ada example"))
            .ReturnsAsync(existing);

        var result = await repository.Object.GetByName("ada example");

        result.Should().BeSameAs(existing);
        repository.Verify(x => x.GetByName("ada example"), Times.Once);
    }

    [Fact]
    public async Task Match_FindsPerson_WhenCaseDiffers()
    {
        var existing = new PersonFactory().Create("Ada Example");
        var repository = new Mock<IPersonRepository>();
        repository
            .Setup(x => x.GetAll())
            .Returns(new[] { existing }.ToAsyncEnumerable());

        var sut = new PersonService(repository.Object, Mock.Of<ITextSanitiser>());

        var result = await sut.Match("ada example");

        result.Should().BeSameAs(existing);
    }

    [Fact]
    public void ConflictingNameKeys_AreDetected()
    {
        var existingKey = Person.NormalizeNameKey("Ada Example");
        var incomingKey = Person.NormalizeNameKey("ada example");
        var selfKey = Person.NormalizeNameKey("Someone Else");

        (existingKey == incomingKey).Should().BeTrue();
        (existingKey == selfKey).Should().BeFalse();
    }
}
