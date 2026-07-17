using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests;

public class YouTubeQuotaUsageTrackerTests
{
    private static readonly DateOnly SampleReportDate = new(2026, 6, 18);

    [Fact]
    public async Task CreateReport_IdentifiesSpareCapacityCandidates()
    {
        var sut = CreateTracker(
            App("key1", "CultPodcasts", "Indexer-Key-01-CultPodcasts"),
            App("key2", "CultPodcasts", "Indexer-Key-02-CultPodcasts"));
        var application = new Application
        {
            ApiKey = "key1",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Indexer-Key-01-CultPodcasts"
        };

        await sut.RecordCallAsync(application, ApplicationUsage.Indexer);
        await sut.RecordQuotaHitAsync(application, ApplicationUsage.Indexer, YouTubeQuotaOperation.ChannelsList);

        var spareApplication = new Application
        {
            ApiKey = "key2",
            Name = application.Name,
            Usage = application.Usage,
            DisplayName = "Indexer-Key-02-CultPodcasts"
        };
        await sut.RecordCallAsync(spareApplication, ApplicationUsage.Indexer);

        var report = await sut.CreateReportAsync(SampleReportDate, "Indexer");

        report.Keys.Should().HaveCount(2);
        var exhaustedKey = report.Keys.Single(x => x.DisplayName == "Indexer-Key-01-CultPodcasts");
        exhaustedKey.CapacityHint.Should().Be("quota-exhausted");
        exhaustedKey.DailyLimit.Should().Be(YouTubeQuotaCosts.DailyLimitPerKey);
        exhaustedKey.EstimatedQuotaUsed.Should().Be(YouTubeQuotaCosts.DailyLimitPerKey);
        exhaustedKey.QuotaConsumedByOperation.ChannelsList.Should().Be(YouTubeQuotaCosts.ChannelsList);

        var spareKey = report.Keys.Single(x => x.DisplayName == "Indexer-Key-02-CultPodcasts");
        spareKey.CapacityHint.Should().Be("spare-capacity-candidate");
        spareKey.DailyLimit.Should().Be(YouTubeQuotaCosts.DailyLimitPerKey);
        spareKey.EstimatedQuotaUsed.Should().Be(0);
        report.ReportDate.Should().Be(SampleReportDate);
        report.SourceApplication.Should().Be("Indexer");
    }

    [Fact]
    public async Task CreateReport_ComputesEstimatedQuotaUsedFromConsumedUnits()
    {
        var sut = CreateTracker(
            App("key1", "CultPodcasts", "Indexer-Key-01-CultPodcasts"),
            App("key2", "CultPodcasts", "Indexer-Key-02-CultPodcasts"));
        var application = new Application
        {
            ApiKey = "key1",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Indexer-Key-01-CultPodcasts"
        };

        await sut.RecordQuotaConsumedAsync(
            application,
            ApplicationUsage.Indexer,
            YouTubeQuotaOperation.SearchList,
            2_500);

        var report = await sut.CreateReportAsync(SampleReportDate, "Indexer");

        report.Keys.Single().QuotaUsed.Should().Be(2_500);
        report.Keys.Single().EstimatedQuotaUsed.Should().Be(2_500);
        report.Keys.Single().QuotaConsumedByOperation.SearchList.Should().Be(2_500);
        report.UsedIndexerKeys.Single().QuotaUsed.Should().Be(2_500);
        report.UsedIndexerKeys.Single().EstimatedQuotaUsed.Should().Be(2_500);
        report.UnusedIndexerKeys.Single().QuotaUsed.Should().Be(0);
        report.UnusedIndexerKeys.Single().EstimatedQuotaUsed.Should().Be(0);
    }

    [Fact]
    public async Task CreateReport_InfersFullDailyLimitWhenQuotaHitRecorded()
    {
        var sut = CreateTracker(App("key1", "CultPodcasts", "Indexer-Key-01-CultPodcasts"));
        var application = new Application
        {
            ApiKey = "key1",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Indexer-Key-01-CultPodcasts"
        };

        await sut.RecordQuotaConsumedAsync(
            application,
            ApplicationUsage.Indexer,
            YouTubeQuotaOperation.PlaylistItemsList,
            12_000);
        await sut.RecordQuotaHitAsync(application, ApplicationUsage.Indexer, YouTubeQuotaOperation.PlaylistItemsList);

        var report = await sut.CreateReportAsync(SampleReportDate, "Indexer");

        report.Keys.Single().QuotaUsed.Should().Be(12_001);
        report.Keys.Single().EstimatedQuotaUsed.Should().Be(12_001);
    }

