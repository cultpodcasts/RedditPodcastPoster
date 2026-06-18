using FluentAssertions;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests;

public class YouTubeQuotaUsageTrackerTests
{
    private static readonly DateOnly SampleReportDate = new(2026, 6, 18);

    [Fact]
    public void CreateReport_IdentifiesSpareCapacityCandidates()
    {
        var sut = CreateTracker(
            App("key1", "CultPodcasts", "Indexer-HourPrimary-1-CultPodcasts"),
            App("key2", "CultPodcasts", "Indexer-HourPrimary-2-CultPodcasts"));
        var application = new Application
        {
            ApiKey = "key1",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Indexer-HourPrimary-1-CultPodcasts"
        };

        sut.RecordCall(application, ApplicationUsage.Indexer);
        sut.RecordQuotaHit(application, ApplicationUsage.Indexer);

        var spareApplication = new Application
        {
            ApiKey = "key2",
            Name = application.Name,
            Usage = application.Usage,
            DisplayName = "Indexer-HourPrimary-2-CultPodcasts"
        };
        sut.RecordCall(spareApplication, ApplicationUsage.Indexer);

        var report = sut.CreateReport(SampleReportDate, "Indexer");

        report.Keys.Should().HaveCount(2);
        var exhaustedKey = report.Keys.Single(x => x.DisplayName == "Indexer-HourPrimary-1-CultPodcasts");
        exhaustedKey.CapacityHint.Should().Be("quota-exhausted");
        exhaustedKey.DailyLimit.Should().Be(YouTubeQuotaCosts.DailyLimitPerKey);
        exhaustedKey.RemainingQuota.Should().Be(0);

        var spareKey = report.Keys.Single(x => x.DisplayName == "Indexer-HourPrimary-2-CultPodcasts");
        spareKey.CapacityHint.Should().Be("spare-capacity-candidate");
        spareKey.DailyLimit.Should().Be(YouTubeQuotaCosts.DailyLimitPerKey);
        spareKey.RemainingQuota.Should().Be(YouTubeQuotaCosts.DailyLimitPerKey);
        report.Id.Should().Be(YouTubeQuotaDailyReport.CreateId(SampleReportDate, "Indexer"));
    }

    [Fact]
    public void CreateReport_ComputesRemainingQuotaFromConsumedUnits()
    {
        var sut = CreateTracker(
            App("key1", "CultPodcasts", "Indexer-HourPrimary-1-CultPodcasts"),
            App("key2", "CultPodcasts", "Indexer-HourPrimary-2-CultPodcasts"));
        var application = new Application
        {
            ApiKey = "key1",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Indexer-HourPrimary-1-CultPodcasts"
        };

        sut.RecordQuotaConsumed(application, ApplicationUsage.Indexer, 2_500);

        var report = sut.CreateReport(SampleReportDate, "Indexer");

        report.Keys.Single().QuotaUsed.Should().Be(2_500);
        report.Keys.Single().RemainingQuota.Should().Be(7_500);
        report.UsedIndexerKeys.Single().QuotaUsed.Should().Be(2_500);
        report.UsedIndexerKeys.Single().RemainingQuota.Should().Be(7_500);
        report.UnusedIndexerKeys.Single().QuotaUsed.Should().Be(0);
        report.UnusedIndexerKeys.Single().RemainingQuota.Should().Be(YouTubeQuotaCosts.DailyLimitPerKey);
    }

    [Fact]
    public void CreateReport_ClampsRemainingQuotaAtZeroWhenConsumedExceedsDailyLimit()
    {
        var sut = CreateTracker(App("key1", "CultPodcasts", "Indexer-HourPrimary-1-CultPodcasts"));
        var application = new Application
        {
            ApiKey = "key1",
            Name = "CultPodcasts",
            Usage = ApplicationUsage.Indexer,
            DisplayName = "Indexer-HourPrimary-1-CultPodcasts"
        };

        sut.RecordQuotaConsumed(application, ApplicationUsage.Indexer, 12_000);

        var report = sut.CreateReport(SampleReportDate, "Indexer");

        report.Keys.Single().QuotaUsed.Should().Be(12_000);
        report.Keys.Single().RemainingQuota.Should().Be(0);
    }

