using Azure;
using Azure.Diagnostics;
using Discovery.Activities;
using Discovery.Models;
using Discovery.Orchestrations;
using Discovery.Services;
using FluentAssertions;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.Discovery.Models;
using RedditPodcastPoster.Discovery.Providers;
using RedditPodcastPoster.Discovery.Services;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PushSubscriptions.Publishers;
using Xunit;

namespace Discovery.Tests;

/// <summary>
/// Discover activity journey: lookback → activity book → discovery service → persist.
/// Provider I/O is mocked; dedupe remains covered by library unit tests (not on this path).
/// </summary>
public class DiscoverActivityBehaviorTests
{
    private static readonly DateTime Since = new(2026, 7, 22, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime LatestSuccess = new(2026, 7, 22, 10, 10, 0, DateTimeKind.Utc);

    [Fact(DisplayName =
        "Discover happy path: lookback since drives discovery service and results are saved once")]
    public async Task Discover_happy_path_uses_lookback_calls_service_and_saves()
    {
        var operationId = Guid.NewGuid();
        var lookback = new Mock<IDiscoveryLookbackResolver>(MockBehavior.Strict);
        lookback.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoveryLookbackResolution(Since, LatestSuccess));

        var marshaller = new Mock<IActivityMarshaller>(MockBehavior.Strict);
        marshaller.Setup(m => m.Initiate(operationId, nameof(Discover)))
            .ReturnsAsync(ActivityStatus.Initiated);
        marshaller.Setup(m => m.Complete(operationId, nameof(Discover)))
            .ReturnsAsync(ActivityStatus.Completed);

        var configProvider = new Mock<IDiscoveryServiceConfigProvider>(MockBehavior.Strict);
        var discoveryConfig = new DiscoveryConfig(Since, null, [], false, false);
        configProvider
            .Setup(c => c.CreateDiscoveryConfig(It.IsAny<GetServiceConfigOptions>()))
            .Returns(discoveryConfig);

        var result = new DiscoveryResult { EpisodeName = "ep", ShowName = "show", Released = Since };
        var discoveryService = new Mock<IDiscoveryService>(MockBehavior.Strict);
        discoveryService
            .Setup(s => s.GetDiscoveryResults(
                It.Is<DiscoveryConfig>(cfg => cfg.Since == Since),
                It.Is<IndexingContext>(ctx => ctx.ReleasedSince == Since)))
            .Returns(ToAsync([result]));

        DiscoveryResultsDocument? saved = null;
        var repo = new Mock<IDiscoveryResultsRepository>(MockBehavior.Strict);
        repo.Setup(r => r.Save(It.IsAny<DiscoveryResultsDocument>()))
            .Callback<DiscoveryResultsDocument>(d => saved = d)
            .Returns(Task.CompletedTask);

        var publisher = new Mock<IDiscoveryInfoContentPublisher>(MockBehavior.Strict);
        publisher.Setup(p => p.PublishUnprocessedSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoveryInfo { DocumentCount = 0 });

        var notifications = new Mock<INotificationPublisher>(MockBehavior.Strict);

        var activity = CreateSut(
            lookback.Object,
            marshaller.Object,
            configProvider.Object,
            discoveryService.Object,
            repo.Object,
            publisher.Object,
            notifications.Object);

        var outcome = await activity.RunAsync(
            Mock.Of<TaskActivityContext>(c => c.InstanceId == "test"),
            new DiscoveryContext(operationId));

        outcome.Success.Should().BeTrue();
        outcome.DuplicateDiscoveryOperation.Should().BeNull();
        saved.Should().NotBeNull();
        saved!.DiscoveryResults.Should().ContainSingle(r => r.EpisodeName == "ep");
        lookback.Verify(r => r.ResolveAsync(It.IsAny<CancellationToken>()), Times.Once);
        discoveryService.VerifyAll();
        repo.Verify(r => r.Save(It.IsAny<DiscoveryResultsDocument>()), Times.Once);
        marshaller.Verify(m => m.Complete(operationId, nameof(Discover)), Times.Once);
        notifications.VerifyNoOtherCalls();
    }

    [Fact(DisplayName =
        "Discover fail-closed: lookback unavailable skips providers and persist")]
    public async Task Discover_lookback_unavailable_fails_closed_without_providers_or_save()
    {
        var lookback = new Mock<IDiscoveryLookbackResolver>(MockBehavior.Strict);
        lookback.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DiscoveryLookbackUnavailableException("fail-closed"));

        var marshaller = new Mock<IActivityMarshaller>(MockBehavior.Strict);
        var discoveryService = new Mock<IDiscoveryService>(MockBehavior.Strict);
        var repo = new Mock<IDiscoveryResultsRepository>(MockBehavior.Strict);
        var configProvider = new Mock<IDiscoveryServiceConfigProvider>(MockBehavior.Strict);
        var publisher = new Mock<IDiscoveryInfoContentPublisher>(MockBehavior.Strict);
        var notifications = new Mock<INotificationPublisher>(MockBehavior.Strict);

        var activity = CreateSut(
            lookback.Object,
            marshaller.Object,
            configProvider.Object,
            discoveryService.Object,
            repo.Object,
            publisher.Object,
            notifications.Object);

        var act = async () => await activity.RunAsync(
            Mock.Of<TaskActivityContext>(c => c.InstanceId == "test"),
            new DiscoveryContext(Guid.NewGuid()));