    [Fact]
    public async Task CreateReport_SeparatesUsedAndUnusedConfiguredIndexerKeys()
    {
        var sut = CreateTracker(
            App("key1", "CultPodcasts", "Indexer-Key-01-CultPodcasts"),
            App("key2", "CultPodcasts", "Indexer-Key-02-CultPodcasts"),
            App("key3", "CultPodcasts", "Indexer-Key-03-CultPodcasts"),
            App("key4", "CultPodcasts", "Indexer-Key-04-CultPodcasts"),
            App("key8", "CultPodcasts", "Indexer-Key-05-CultPodcasts"),
            App("key12", "CultPodcasts", "ApiKey-12 - Api", usage: ApplicationUsage.Api));

        await sut.RecordCallAsync(
            new Application
            {
                ApiKey = "key1",
                Name = "CultPodcasts",
                Usage = ApplicationUsage.Indexer,
                DisplayName = "Indexer-Key-01-CultPodcasts"
            },
            ApplicationUsage.Indexer);
        await sut.RecordQuotaHitAsync(
            new Application
            {
                ApiKey = "key8",
                Name = "CultPodcasts",
                Usage = ApplicationUsage.Indexer,
                DisplayName = "Indexer-Key-05-CultPodcasts"
            },
            ApplicationUsage.Indexer,
            YouTubeQuotaOperation.SearchList);

        var report = await sut.CreateReportAsync(SampleReportDate, "Indexer");

        report.Keys.Should().OnlyContain(x => x.Usage == nameof(ApplicationUsage.Indexer));
        report.UsedIndexerKeys.Select(x => x.DisplayName).Should().Equal(
            "Indexer-Key-01-CultPodcasts",
            "Indexer-Key-05-CultPodcasts");
        report.UnusedIndexerKeys.Select(x => x.DisplayName).Should().Equal(
            "Indexer-Key-02-CultPodcasts",
            "Indexer-Key-03-CultPodcasts",
            "Indexer-Key-04-CultPodcasts");
        report.UsedIndexerKeys.Should().AllSatisfy(x =>
        {
            x.Project.Should().Be("CultPodcasts");
            x.ApiKeySuffix.Should().HaveLength(2);
            (x.CallsAttempted > 0 || x.QuotaHits > 0).Should().BeTrue();
        });
        report.UnusedIndexerKeys.Should().AllSatisfy(x =>
        {
            x.CallsAttempted.Should().Be(0);
            x.QuotaHits.Should().Be(0);
            x.QuotaUsed.Should().Be(0);
            x.EstimatedQuotaUsed.Should().Be(0);
            x.DailyLimit.Should().Be(YouTubeQuotaCosts.DailyLimitPerKey);
        });
    }

