using System.Linq.Expressions;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.D1Search;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using Xunit;

namespace Indexer.Tests;

/// <summary>SP-2.1 (#1321): the Api / console episode push also writes D1, and D1 never changes the Azure outcome.</summary>
public class EpisodeSearchIndexerServiceD1MirrorTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    // SearchClient is a class: AutoMocker would pick its public constructor, so the Moq protected one is used here.
    private readonly Mock<SearchClient> _searchClient = new();
    private readonly List<(D1SearchDocument[] Upserts, string[] Deletes)> _d1Writes = [];
    private readonly Podcast _podcast;
    private Episode _episode;
    private bool _d1Active = true;

    public EpisodeSearchIndexerServiceD1MirrorTests()
    {
        _podcast = _fixture.CreatePodcast(p => p.Removed = false);
        _episode = _fixture.CreateEpisode(e =>
        {
            e.PodcastId = _podcast.Id;
            e.Removed = false;
            e.ParentRemoved = false;
        });
        _mocker.Use(_searchClient);
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<Episode, bool>>>()))
            .ReturnsAsync(() => _episode);
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetPodcast(It.IsAny<Guid>()))
            .ReturnsAsync(() => _podcast);
        var d1 = _mocker.GetMock<ID1SearchIndexWriter>();
        d1.SetupGet(w => w.IsActive).Returns(() => _d1Active);
        d1.Setup(w => w.Write(It.IsAny<IReadOnlyCollection<D1SearchDocument>>(),
                It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<D1SearchDocument>, IReadOnlyCollection<string>, CancellationToken>(
                (u, d, _) => _d1Writes.Add((u.ToArray(), d.ToArray())))
            .ReturnsAsync(D1SearchWriteResult.Skipped);
    }

    [Fact(DisplayName =
        "When Azure rejects an episode push, the episode is still mirrored to D1 and the Azure failure is still " +
        "returned, because D1 mirrors Cosmos (not Azure) and must never change the Azure outcome.")]
    public async Task azure_failure_still_mirrors_and_returns_failure()
    {
        // Arrange
        _searchClient
            .Setup(s => s.MergeOrUploadDocumentsAsync(It.IsAny<IEnumerable<EpisodeSearchRecord>>(),
                It.IsAny<IndexDocumentsOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(500, _fixture.CreateTitle()));
        var sut = _mocker.CreateInstance<EpisodeSearchIndexerService>();

        // Act
        var response = await sut.IndexEpisode(_episode.Id, CancellationToken.None);

        // Assert
        response.IndexerState.Should().Be(IndexerState.Failure);
        var write = _d1Writes.Single();
        write.Upserts.Single().Id.Should().Be(_episode.Id.ToString());
        write.Upserts.Single().Description.Should().Be(_episode.Description.Trim());
    }

    [Fact(DisplayName =
        "A removed episode is deleted from D1 as it is from Azure, because both indexes must drop it.")]
    public async Task removed_episode_is_deleted_from_d1()
    {
        // Arrange
        _episode = _fixture.CreateEpisode(e =>
        {
            e.PodcastId = _podcast.Id;
            e.Removed = true;
        });
        _searchClient
            .Setup(s => s.DeleteDocumentsAsync(It.IsAny<string>(), It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexDocumentsOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(SearchModelFactory.IndexDocumentsResult([]), Mock.Of<Response>()));
        var sut = _mocker.CreateInstance<EpisodeSearchIndexerService>();

        // Act
        var response = await sut.IndexEpisodes([_episode.Id], CancellationToken.None);

        // Assert
        response.IndexerState.Should().Be(IndexerState.Executed);
        _d1Writes.Single().Deletes.Should().Equal(_episode.Id.ToString());
        _d1Writes.Single().Upserts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "With no active D1 target the push never calls the D1 writer, because a switched-off sync must do nothing.")]
    public async Task inactive_d1_is_not_called()
    {
        // Arrange
        _d1Active = false;
        _searchClient
            .Setup(s => s.MergeOrUploadDocumentsAsync(It.IsAny<IEnumerable<EpisodeSearchRecord>>(),
                It.IsAny<IndexDocumentsOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(SearchModelFactory.IndexDocumentsResult([]), Mock.Of<Response>()));
        var sut = _mocker.CreateInstance<EpisodeSearchIndexerService>();

        // Act
        await sut.IndexEpisode(_podcast, _episode, CancellationToken.None);

        // Assert
        _mocker.GetMock<ID1SearchIndexWriter>().Verify(w => w.Write(
            It.IsAny<IReadOnlyCollection<D1SearchDocument>>(), It.IsAny<IReadOnlyCollection<string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
