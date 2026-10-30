using FluentAssertions;
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
    // Fixed synthetic names: the expected keys below are written out, so no rule depends on a random title.
    private const string LatinName = "Synthetic Cult Watch";
    private const string LatinKey = "synthetic_cult_watch";
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
        "A Latin name gives the same key it always did.")]
    public async Task latin_name_gets_its_slug()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PodcastFactory>();

        // Act
        var podcast = await sut.Create(LatinName);

        // Assert
        podcast.FileKey.Should().Be(LatinKey);
    }

    [Fact(DisplayName =
        "A one-letter Latin name still gives a one-letter key, as before: only empty, underscore and _N slugs are placeholders.")]
    public async Task one_letter_latin_name_keeps_one_letter_key()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PodcastFactory>();

        // Act
        var podcast = await sut.Create("X");

        // Assert
        podcast.FileKey.Should().Be("x");
    }

    [Fact(DisplayName =
        "A create that is never saved reserves nothing: the next create of the same name gets the same key, not _2.")]
    public async Task unsaved_create_does_not_reserve_its_key()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PodcastFactory>();
        var abandoned = await sut.Create(LatinName);

        // Act
        var next = await sut.Create(LatinName);

        // Assert
        abandoned.FileKey.Should().Be(LatinKey);
        next.FileKey.Should().Be(LatinKey);
    }

    [Fact(DisplayName =
        "Once the first podcast is saved, a second create of the same name gets the _2 suffix.")]
    public async Task saved_create_makes_next_create_suffixed()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PodcastFactory>();
        await _podcasts.Save(await sut.Create(LatinName));

        // Act
        var second = await sut.Create(LatinName);

        // Assert
        second.FileKey.Should().Be(LatinKey + "_2");
    }

    [Fact(DisplayName =
        "Creating a podcast whose name slug a stored podcast already holds (any case) gives it a numbered key instead of a duplicate.")]
    public async Task create_with_stored_key_gets_suffix()
    {
        // Arrange
        _podcasts.Seed(_fixture.CreatePodcast(p => p.FileKey = LatinKey.ToUpperInvariant()));
        var sut = _mocker.CreateInstance<PodcastFactory>();

        // Act
        var podcast = await sut.Create(LatinName);

        // Assert
        podcast.FileKey.Should().Be(LatinKey + "_2");
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
}