    [Fact]
    public async Task CreateReport_IncludesOperationBreakdownRingExhaustionAndNonQuotaErrors()
    {
        var sut = CreateTracker(App("key1", "CultPodcasts", "Indexer-Key-01-CultPodcasts"));
        var application = new Application
        {
            ApiKey = "key1",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Indexer-Key-01-CultPodcasts"
        };

        await sut.RecordQuotaConsumedAsync(
            application,
            ApplicationUsage.Indexer,
            YouTubeQuotaOperation.VideosList,
            YouTubeQuotaCosts.VideosList);
        await sut.RecordQuotaHitAsync(application, ApplicationUsage.Indexer, YouTubeQuotaOperation.SearchList);
        await sut.RecordRingExhaustionAsync();
        await sut.RecordNonQuotaErrorAsync();

        var report = await sut.CreateReportAsync(SampleReportDate, "Indexer");

        report.Keys.Single().QuotaConsumedByOperation.VideosList.Should().Be(YouTubeQuotaCosts.VideosList);
        report.Keys.Single().QuotaConsumedByOperation.SearchList.Should().Be(YouTubeQuotaCosts.SearchList);
        report.RingExhaustionCount.Should().Be(1);
        report.NonQuotaErrorCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateReport_ListsAllConfiguredIndexerKeysEvenWhenNoneWereUsed()
    {
        var sut = CreateTracker(
            App("key1", "CultPodcasts", "Indexer-Key-01-CultPodcasts"),
            App("key2", "CultPodcasts", "Indexer-Key-02-CultPodcasts"),
            App("key7", "CultPodcasts", "ApiKey-7 - Bluesky", usage: ApplicationUsage.Bluesky));

        var report = await sut.CreateReportAsync(SampleReportDate, "Indexer");

        report.UsedIndexerKeys.Should().BeEmpty();
        report.UnusedIndexerKeys.Select(x => x.DisplayName).Should().Equal(
            "Indexer-Key-01-CultPodcasts",
            "Indexer-Key-02-CultPodcasts");
        report.UnusedIndexerKeys.Should().AllSatisfy(x =>
        {
            x.EstimatedQuotaUsed.Should().Be(0);
            x.QuotaUsed.Should().Be(0);
        });
        report.Keys.Should().BeEmpty();
    }

    [Fact]
    public void ResolveEstimatedQuotaUsed_ReturnsDailyLimitWhenQuotaHitRecorded()
    {
        YouTubeQuotaUsageTracker.ResolveEstimatedQuotaUsed(500, quotaHits: 1).Should().Be(YouTubeQuotaCosts.DailyLimitPerKey);
    }

    [Fact]
    public void ResolveEstimatedQuotaUsed_ReturnsQuotaUsedWhenNoHits()
    {
        YouTubeQuotaUsageTracker.ResolveEstimatedQuotaUsed(500, quotaHits: 0).Should().Be(500);
    }

    [Fact]
    public async Task RecordCallAsync_DoesNotPersistUntilFlush()
    {
        YouTubeQuotaUsageState? savedState = null;
        var quotaStore = new Mock<IYouTubeQuotaUsageStateStore>();
        quotaStore.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((YouTubeQuotaUsageState?)null);
        quotaStore
            .Setup(x => x.SaveAsync(It.IsAny<YouTubeQuotaUsageState>(), It.IsAny<CancellationToken>()))
            .Callback<YouTubeQuotaUsageState, CancellationToken>((state, _) => savedState = state)
            .Returns(Task.CompletedTask);

        var application = new Application
        {
            ApiKey = "key1",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Indexer-Key-01-CultPodcasts"
        };
        var sut = CreateTracker(quotaStore.Object, App("key1", "CultPodcasts", "Indexer-Key-01-CultPodcasts"));

        await sut.RecordCallAsync(application, ApplicationUsage.Indexer);

        savedState.Should().BeNull();
    }

    [Fact]
    public async Task FlushToCosmosAsync_PersistsUsageStateAfterRecord()
    {
        YouTubeQuotaUsageState? savedState = null;
        var quotaStore = new Mock<IYouTubeQuotaUsageStateStore>();
        quotaStore.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((YouTubeQuotaUsageState?)null);
        quotaStore
            .Setup(x => x.SaveAsync(It.IsAny<YouTubeQuotaUsageState>(), It.IsAny<CancellationToken>()))
            .Callback<YouTubeQuotaUsageState, CancellationToken>((state, _) => savedState = state)
            .Returns(Task.CompletedTask);

        var application = new Application
        {
            ApiKey = "key1",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Indexer-Key-01-CultPodcasts"
        };
        var sut = CreateTracker(quotaStore.Object, App("key1", "CultPodcasts", "Indexer-Key-01-CultPodcasts"));

        await sut.RecordCallAsync(application, ApplicationUsage.Indexer);
        await sut.FlushToCosmosAsync();

        savedState.Should().NotBeNull();
        savedState!.Entries.Should().ContainSingle();
        savedState.Entries.Single().CallsAttempted.Should().Be(1);
        savedState.Entries.Single().StatsKey.Should().Be("Indexer:key1");
    }

    [Fact]
    public async Task FlushToCosmosAsync_MergesSessionUsageWithExistingCosmosState()
    {
        var pacificQuotaDate = YouTubePacificQuotaDate.GetCurrent(DateTime.UtcNow);
        YouTubeQuotaUsageState? savedState = null;
        var quotaStore = new Mock<IYouTubeQuotaUsageStateStore>();
        quotaStore.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new YouTubeQuotaUsageState
        {
            PacificQuotaDate = pacificQuotaDate,
            SourceApplication = "Indexer",
            UpdatedUtc = DateTime.UtcNow,
            Entries =
            [
                new YouTubeQuotaUsageEntry
                {
                    StatsKey = "Indexer:key1",
                    DisplayName = "Indexer-Key-01-CultPodcasts",
                    Project = "CultPodcasts",
                    Usage = nameof(ApplicationUsage.Indexer),
                    CallsAttempted = 3,
                    QuotaHits = 0,
                    QuotaUsed = 0
                }
            ]
        });
        quotaStore
            .Setup(x => x.SaveAsync(It.IsAny<YouTubeQuotaUsageState>(), It.IsAny<CancellationToken>()))
            .Callback<YouTubeQuotaUsageState, CancellationToken>((state, _) => savedState = state)
            .Returns(Task.CompletedTask);

        var application = new Application
        {
            ApiKey = "key1",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Indexer-Key-01-CultPodcasts"
        };
        var sut = CreateTracker(quotaStore.Object, App("key1", "CultPodcasts", "Indexer-Key-01-CultPodcasts"));

        await sut.RecordCallAsync(application, ApplicationUsage.Indexer);
        await sut.RecordCallAsync(application, ApplicationUsage.Indexer);
        await sut.FlushToCosmosAsync();

        savedState.Should().NotBeNull();
        savedState!.Entries.Should().ContainSingle();
        savedState.Entries.Single().CallsAttempted.Should().Be(5);
    }

