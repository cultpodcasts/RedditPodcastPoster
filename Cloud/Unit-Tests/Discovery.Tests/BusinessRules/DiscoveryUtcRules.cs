using Azure;
using Azure.Diagnostics;
using Discovery.Activities;
using Discovery.Models;
using Discovery.Services;
using FluentAssertions;
using Microsoft.DurableTask;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.ContentPublisher.Models;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.Discovery.Models;
using RedditPodcastPoster.Discovery.Providers;
using RedditPodcastPoster.Discovery.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Episodes.TestSupport.TimeZones;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Serialization;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PushSubscriptions.Models;
using RedditPodcastPoster.PushSubscriptions.Publishers;
using Xunit;

namespace Discovery.Tests.BusinessRules;

/// <summary>
/// Discovery lookback, Discover activity, and push-notification copy treat unspecified-kind times as UTC (#1219).
/// <see cref="DateTime.ToUniversalTime"/> would shift them on a host east of UTC. CI is UTC, so
/// <see cref="DiscoveryUtcEastOfUtcHostGuard"/> re-runs this class under TZ=Pacific/Auckland.
/// </summary>
public class DiscoveryUtcRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private static readonly TimeSpan Overlap10m = TimeSpan.FromMinutes(10);
    private static readonly TimeZoneInfo Uk = TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");

    public DiscoveryUtcRules()
    {
        _mocker.Use(Options.Create(new DiscoverOptions
        {
            IncludeYouTube = true,
            DynamicLookbackOverlap = Overlap10m
        }));
        _mocker.Use<ILogger<DiscoveryLookbackResolver>>(NullLogger<DiscoveryLookbackResolver>.Instance);
        _mocker.Use<ILogger<Discover>>(NullLogger<Discover>.Instance);
        _mocker.GetMock<IMemoryProbeOrchestrator>()
            .Setup(m => m.Start(It.IsAny<string>()))
            .Returns(Mock.Of<IMemoryProbeScope>());
    }

    private DateTime CreateUnspecifiedUtcInstant() =>
        DateTime.SpecifyKind(
            DomainTestFixture.UtcAtTime(-(_fixture.Create<int>() % 300 + 2), _fixture.CreateNonMidnightTimeOfDay()),
            DateTimeKind.Unspecified);

    [Fact(DisplayName =
        "When the latest successful DiscoveryBegan is unspecified-kind, then ResolveSince subtracts the overlap from that same UTC instant, because an unspecified watermark is UTC and ToUniversalTime on a host east of UTC would move since by the host's offset.")]
    public void lookback_calculator_reads_unspecified_watermark_as_utc()
    {
        // Arrange
        var latest = CreateUnspecifiedUtcInstant();

        // Act
        var since = DiscoveryLookbackCalculator.ResolveSince(latest, Overlap10m);

        // Assert
        since.Should().Be(UtcDateTimes.ToUtc(latest).Subtract(Overlap10m));
        since.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact(DisplayName =
        "When Cosmos returns an unspecified-kind DiscoveryBegan watermark, then the resolver's since and latest success are that same UTC instant minus overlap, because the watermark is UTC and ToUniversalTime on a host east of UTC would shift both values.")]
    public async Task lookback_resolver_reads_unspecified_watermark_as_utc()
    {
        // Arrange
        var latest = CreateUnspecifiedUtcInstant();
        _mocker.GetMock<IDiscoveryResultsRepository>()
            .Setup(r => r.GetLatestDiscoveryBegan(It.IsAny<CancellationToken>()))
            .ReturnsAsync(latest);
        var sut = _mocker.CreateInstance<DiscoveryLookbackResolver>();

        // Act
        var result = await sut.ResolveAsync();

        // Assert
        result.LatestSuccessfulDiscoveryBegan.Should().Be(UtcDateTimes.ToUtc(latest));
        result.Since.Should().Be(UtcDateTimes.ToUtc(latest).Subtract(Overlap10m));
    }

    [Fact(DisplayName =
        "When lookback returns an unspecified-kind since, then Discover passes that same UTC instant as IndexingContext.ReleasedSince, because the activity normalises unspecified as UTC and ToUniversalTime on a host east of UTC would shift the discovery window.")]
    public async Task discover_activity_reads_unspecified_since_as_utc()
    {
        // Arrange
        var sinceUnspecified = CreateUnspecifiedUtcInstant();
        var sinceUtc = UtcDateTimes.ToUtc(sinceUnspecified);
        var latestUtc = sinceUtc.AddMinutes(10);
        var operationId = _fixture.CreateGuid();
        IndexingContext? capturedContext = null;

        _mocker.GetMock<IDiscoveryLookbackResolver>()
            .Setup(r => r.ResolveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoveryLookbackResolution(sinceUnspecified, latestUtc));

        _mocker.GetMock<IActivityMarshaller>()
            .Setup(m => m.Initiate(operationId, nameof(Discover)))
            .ReturnsAsync(ActivityStatus.Initiated);
        _mocker.GetMock<IActivityMarshaller>()
            .Setup(m => m.Complete(operationId, nameof(Discover)))
            .ReturnsAsync(ActivityStatus.Completed);

        _mocker.GetMock<IDiscoveryServiceConfigProvider>()
            .Setup(c => c.CreateDiscoveryConfig(It.IsAny<GetServiceConfigOptions>()))
            .Returns(new DiscoveryConfig(sinceUtc, null, [], false, false));

        _mocker.GetMock<IDiscoveryService>()
            .Setup(s => s.GetDiscoveryResults(It.IsAny<DiscoveryConfig>(), It.IsAny<IndexingContext>()))
            .Callback<DiscoveryConfig, IndexingContext>((_, ctx) => capturedContext = ctx)
            .Returns(EmptyAsync());

        _mocker.GetMock<IDiscoveryResultsRepository>()
            .Setup(r => r.Save(It.IsAny<DiscoveryResultsDocument>()))
            .Returns(Task.CompletedTask);

        _mocker.GetMock<IDiscoveryInfoContentPublisher>()
            .Setup(p => p.PublishUnprocessedSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoveryInfo { DocumentCount = 0 });

        var sut = _mocker.CreateInstance<Discover>();

        // Act
        var outcome = await sut.RunAsync(
            Mock.Of<TaskActivityContext>(c => c.InstanceId == "test"),
            new DiscoveryContext(operationId));

        // Assert
        outcome.Success.Should().BeTrue();
        capturedContext.Should().NotBeNull();
        capturedContext!.ReleasedSince.Should().Be(sinceUtc);
        capturedContext.ReleasedSince!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact(DisplayName =
        "When a discovery notification's MinDateTime is unspecified-kind, then ToString formats that same UTC instant in UK local time, because an unspecified min time is UTC and ToUniversalTime on a host east of UTC would shift the displayed clock.")]
    public void discovery_notification_reads_unspecified_min_as_utc()
    {
        // Arrange
        // Keep the span under one day so the format is time-only ("t"), which still moves when the instant is shifted.
        var minRecent = DateTime.SpecifyKind(
            DateTime.UtcNow.AddHours(-(_fixture.Create<int>() % 5 + 1)).AddMinutes(_fixture.Create<int>() % 50 + 1),
            DateTimeKind.Unspecified);
        var notification = new DiscoveryNotification(1, minRecent, 1);
        var expectedUk = TimeZoneInfo.ConvertTimeFromUtc(UtcDateTimes.ToUtc(minRecent), Uk).ToString("t");

        // Act
        var text = notification.ToString();

        // Assert
        text.Should().Be($"1 report since {expectedUk}. 1 result.");
    }

    [DiscoveryUtcGuardChildFact(DisplayName =
        "When this class runs in the guard's child test host, then the host really is on Pacific/Auckland time, because a missing tzdata would silently fall back to UTC and every rule would pass without proving anything.")]
    public void child_host_is_east_of_utc()
    {
        // Arrange
        var local = TimeZoneInfo.Local;

        // Act
        var standardOffset = local.BaseUtcOffset;

        // Assert
        standardOffset.Should().Be(TimeSpan.FromHours(12),
            "TZ={0} should give NZST; local zone was '{1}'", EastOfUtcChildTestHost.EastOfUtcZone, local.Id);
    }

    private static async IAsyncEnumerable<DiscoveryResult> EmptyAsync()
    {
        await Task.CompletedTask;
        yield break;
    }
}