    [Fact]
    public void CreateReport_SeparatesUsedAndUnusedConfiguredIndexerKeys()
    {
        var sut = CreateTracker(
            App("key1", "CultPodcasts", "Indexer-HourPrimary-1-CultPodcasts"),
            App("key2", "CultPodcasts", "Indexer-HourPrimary-2-CultPodcasts"),
            App("key3", "CultPodcasts", "Indexer-HourPrimary-3-CultPodcasts"),
            App("key4", "CultPodcasts", "Indexer-HourPrimary-4-CultPodcasts"),
            App("key8", "CultPodcasts", "Indexer-HourPrimary-1-Reattempt1-CultPodcasts", 1),
            App("key12", "CultPodcasts", "ApiKey-12 - Api", usage: ApplicationUsage.Api));

        sut.RecordCall(
            new Application
            {
                ApiKey = "key1",
                Name = "CultPodcasts",
                Usage = ApplicationUsage.Indexer,
                DisplayName = "Indexer-HourPrimary-1-CultPodcasts"
            },
            ApplicationUsage.Indexer);
        sut.RecordQuotaHit(
            new Application
            {
                ApiKey = "key8",
                Name = "CultPodcasts",
                Usage = ApplicationUsage.Indexer,
                DisplayName = "Indexer-HourPrimary-1-Reattempt1-CultPodcasts",
                Reattempt = 1
            },
            ApplicationUsage.Indexer);

        var report = sut.CreateReport(SampleReportDate, "Indexer");

        report.Keys.Should().OnlyContain(x => x.Usage == nameof(ApplicationUsage.Indexer));
        report.UsedIndexerKeys.Select(x => x.DisplayName).Should().Equal(
            "Indexer-HourPrimary-1-CultPodcasts",
            "Indexer-HourPrimary-1-Reattempt1-CultPodcasts");
        report.UnusedIndexerKeys.Select(x => x.DisplayName).Should().Equal(
            "Indexer-HourPrimary-2-CultPodcasts",
            "Indexer-HourPrimary-3-CultPodcasts",
            "Indexer-HourPrimary-4-CultPodcasts");
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
            x.DailyLimit.Should().Be(YouTubeQuotaCosts.DailyLimitPerKey);
            x.RemainingQuota.Should().Be(YouTubeQuotaCosts.DailyLimitPerKey);
        });
    }

    [Fact]
    public void CreateReport_ListsAllConfiguredIndexerKeysEvenWhenNoneWereUsed()
    {
        var sut = CreateTracker(
            App("key1", "CultPodcasts", "Indexer-HourPrimary-1-CultPodcasts"),
            App("key2", "CultPodcasts", "Indexer-HourPrimary-2-CultPodcasts"),
            App("key7", "CultPodcasts", "ApiKey-7 - Bluesky", usage: ApplicationUsage.Bluesky));

        var report = sut.CreateReport(SampleReportDate, "Indexer");

        report.UsedIndexerKeys.Should().BeEmpty();
        report.UnusedIndexerKeys.Select(x => x.DisplayName).Should().Equal(
            "Indexer-HourPrimary-1-CultPodcasts",
            "Indexer-HourPrimary-2-CultPodcasts");
        report.UnusedIndexerKeys.Should().AllSatisfy(x =>
        {
            x.RemainingQuota.Should().Be(YouTubeQuotaCosts.DailyLimitPerKey);
            x.QuotaUsed.Should().Be(0);
        });
        report.Keys.Should().BeEmpty();
    }

    [Fact]
    public void ResolveRemainingQuota_ReturnsZeroWhenQuotaHitRecorded()
    {
        YouTubeQuotaUsageTracker.ResolveRemainingQuota(500, quotaHits: 1).Should().Be(0);
    }

    private static YouTubeQuotaUsageTracker CreateTracker(params Application[] applications) =>
        new(Options.Create(new YouTubeSettings { Applications = applications }));

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