        await act.Should().ThrowAsync<DiscoveryLookbackUnavailableException>();
        marshaller.VerifyNoOtherCalls();
        discoveryService.VerifyNoOtherCalls();
        repo.VerifyNoOtherCalls();
        configProvider.VerifyNoOtherCalls();
    }

    [Fact(DisplayName =
        "Discover duplicate operation skips providers and persist")]
    public async Task Discover_duplicate_operation_skips_providers_and_persist()
    {
        var operationId = Guid.NewGuid();
        var lookback = new Mock<IDiscoveryLookbackResolver>(MockBehavior.Strict);
        lookback.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoveryLookbackResolution(Since, LatestSuccess));

        var marshaller = new Mock<IActivityMarshaller>(MockBehavior.Strict);
        marshaller.Setup(m => m.Initiate(operationId, nameof(Discover)))
            .ReturnsAsync(ActivityStatus.AlreadyInitiated);

        var discoveryService = new Mock<IDiscoveryService>(MockBehavior.Strict);
        var repo = new Mock<IDiscoveryResultsRepository>(MockBehavior.Strict);
        var configProvider = new Mock<IDiscoveryServiceConfigProvider>(MockBehavior.Strict);
        var publisher = new Mock<IDiscoveryInfoContentPublisher>(MockBehavior.Strict);
        var notifications = new Mock<INotificationPublisher>(MockBehavior.Strict);

        var activity = CreateSut(
            lookback.Object,
            marshaller.Object,
            configProvider.Object,
            discoveryService.Object,
            repo.Object,
            publisher.Object,
            notifications.Object);

        var outcome = await activity.RunAsync(
            Mock.Of<TaskActivityContext>(c => c.InstanceId == "test"),
            new DiscoveryContext(operationId));

        outcome.DuplicateDiscoveryOperation.Should().BeTrue();
        outcome.Success.Should().BeNull();
        discoveryService.VerifyNoOtherCalls();
        repo.VerifyNoOtherCalls();
        configProvider.VerifyNoOtherCalls();
        marshaller.Verify(m => m.Complete(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName =
        "Discover save failure surfaces as DiscoveryOrchestrationIncompleteException")]
    public async Task Discover_save_failure_surfaces_as_incomplete()
    {
        var operationId = Guid.NewGuid();
        var lookback = new Mock<IDiscoveryLookbackResolver>(MockBehavior.Strict);
        lookback.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoveryLookbackResolution(Since, LatestSuccess));

        var marshaller = new Mock<IActivityMarshaller>(MockBehavior.Strict);
        marshaller.Setup(m => m.Initiate(operationId, nameof(Discover)))
            .ReturnsAsync(ActivityStatus.Initiated);
        marshaller.Setup(m => m.Complete(operationId, nameof(Discover)))
            .ReturnsAsync(ActivityStatus.Completed);

        var configProvider = new Mock<IDiscoveryServiceConfigProvider>(MockBehavior.Strict);
        configProvider
            .Setup(c => c.CreateDiscoveryConfig(It.IsAny<GetServiceConfigOptions>()))
            .Returns(new DiscoveryConfig(Since, null, [], false, false));

        var discoveryService = new Mock<IDiscoveryService>(MockBehavior.Strict);
        discoveryService
            .Setup(s => s.GetDiscoveryResults(It.IsAny<DiscoveryConfig>(), It.IsAny<IndexingContext>()))
            .Returns(ToAsync(Array.Empty<DiscoveryResult>()));

        var repo = new Mock<IDiscoveryResultsRepository>(MockBehavior.Strict);
        repo.Setup(r => r.Save(It.IsAny<DiscoveryResultsDocument>()))
            .ThrowsAsync(new InvalidOperationException("cosmos down"));

        var publisher = new Mock<IDiscoveryInfoContentPublisher>(MockBehavior.Strict);
        var notifications = new Mock<INotificationPublisher>(MockBehavior.Strict);

        var activity = CreateSut(
            lookback.Object,
            marshaller.Object,
            configProvider.Object,
            discoveryService.Object,
            repo.Object,
            publisher.Object,
            notifications.Object);

        var act = async () => await activity.RunAsync(
            Mock.Of<TaskActivityContext>(c => c.InstanceId == "test"),
            new DiscoveryContext(operationId));

        var ex = await act.Should().ThrowAsync<DiscoveryOrchestrationIncompleteException>();
        ex.Which.InnerException.Should().BeOfType<InvalidOperationException>();
        marshaller.Verify(m => m.Complete(operationId, nameof(Discover)), Times.Once);
        publisher.VerifyNoOtherCalls();
    }

    private static Discover CreateSut(
        IDiscoveryLookbackResolver lookback,
        IActivityMarshaller marshaller,
        IDiscoveryServiceConfigProvider configProvider,
        IDiscoveryService discoveryService,
        IDiscoveryResultsRepository repo,
        IDiscoveryInfoContentPublisher publisher,
        INotificationPublisher notifications)
    {
        var memory = new Mock<IMemoryProbeOrchestrator>();
        memory.Setup(m => m.Start(It.IsAny<string>())).Returns(Mock.Of<IMemoryProbeScope>());

        return new Discover(
            Options.Create(new DiscoverOptions
            {
                IncludeYouTube = true,
                DynamicLookbackOverlap = TimeSpan.FromMinutes(10)
            }),
            memory.Object,
            lookback,
            configProvider,
            discoveryService,
            repo,
            notifications,
            publisher,
            marshaller,
            NullLogger<Discover>.Instance);
    }

    private static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}
