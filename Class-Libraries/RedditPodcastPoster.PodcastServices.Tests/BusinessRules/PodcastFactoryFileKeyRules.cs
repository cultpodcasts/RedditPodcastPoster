using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Podcasts;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules;

public class PodcastFactoryFileKeyRules
{
    private const string KoreanName = "오늘의 뉴스";
    private const string EmojiOnlyName = "🎙️📻 ✨";

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryPodcastRepository _podcasts = new();

    public PodcastFactoryFileKeyRules()
    {
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<IFileKeyAllocator>(_mocker.CreateInstance<FileKeyAllocator>());
    }

    [Fact(DisplayName =
        "Creating two podcasts with the same name issues two file keys, because the factory remembers the key it just handed out for the rest of the process.")]
    public async Task second_create_with_the_same_name_gets_a_different_file_key()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var sut = _mocker.CreateInstance<PodcastFactory>();

        // Act
        var first = await sut.Create(name);
        var second = await sut.Create(name);

        // Assert
        first.FileKey.Should().Be(FileKeyFactory.GetFileKey(name));
        second.FileKey.Should().Be(FileKeyFactory.GetFileKey(name) + "_2");
    }

    [Fact(DisplayName =
        "Creating a podcast whose name slug a stored podcast already holds (any case) gives it a numbered key instead of a duplicate.")]
    public async Task create_with_stored_key_gets_suffix()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        _podcasts.Seed(_fixture.CreatePodcast(p => p.FileKey = FileKeyFactory.GetFileKey(name).ToUpperInvariant()));
        var sut = _mocker.CreateInstance<PodcastFactory>();

        // Act
        var podcast = await sut.Create(name);

        // Assert
        podcast.FileKey.Should().Be(FileKeyFactory.GetFileKey(name) + "_2");
    }

    [Theory(DisplayName =
        "Creating a podcast with a Korean-only or emoji-only name gives it a key built from its own id, never an empty or underscore key.")]
    [InlineData(KoreanName)]
    [InlineData(EmojiOnlyName)]
    public async Task create_with_non_latin_name_gets_id_key(string name)
    {
        // Arrange
        var sut = _mocker.CreateInstance<PodcastFactory>();

        // Act
        var podcast = await sut.Create(name);

        // Assert
        podcast.FileKey.Should().Be(FileKeyFactory.GetIdSlug(podcast.Id));
        FileKeyFactory.IsUsableSlug(podcast.FileKey).Should().BeTrue();
    }

    [Fact(DisplayName =
        "Creating two podcasts with the same Korean-only name gives them two different id keys, so they never collide.")]
    public async Task two_non_latin_creates_get_distinct_keys()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PodcastFactory>();

        // Act
        var first = await sut.Create(KoreanName);
        var second = await sut.Create(KoreanName);

        // Assert
        second.FileKey.Should().NotBe(first.FileKey);
    }

    [Fact(DisplayName =
        "When a concurrent create issues the same key while this create awaited the store, this create allocates again, so two in-flight creates never share a key.")]
    public async Task key_issued_during_allocation_is_not_reused()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var raced = FileKeyFactory.GetFileKey(name);
        var other = _mocker.CreateInstance<PodcastFactory>();
        var racingMocker = new AutoMocker();
        var allocator = racingMocker.GetMock<IFileKeyAllocator>();
        var calls = 0;
        allocator
            .Setup(x => x.Allocate(FileKeyKind.Podcast, name, It.IsAny<Guid>(), null, It.IsAny<Func<string, bool>?>()))
            .Returns(async (FileKeyKind _, string _, Guid _, string? _, Func<string, bool>? alsoTaken) =>
            {
                if (calls++ == 0)
                {
                    // The concurrent create finishes first and is issued the same key.
                    (await other.Create(name)).FileKey.Should().Be(raced);
                    return raced;
                }

                return FileKeyFactory.MakeUnique(raced, alsoTaken!);
            });
        var sut = racingMocker.CreateInstance<PodcastFactory>();

        // Act
        var podcast = await sut.Create(name);

        // Assert
        podcast.FileKey.Should().Be(raced + "_2");
        calls.Should().Be(2);
    }
}
