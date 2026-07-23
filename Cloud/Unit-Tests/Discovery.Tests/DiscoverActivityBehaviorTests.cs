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

    private readonly Guid _operationId = Guid.NewGuid();
    private readonly Mock<IDiscoveryLookbackResolver> _lookback = new(MockBehavior.Strict);
    private readonly Mock<IActivityMarshaller> _marshaller = new(MockBehavior.Strict);
    private readonly Mock<IDiscoveryServiceConfigProvider> _configProvider = new(MockBehavior.Strict);
    private readonly Mock<IDiscoveryService> _discoveryService = new(MockBehavior.Strict);
    private readonly Mock<IDiscoveryResultsRepository> _repo = new(MockBehavior.Strict);
    private readonly Mock<IDiscoveryInfoContentPublisher> _publisher = new(MockBehavior.Strict);
    private readonly Mock<INotificationPublisher> _notifications = new(MockBehavior.Strict);
    private readonly Discover _sut;
    private readonly TaskActivityContext _activityContext =
        Mock.Of<TaskActivityContext>(c => c.InstanceId == "test");

    public DiscoverActivityBehaviorTests()
    {
        var memory = new Mock<IMemoryProbeOrchestrator>();
        memory.Setup(m => m.Start(It.IsAny<string>())).Returns(Mock.Of<IMemoryProbeScope>());

        _sut = new Discover(
            Options.Create(new DiscoverOptions
            {
                IncludeYouTube = true,
                DynamicLookbackOverlap = TimeSpan.FromMinutes(10)
            }),
            memory.Object,
            _lookback.Object,
            _configProvider.Object,
            _discoveryService.Object,
            _repo.Object,
            _notifications.Object,
            _publisher.Object,
            _marshaller.Object,
            NullLogger<Discover>.Instance);
    }

    [Fact(DisplayName =
        "Discover happy path: lookback since drives discovery service and results are saved once")]
    public async Task Discover_happy_path_uses_lookback_calls_service_and_saves()
    {
        ArrangeSuccessfulLookback();
        ArrangeMarshallerInitiated();
        ArrangeDiscoveryConfig();
        ArrangeDiscoveryResults([new DiscoveryResult { EpisodeName = "ep", ShowName = "show", Released = Since }]);

        DiscoveryResultsDocument? saved = null;
        _repo.Setup(r => r.Save(It.IsAny<DiscoveryResultsDocument>()))
            .Callback<DiscoveryResultsDocument>(d => saved = d)
            .Returns(Task.CompletedTask);

        _publisher.Setup(p => p.PublishUnprocessedSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoveryInfo { DocumentCount = 0 });

        var outcome = await _sut.RunAsync(_activityContext, new DiscoveryContext(_operationId));

        outcome.Success.Should().BeTrue();
        outcome.DuplicateDiscoveryOperation.Should().BeNull();
        saved.Should().NotBeNull();
        saved!.DiscoveryResults.Should().ContainSingle(r => r.EpisodeName == "ep");
        _lookback.Verify(r => r.ResolveAsync(It.IsAny<CancellationToken>()), Times.Once);
        _discoveryService.VerifyAll();
        _repo.Verify(r => r.Save(It.IsAny<DiscoveryResultsDocument>()), Times.Once);
        _marshaller.Verify(m => m.Complete(_operationId, nameof(Discover)), Times.Once);
        _notifications.VerifyNoOtherCalls();
    }

    [Fact(DisplayName =
        "Discover fail-closed: lookback unavailable skips providers and persist")]
    public async Task Discover_lookback_unavailable_fails_closed_without_providers_or_save()
    {
        _lookback.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DiscoveryLookbackUnavailableException("fail-closed"));

        var act = async () => await _sut.RunAsync(_activityContext, new DiscoveryContext(_operationId));

        await act.Should().ThrowAsync<DiscoveryLookbackUnavailableException>();
        _marshaller.VerifyNoOtherCalls();
        _discoveryService.VerifyNoOtherCalls();
        _repo.VerifyNoOtherCalls();
        _configProvider.VerifyNoOtherCalls();
    }

    [Fact(DisplayName =
        "Discover duplicate operation skips providers and persist")]
    public async Task Discover_duplicate_operation_skips_providers_and_persist()
    {
        ArrangeSuccessfulLookback();
        _marshaller.Setup(m => m.Initiate(_operationId, nameof(Discover)))
            .ReturnsAsync(ActivityStatus.AlreadyInitiated);

        var outcome = await _sut.RunAsync(_activityContext, new DiscoveryContext(_operationId));

        outcome.DuplicateDiscoveryOperation.Should().BeTrue();
        outcome.Success.Should().BeNull();
        _discoveryService.VerifyNoOtherCalls();
        _repo.VerifyNoOtherCalls();
        _configProvider.VerifyNoOtherCalls();
        _marshaller.Verify(m => m.Complete(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
    }

    [Fact(DisplayName =
        "Discover save failure surfaces as DiscoveryOrchestrationIncompleteException")]
    public async Task Discover_save_failure_surfaces_as_incomplete()
    {
        ArrangeSuccessfulLookback();
        ArrangeMarshallerInitiated();
        ArrangeDiscoveryConfig();
        ArrangeDiscoveryResults([]);

        _repo.Setup(r => r.Save(It.IsAny<DiscoveryResultsDocument>()))
            .ThrowsAsync(new InvalidOperationException("cosmos down"));

        var act = async () => await _sut.RunAsync(_activityContext, new DiscoveryContext(_operationId));

        var ex = await act.Should().ThrowAsync<DiscoveryOrchestrationIncompleteException>();
        ex.Which.InnerException.Should().BeOfType<InvalidOperationException>();
        _marshaller.Verify(m => m.Complete(_operationId, nameof(Discover)), Times.Once);
        _publisher.VerifyNoOtherCalls();
    }

    private void ArrangeSuccessfulLookback()
    {
        _lookback.Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoveryLookbackResolution(Since, LatestSuccess));
    }

    private void ArrangeMarshallerInitiated()
    {
        _marshaller.Setup(m => m.Initiate(_operationId, nameof(Discover)))
            .ReturnsAsync(ActivityStatus.Initiated);
        _marshaller.Setup(m => m.Complete(_operationId, nameof(Discover)))
            .ReturnsAsync(ActivityStatus.Completed);
    }

    private void ArrangeDiscoveryConfig()
    {
        _configProvider
            .Setup(c => c.CreateDiscoveryConfig(It.IsAny<GetServiceConfigOptions>()))
            .Returns(new DiscoveryConfig(Since, null, [], false, false));
    }

    private void ArrangeDiscoveryResults(IEnumerable<DiscoveryResult> results)
    {
        _discoveryService
            .Setup(s => s.GetDiscoveryResults(
                It.Is<DiscoveryConfig>(cfg => cfg.Since == Since),
                It.Is<IndexingContext>(ctx => ctx.ReleasedSince == Since)))
            .Returns(ToAsync(results));
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