    [Fact]
    public async Task CreateReportAsync_HydratesPersistedUsageStateOnColdStart()
    {
        var pacificQuotaDate = YouTubePacificQuotaDate.GetCurrent(DateTime.UtcNow);
        var quotaStore = new Mock<IYouTubeQuotaUsageStateStore>();
        quotaStore.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new YouTubeQuotaUsageState
        {
            PacificQuotaDate = pacificQuotaDate,
            SourceApplication = "Indexer",
            UpdatedUtc = DateTime.UtcNow,
            Entries =
            [
                new YouTubeQuotaUsageEntry
                {
                    StatsKey = "Indexer:key1",
                    DisplayName = "Indexer-Key-01-CultPodcasts",
                    Project = "CultPodcasts",
                    Usage = nameof(ApplicationUsage.Indexer),
                    CallsAttempted = 3,
                    QuotaHits = 1,
                    QuotaUsed = 12
                }
            ]
        });
        quotaStore
            .Setup(x => x.SaveAsync(It.IsAny<YouTubeQuotaUsageState>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateTracker(
            quotaStore.Object,
            App("key1", "CultPodcasts", "Indexer-Key-01-CultPodcasts"),
            App("key2", "CultPodcasts", "Indexer-Key-02-CultPodcasts"));

        var report = await sut.CreateReportAsync(pacificQuotaDate, "Indexer");

        report.Keys.Should().ContainSingle();
        report.Keys.Single().QuotaHits.Should().Be(1);
        report.UsedIndexerKeys.Should().ContainSingle(x => x.DisplayName == "Indexer-Key-01-CultPodcasts");
        report.UnusedIndexerKeys.Should().ContainSingle(x => x.DisplayName == "Indexer-Key-02-CultPodcasts");
    }

    [Fact]
    public async Task CreateReportAsync_ReflectsInMemoryUsageBeforeFlush()
    {
        var sut = CreateTracker(App("key1", "CultPodcasts", "Indexer-Key-01-CultPodcasts"));
        var application = new Application
        {
            ApiKey = "key1",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Indexer-Key-01-CultPodcasts"
        };

        await sut.RecordCallAsync(application, ApplicationUsage.Indexer);
        await sut.RecordQuotaConsumedAsync(
            application,
            ApplicationUsage.Indexer,
            YouTubeQuotaOperation.ChannelsList,
            100);

        var report = await sut.CreateReportAsync(SampleReportDate, "Indexer");

        report.Keys.Single().CallsAttempted.Should().Be(1);
        report.Keys.Single().QuotaUsed.Should().Be(100);
        report.Keys.Single().QuotaConsumedByOperation.ChannelsList.Should().Be(100);
    }

    [Fact]
    public async Task CreateReport_IncludesPodcastQuotaSkipCounts()
    {
        var sut = CreateTracker(App("key1", "CultPodcasts", "Indexer-Key-01-CultPodcasts"));

        await sut.RecordPodcastNotIndexedDueToQuotaAsync();
        await sut.RecordPodcastNotIndexedDueToQuotaAsync();
        await sut.RecordPodcastNotEnrichedDueToQuotaAsync();

        var report = await sut.CreateReportAsync(SampleReportDate, "Indexer");

        report.PodcastsNotIndexedDueToQuota.Should().Be(2);
        report.PodcastsNotEnrichedDueToQuota.Should().Be(1);
    }

    [Fact]
    public async Task FlushToCosmosAsync_PersistsPodcastQuotaSkipCounts()
    {
        YouTubeQuotaUsageState? savedState = null;
        var quotaStore = new Mock<IYouTubeQuotaUsageStateStore>();
        quotaStore.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((YouTubeQuotaUsageState?)null);
        quotaStore
            .Setup(x => x.SaveAsync(It.IsAny<YouTubeQuotaUsageState>(), It.IsAny<CancellationToken>()))
            .Callback<YouTubeQuotaUsageState, CancellationToken>((state, _) => savedState = state)
            .Returns(Task.CompletedTask);

        var sut = CreateTracker(quotaStore.Object, App("key1", "CultPodcasts", "Indexer-Key-01-CultPodcasts"));

        await sut.RecordPodcastNotIndexedDueToQuotaAsync();
        await sut.RecordPodcastNotEnrichedDueToQuotaAsync();
        await sut.FlushToCosmosAsync();

        savedState.Should().NotBeNull();
        savedState!.PodcastsNotIndexedDueToQuota.Should().Be(1);
        savedState.PodcastsNotEnrichedDueToQuota.Should().Be(1);
    }

    [Fact]
    public async Task CreateReportAsync_HydratesPodcastQuotaSkipCountsOnColdStart()
    {
        var pacificQuotaDate = YouTubePacificQuotaDate.GetCurrent(DateTime.UtcNow);
        var quotaStore = new Mock<IYouTubeQuotaUsageStateStore>();
        quotaStore.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new YouTubeQuotaUsageState
        {
            PacificQuotaDate = pacificQuotaDate,
            SourceApplication = "Indexer",
            UpdatedUtc = DateTime.UtcNow,
            PodcastsNotIndexedDueToQuota = 4,
            PodcastsNotEnrichedDueToQuota = 7,
            Entries = []
        });
        quotaStore
            .Setup(x => x.SaveAsync(It.IsAny<YouTubeQuotaUsageState>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateTracker(
            quotaStore.Object,
            App("key1", "CultPodcasts", "Indexer-Key-01-CultPodcasts"));

        await sut.RecordPodcastNotIndexedDueToQuotaAsync();
        var report = await sut.CreateReportAsync(pacificQuotaDate, "Indexer");

        report.PodcastsNotIndexedDueToQuota.Should().Be(5);
        report.PodcastsNotEnrichedDueToQuota.Should().Be(7);
    }

    [Fact]
    public async Task ResetAsync_ClearsPodcastQuotaSkipCounts()
    {
        YouTubeQuotaUsageState? savedState = null;
        var quotaStore = new Mock<IYouTubeQuotaUsageStateStore>();
        quotaStore.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((YouTubeQuotaUsageState?)null);
        quotaStore
            .Setup(x => x.SaveAsync(It.IsAny<YouTubeQuotaUsageState>(), It.IsAny<CancellationToken>()))
            .Callback<YouTubeQuotaUsageState, CancellationToken>((state, _) => savedState = state)
            .Returns(Task.CompletedTask);

        var sut = CreateTracker(quotaStore.Object, App("key1", "CultPodcasts", "Indexer-Key-01-CultPodcasts"));

        await sut.RecordPodcastNotIndexedDueToQuotaAsync();
        await sut.ResetAsync();

        savedState.Should().NotBeNull();
        savedState!.PodcastsNotIndexedDueToQuota.Should().Be(0);
        savedState.PodcastsNotEnrichedDueToQuota.Should().Be(0);

        var report = await sut.CreateReportAsync(SampleReportDate, "Indexer");
        report.PodcastsNotIndexedDueToQuota.Should().Be(0);
        report.PodcastsNotEnrichedDueToQuota.Should().Be(0);
    }

    private static YouTubeQuotaUsageTracker CreateTracker(params Application[] applications) =>
        CreateTracker(CreateQuotaUsageStateStore().Object, applications);

    private static YouTubeQuotaUsageTracker CreateTracker(
        IYouTubeQuotaUsageStateStore quotaUsageStateStore,
        params Application[] applications) =>
        new(
            Options.Create(new YouTubeSettings { Applications = applications }),
            quotaUsageStateStore,
            NullLogger<YouTubeQuotaUsageTracker>.Instance);

    private static Mock<IYouTubeQuotaUsageStateStore> CreateQuotaUsageStateStore()
    {
        var store = new Mock<IYouTubeQuotaUsageStateStore>();
        store.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync((YouTubeQuotaUsageState?)null);
        store
            .Setup(x => x.SaveAsync(It.IsAny<YouTubeQuotaUsageState>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return store;
    }

    private static Application App(
        string apiKey,
        string name,
        string displayName,
        int? reattempt = null,
        ApplicationUsage usage = ApplicationUsage.Indexer) =>
        new()
        {
            ApiKey = apiKey,
            Name = name,
            Usage = usage,
            DisplayName = displayName,
            Reattempt = reattempt
        };
}
