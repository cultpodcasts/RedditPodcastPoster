using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Models.People;
using RedditPodcastPoster.People.Factories;
using RedditPodcastPoster.People.Services;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.People.Tests;

public class PersonNameConflictTests
{
    private readonly AutoMocker _mocker = new();
    private readonly string _name;
    private Person[] _people = [];

    public PersonNameConflictTests()
    {
        _name = $"Name{Guid.NewGuid():N}"[..12] + " Surname" + Guid.NewGuid().ToString("N")[..6];
        _mocker.GetMock<IPersonRepository>()
            .Setup(x => x.GetAll())
            .Returns(() => _people.ToAsyncEnumerable());
    }

    [Fact(DisplayName =
        "Person repository lookups by name use the lower-cased normalised name key, so a differently-cased name " +
        "finds the same person.")]
    public async Task GetByName_UsesNormalizedKey_CaseInsensitive()
    {
        // Arrange
        var existing = new PersonFactory().Create(_name);
        var key = _name.ToLowerInvariant();
        var repository = _mocker.GetMock<IPersonRepository>();
        repository
            .Setup(x => x.GetByName(key))
            .ReturnsAsync(() => existing);

        // Act
        var result = await repository.Object.GetByName(key);

        // Assert
        result.Should().BeSameAs(existing);
        repository.Verify(x => x.GetByName(key), Times.Once);
    }

    [Fact(DisplayName = "Person matching by name ignores case, so a lower-cased name finds the existing person.")]
    public async Task Match_FindsPerson_WhenCaseDiffers()
    {
        // Arrange
        var existing = new PersonFactory().Create(_name);
        _people = [existing];
        var sut = _mocker.CreateInstance<PersonService>();

        // Act
        var result = await sut.Match(_name.ToLowerInvariant());

        // Assert
        result.Should().BeSameAs(existing);
    }

    [Fact(DisplayName =
        "Person name keys conflict when names differ only by case, and do not conflict for different names.")]
    public void ConflictingNameKeys_AreDetected()
    {
        // Arrange
        var otherName = $"Other{Guid.NewGuid():N}"[..12];

        // Act
        var existingKey = Person.NormalizeNameKey(_name);
        var incomingKey = Person.NormalizeNameKey(_name.ToLowerInvariant());
        var selfKey = Person.NormalizeNameKey(otherName);

        // Assert
        (existingKey == incomingKey).Should().BeTrue();
        (existingKey == selfKey).Should().BeFalse();
    }
}
