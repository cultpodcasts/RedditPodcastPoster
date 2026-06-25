using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Strategies;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests;

public class IndexerKeyRingTests
{
    private static YouTubeSettings CreateProductionLikeSettings() => new()
    {
        Applications =
        [
            App("key0", "CultPodcasts", ApplicationUsage.Cli, "ApiKey-0 - Cli", null),
            App("key1", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-1-CultPodcasts", null),
            App("key2", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-2-CultPodcasts", null),
            App("key3", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-3-CultPodcasts", null),
            App("key4", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-4-CultPodcasts", null),
            App("key13discover", "cultcodcasts", ApplicationUsage.Discover, "ApiKey-13 - Discover", null),
            App("key13discover2", "cultcodcasts", ApplicationUsage.Discover, "ApiKey-13 - Discover backup", null),
            App("key7", "CultPodcasts", ApplicationUsage.Bluesky, "ApiKey-7 - Bluesky", null),
            App("key8", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-1-Reattempt1-CultPodcasts", 1),
            App("key9", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-2-Reattempt1-CultPodcasts", 1),
            App("key10", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-3-Reattempt1-CultPodcasts", 1),
            App("key11", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-4-Reattempt1-CultPodcasts", 1),
            App("key12", "CultPodcasts", ApplicationUsage.Api, "ApiKey-12 - Api", null),
            App("key15", "cultpodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-1-Reattempt2-CultPodcasts", 2),
            App("key14", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-2-Reattempt2-CultPodcasts", 2),
            App("key16", "cultpodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-3-Reattempt2-CultPodcasts", 2),
            App("key14", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-4-Reattempt2-CultPodcasts", 2),
        ]
    };

    private static Application App(
        string apiKey,
        string name,
        ApplicationUsage usage,
        string displayName,
        int? reattempt) =>
        new()
        {
            ApiKey = apiKey,
            Name = name,
            Usage = usage,
            DisplayName = displayName,
            Reattempt = reattempt
        };

    [Fact]
    public void BuildIndexerKeyRing_StartsAtSelectedPrimaryAndWalksAllHourPrimaries()
    {
        var sut = CreateStrategy(CreateProductionLikeSettings(), hour: 0);

        var ring = sut.BuildIndexerKeyRing(startPrimaryIndex: 0);

        ring.Select(x => x.Application.DisplayName).Should().Equal(
            "Indexer-HourPrimary-1-CultPodcasts",
            "Indexer-HourPrimary-1-Reattempt1-CultPodcasts",
            "Indexer-HourPrimary-1-Reattempt2-CultPodcasts",
            "Indexer-HourPrimary-2-CultPodcasts",
            "Indexer-HourPrimary-2-Reattempt1-CultPodcasts",
            "Indexer-HourPrimary-2-Reattempt2-CultPodcasts",
            "Indexer-HourPrimary-3-CultPodcasts",
            "Indexer-HourPrimary-3-Reattempt1-CultPodcasts",
            "Indexer-HourPrimary-3-Reattempt2-CultPodcasts",
            "Indexer-HourPrimary-4-CultPodcasts",
            "Indexer-HourPrimary-4-Reattempt1-CultPodcasts");
    }

    [Fact]
    public void BuildIndexerKeyRing_DeduplicatesPhysicalApiKeys()
    {
        var sut = CreateStrategy(CreateProductionLikeSettings(), hour: 0);

        var ring = sut.BuildIndexerKeyRing(startPrimaryIndex: 0);

        ring.Select(x => x.Application.ApiKey).Should().OnlyHaveUniqueItems();
        ring.Should().HaveCount(11);
        ring.Count(x => x.Application.ApiKey == "key15").Should().Be(1);
        ring.Count(x => x.Application.ApiKey == "key16").Should().Be(1);
        ring.Count(x => x.Application.ApiKey == "key14").Should().Be(1);
    }

    [Fact]
    public void BuildIndexerKeyRing_ExcludesDiscoverApiBlueskyAndCliKeys()
    {
        var sut = CreateStrategy(CreateProductionLikeSettings(), hour: 0);

        var ring = sut.BuildIndexerKeyRing(startPrimaryIndex: 0);

        ring.Select(x => x.Application.ApiKey).Should().NotContain(["key0", "key7", "key12", "key13discover"]);
        ring.Select(x => x.Application.Usage).Should().AllBeEquivalentTo(ApplicationUsage.Indexer);
    }

    [Fact]
    public void GetApplication_ForIndexer_DoesNotReturnDiscoverOrApiKeys()
    {
        var sut = CreateStrategy(CreateProductionLikeSettings(), hour: 0);

        var application = sut.GetApplication(ApplicationUsage.Indexer);

        application.Application.Usage.Should().Be(ApplicationUsage.Indexer);
        application.Application.ApiKey.Should().Be("key1");
    }

    [Fact]
    public void BuildIndexerKeyRing_RotatesStartingPrimaryByHour()
    {
        var sut = CreateStrategy(CreateProductionLikeSettings(), hour: 12);

        var application = sut.GetApplication(ApplicationUsage.Indexer);
        var ring = sut.BuildIndexerKeyRing(application.Index);

        ring.First().Application.DisplayName.Should().Be("Indexer-HourPrimary-3-CultPodcasts");
    }

    private static YouTubeApiKeyStrategy CreateStrategy(YouTubeSettings settings, int hour)
    {
        var dateTimeService = new Mock<IDateTimeService>();
        dateTimeService.Setup(x => x.GetHour()).Returns(hour);
        return new YouTubeApiKeyStrategy(
            Options.Create(settings),
            dateTimeService.Object,
            NullLogger<YouTubeApiKeyStrategy>.Instance);
    }
}
