using System.Net;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Persistence.Repositories;

namespace RedditPodcastPoster.Persistence.Tests;

public class FilmRepositoryDeleteRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private static CosmosException CosmosStatus(HttpStatusCode status) => new("synthetic", status, 0, string.Empty, 0);

    [Fact(DisplayName =
        "Film delete removes the item by id with the id as partition key, because films are partitioned by their own id.")]
    public async Task delete_uses_id_as_partition_key()
    {
        // Arrange
        var filmId = _fixture.CreateGuid();
        var sut = _mocker.CreateInstance<FilmRepository>();

        // Act
        await sut.Delete(filmId);

        // Assert
        _mocker.GetMock<Container>().Verify(x => x.DeleteItemAsync<Film>(
            filmId.ToString(),
            new PartitionKey(filmId.ToString()),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "Film delete of a film that is already gone succeeds, because rollback deletes must be idempotent.")]
    public async Task delete_of_missing_film_is_swallowed()
    {
        // Arrange
        _mocker.GetMock<Container>()
            .Setup(x => x.DeleteItemAsync<Film>(It.IsAny<string>(), It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CosmosStatus(HttpStatusCode.NotFound));
        var sut = _mocker.CreateInstance<FilmRepository>();

        // Act
        var act = () => sut.Delete(_fixture.CreateGuid());

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact(DisplayName =
        "Film delete surfaces any other Cosmos failure, because only not-found is safe to ignore.")]
    public async Task delete_failure_other_than_not_found_propagates()
    {
        // Arrange
        _mocker.GetMock<Container>()
            .Setup(x => x.DeleteItemAsync<Film>(It.IsAny<string>(), It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CosmosStatus(HttpStatusCode.TooManyRequests));
        var sut = _mocker.CreateInstance<FilmRepository>();

        // Act
        var act = () => sut.Delete(_fixture.CreateGuid());

        // Assert
        (await act.Should().ThrowAsync<CosmosException>()).Which.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
