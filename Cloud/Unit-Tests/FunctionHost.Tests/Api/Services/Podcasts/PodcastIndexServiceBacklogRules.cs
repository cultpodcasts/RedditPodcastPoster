using Api.Models;
using Api.Services;
using Api.Services.Podcasts;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Indexing.Models;
using RedditPodcastPoster.Indexing.Services;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Podcasts;

public class PodcastIndexServiceBacklogRules
{
    private readonly AutoMocker _mocker = new();
    private readonly DomainTestFixture _fixture = new();
    private IndexingContext? _context;
    private IndexPreviewResponse _preview = new(IndexStatus.Performed);

    public PodcastIndexServiceBacklogRules()
    {
        _mocker.Use(Options.Create(new IndexerOptions { ReleasedDaysAgo = 2 }));
        _mocker.GetMock<IIndexer>()
            .Setup(x => x.Index(It.IsAny<string>(), It.IsAny<IndexingContext>()))
            .Callback<string, IndexingContext>((_, context) => _context = context)
            .ReturnsAsync(new IndexResponse(IndexStatus.Performed, []));
        _mocker.GetMock<IIndexer>()
            .Setup(x => x.Preview(It.IsAny<string>(), It.IsAny<IndexingContext>()))
            .Callback<string, IndexingContext>((_, context) => _context = context)
            .ReturnsAsync(() => _preview);
    }

    [Fact(DisplayName =
        "IndexAsync keeps ReleasedSince from ReleasedDaysAgo so hourly-style curator lookback is unchanged.")]
    public async Task default_index_keeps_released_since()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PodcastIndexService>();

        // Act
        var result = await sut.IndexAsync("synthetic-podcast", CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastIndexStatus.Ok);
        _context.Should().NotBeNull();
        _context!.ReleasedSince.Should().NotBeNull();
    }

    [Fact(DisplayName =
        "IndexBacklogAsync sets ReleasedSince to UnixEpoch so PodcastUpdater accepts the context while the walk is not capped at the two-day hub Index window.")]
    public async Task backlog_index_uses_unix_epoch_released_since()
    {
        // Arrange
        var sut = _mocker.CreateInstance<PodcastIndexService>();

        // Act
        var result = await sut.IndexBacklogAsync("synthetic-podcast", CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastIndexStatus.Ok);
        _context.Should().NotBeNull();
        _context!.ReleasedSince.Should().Be(CuratorIndexingContext.BacklogReleasedSince);
    }

    [Fact(DisplayName =
        "PreviewBacklogAsync previews with the backlog UnixEpoch ReleasedSince and runs neither the real index nor the search indexer, because the confirmation must count the same walk without ingesting it.")]
    public async Task preview_uses_the_backlog_window_and_never_indexes()
    {
        // Arrange
        var podcastName = _fixture.CreateTitle();
        var sut = _mocker.CreateInstance<PodcastIndexService>();

        // Act
        await sut.PreviewBacklogAsync(podcastName, CancellationToken.None);

        // Assert
        _context.Should().NotBeNull();
        _context!.ReleasedSince.Should().Be(CuratorIndexingContext.BacklogReleasedSince);
        _mocker.GetMock<IIndexer>().Verify(
            x => x.Preview(podcastName, It.IsAny<IndexingContext>()),
            Times.Once);
        _mocker.GetMock<IIndexer>().Verify(
            x => x.Index(It.IsAny<string>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "PreviewBacklogAsync answers Ok carrying the indexer's counts when the preview is Performed, because the handler reports those counts to the admin.")]
    public async Task performed_preview_is_ok_with_counts()
    {
        // Arrange
        _preview = new IndexPreviewResponse(IndexStatus.Performed, _fixture.Create<int>() % 500 + 1, 2);
        var sut = _mocker.CreateInstance<PodcastIndexService>();

        // Act
        var result = await sut.PreviewBacklogAsync(_fixture.CreateTitle(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastIndexStatus.Ok);
        result.Preview.Should().Be(_preview);
    }

    [Theory(DisplayName =
        "PreviewBacklogAsync maps NotFound, TimedOut and NotPerformed indexer statuses to NotFound, TimedOut and BadRequest, because they mirror the real backlog index.")]
    [InlineData(IndexStatus.NotFound, PodcastIndexStatus.NotFound)]
    [InlineData(IndexStatus.TimedOut, PodcastIndexStatus.TimedOut)]
    [InlineData(IndexStatus.NotPerformed, PodcastIndexStatus.BadRequest)]
    public async Task non_performed_preview_statuses_map_like_index(
        IndexStatus indexStatus,
        PodcastIndexStatus expected)
    {
        // Arrange
        _preview = new IndexPreviewResponse(indexStatus);
        var sut = _mocker.CreateInstance<PodcastIndexService>();

        // Act
        var result = await sut.PreviewBacklogAsync(_fixture.CreateTitle(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(expected);
    }

    [Fact(DisplayName =
        "PreviewBacklogAsync answers Failed when the indexer throws, because a faulty preview must not look like a count.")]
    public async Task preview_exception_is_failed()
    {
        // Arrange
        _mocker.GetMock<IIndexer>()
            .Setup(x => x.Preview(It.IsAny<string>(), It.IsAny<IndexingContext>()))
            .ThrowsAsync(new InvalidOperationException(_fixture.CreateTitle()));
        var sut = _mocker.CreateInstance<PodcastIndexService>();

        // Act
        var result = await sut.PreviewBacklogAsync(_fixture.CreateTitle(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastIndexStatus.Failed);
        result.Preview.Should().BeNull();
    }}
