using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Podcasts;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules;

public class PodcastFactoryFileKeyRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public PodcastFactoryFileKeyRules()
    {
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetAll())
            .Returns(Empty<RedditPodcastPoster.Models.Podcasts.Podcast>());
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
        second.FileKey.Should().NotBe(first.FileKey);
        second.FileKey.Should().StartWith(FileKeyFactory.GetFileKey(name));
    }

    private static async IAsyncEnumerable<T> Empty<T>()
    {
        await Task.CompletedTask;
        yield break;
    }
}
