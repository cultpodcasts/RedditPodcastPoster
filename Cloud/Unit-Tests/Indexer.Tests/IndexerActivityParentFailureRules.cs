using AutoFixture;
using Azure;
using Azure.Diagnostics;
using FluentAssertions;
using Indexer.Activities;
using Indexer.Services;
using Microsoft.DurableTask;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using Xunit;

namespace Indexer.Tests;

public class IndexerActivityParentFailureRules
{
    private readonly Fixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly IndexerContextWrapper _wrapper;
    private bool _parentsUpdated;

    public IndexerActivityParentFailureRules()
    {
        _mocker.Use(Options.Create(new IndexerOptions { ReleasedDaysAgo = 1 }));
        var reason = "enabled";
        _mocker.GetMock<IActivityOptionsProvider>().Setup(x => x.RunIndex(out reason)).Returns(true);
        _mocker.GetMock<IActivityMarshaller>()
            .Setup(x => x.Initiate(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(ActivityStatus.Initiated);
        _mocker.GetMock<IActivityMarshaller>()
            .Setup(x => x.Complete(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync(ActivityStatus.Completed);
        _mocker.GetMock<IMemoryProbeOrchestrator>()
            .Setup(x => x.Start(It.IsAny<string>()))
            .Returns(() => _mocker.GetMock<IMemoryProbeScope>().Object);
        _mocker.GetMock<IPodcastsUpdater>()
            .Setup(x => x.UpdatePodcasts(It.IsAny<Guid[]>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(true);
        _mocker.GetMock<ICatalogueParentsUpdater>()
            .Setup(x => x.UpdateParents(It.IsAny<Guid[]>(), It.IsAny<IndexingContext>()))
            .Callback(() => _parentsUpdated = true)
            .ReturnsAsync(true);

        var context = new IndexerContext(
            IndexerPassOperationIds: [_fixture.Create<Guid>()],
            IndexIds: [[_fixture.Create<Guid>()]],
            CatalogueParentIndexIds: [[_fixture.Create<Guid>()]]);
        _wrapper = new IndexerContextWrapper(context, 1);
    }

    private Task<IndexerContext> Run() =>
        _mocker.CreateInstance<Activities.Indexer>().RunAsync(_mocker.GetMock<TaskActivityContext>().Object, _wrapper);

    [Fact(DisplayName =
        "Indexer pass: when the podcast batch throws, then the TV/News parents for the pass are still indexed and " +
        "the pass reports failure, because the two pipelines must fail independently.")]
    public async Task podcast_batch_exception_does_not_skip_parents()
    {
        // Arrange
        _mocker.GetMock<IPodcastsUpdater>()
            .Setup(x => x.UpdatePodcasts(It.IsAny<Guid[]>(), It.IsAny<IndexingContext>()))
            .ThrowsAsync(new InvalidOperationException("synthetic podcast failure"));

        // Act
        var result = await Run();

        // Assert
        _parentsUpdated.Should().BeTrue();
        result.Success.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Indexer pass: when the parent batch throws, then the pass completes and reports failure without " +
        "throwing, because a parent failure must not fail the hourly orchestration.")]
    public async Task parent_batch_exception_reports_failure()
    {
        // Arrange
        _mocker.GetMock<ICatalogueParentsUpdater>()
            .Setup(x => x.UpdateParents(It.IsAny<Guid[]>(), It.IsAny<IndexingContext>()))
            .ThrowsAsync(new InvalidOperationException("synthetic parent failure"));

        // Act
        var result = await Run();

        // Assert
        result.Success.Should().BeFalse();
        _mocker.GetMock<IActivityMarshaller>()
            .Verify(x => x.Complete(It.IsAny<Guid>(), It.IsAny<string>()), Times.Once);
    }

    [Fact(DisplayName =
        "Indexer pass: when podcasts and parents both succeed, then the pass reports success, because the " +
        "parent pass only adds to the podcast result.")]
    public async Task both_batches_succeed()
    {
        // Arrange
        _parentsUpdated = false;

        // Act
        var result = await Run();

        // Assert
        _parentsUpdated.Should().BeTrue();
        result.Success.Should().BeTrue();
    }
}
