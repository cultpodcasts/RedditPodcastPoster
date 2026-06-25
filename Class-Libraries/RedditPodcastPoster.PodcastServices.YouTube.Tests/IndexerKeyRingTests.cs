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
            App("key1", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-Key-01-CultPodcasts", null),
            App("key2", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-Key-02-CultPodcasts", null),
            App("key3", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-Key-03-CultPodcasts", null),
            App("key4", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-Key-04-CultPodcasts", null),
            App("key13discover", "cultpodcasts", ApplicationUsage.Discover, "ApiKey-13 - Discover", null),
            App("key13discover2", "cultpodcasts", ApplicationUsage.Discover, "ApiKey-13 - Discover backup", null),
            App("key7", "CultPodcasts", ApplicationUsage.Bluesky, "ApiKey-7 - Bluesky", null),
            App("key8", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-Key-05-CultPodcasts", null),
            App("key9", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-Key-06-CultPodcasts", null),
            App("key10", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-Key-07-CultPodcasts", null),
            App("key11", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-Key-08-CultPodcasts", null),
            App("key12", "CultPodcasts", ApplicationUsage.Api, "ApiKey-12 - Api", null),
            App("key15", "cultpodcasts", ApplicationUsage.Indexer, "Indexer-Key-09-CultPodcasts", null),
            App("key14", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-Key-10-CultPodcasts", null),
            App("key16", "cultpodcasts", ApplicationUsage.Indexer, "Indexer-Key-11-CultPodcasts", null),
            App("key14", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-Key-12-CultPodcasts", null),
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
    public void BuildIndexerKeyRing_ReturnsFlatConfigOrderWhenStartIndexZero()
    {
        var sut = CreateStrategy(CreateProductionLikeSettings(), hour: 0);

        var ring = sut.BuildIndexerKeyRing(startRingIndex: 0);

        ring.Select(x => x.Application.DisplayName).Should().Equal(
            "Indexer-Key-01-CultPodcasts",
            "Indexer-Key-02-CultPodcasts",
            "Indexer-Key-03-CultPodcasts",
            "Indexer-Key-04-CultPodcasts",
            "Indexer-Key-05-CultPodcasts",
            "Indexer-Key-06-CultPodcasts",
            "Indexer-Key-07-CultPodcasts",
            "Indexer-Key-08-CultPodcasts",
            "Indexer-Key-09-CultPodcasts",
            "Indexer-Key-10-CultPodcasts",
            "Indexer-Key-11-CultPodcasts");
    }

    [Fact]
    public void BuildIndexerKeyRing_DeduplicatesPhysicalApiKeys()
    {
        var sut = CreateStrategy(CreateProductionLikeSettings(), hour: 0);

        var ring = sut.BuildIndexerKeyRing(startRingIndex: 0);

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

        var ring = sut.BuildIndexerKeyRing(startRingIndex: 0);

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
    public void BuildIndexerKeyRing_RotatesFromStartRingIndex()
    {
        var sut = CreateStrategy(CreateProductionLikeSettings(), hour: 12);

        var ring = sut.BuildIndexerKeyRing(startRingIndex: 4);

        ring.First().Application.DisplayName.Should().Be("Indexer-Key-05-CultPodcasts");
    }

    [Fact]
    public void GetApplication_ForIndexer_UsesHourFallbackSpread()
    {
        var sut = CreateStrategy(CreateProductionLikeSettings(), hour: 18);

        var application = sut.GetApplication(ApplicationUsage.Indexer);

        application.Application.DisplayName.Should().Be("Indexer-Key-09-CultPodcasts");
        application.Index.Should().Be(8);
    }

    [Fact]
    public void GetHourFallbackRingIndex_CoversAllRingPositionsAcrossUtcDay()
    {
        const int ringCount = 11;
        var seen = new HashSet<int>();

        for (var hour = 0; hour < 24; hour++)
        {
            seen.Add(IndexerKeyRingBuilder.GetHourFallbackRingIndex(hour, ringCount));
        }

        seen.Should().HaveCount(ringCount);
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
