using Api.Models;
using Api.Services;
using Api.Services.Podcasts;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.Indexing.Models;
using RedditPodcastPoster.Indexing.Services;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Podcasts;

public class PodcastIndexServiceBacklogRules
{
    private readonly AutoMocker _mocker = new();
    private IndexingContext? _context;

    public PodcastIndexServiceBacklogRules()
    {
        _mocker.Use(Options.Create(new IndexerOptions { ReleasedDaysAgo = 2 }));
        _mocker.GetMock<IIndexer>()
            .Setup(x => x.Index(It.IsAny<string>(), It.IsAny<IndexingContext>()))
            .Callback<string, IndexingContext>((_, context) => _context = context)
            .ReturnsAsync(new IndexResponse(IndexStatus.Performed, []));
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
}
