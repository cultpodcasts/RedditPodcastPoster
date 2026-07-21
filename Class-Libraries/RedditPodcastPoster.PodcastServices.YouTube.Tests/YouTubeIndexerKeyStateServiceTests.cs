using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Models.YouTubeQuota;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.YouTube.Strategies;
using RedditPodcastPoster.PodcastServices.Abstractions.Stores;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests;

public class YouTubeIndexerKeyStateServiceTests
{
    private static DateOnly CurrentPacificQuotaDate =>
        YouTubePacificQuotaDate.GetCurrent(DateTime.UtcNow);

    private static DateOnly PreviousPacificQuotaDate =>
        YouTubePacificQuotaDate.GetCurrent(DateTime.UtcNow.AddDays(-1));

    private static readonly DateOnly SamplePacificQuotaDate = new(2026, 6, 18);

    private static YouTubeSettings CreateProductionLikeSettings() => new()
    {
        Applications =
        [
            App("key1", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-Key-01-CultPodcasts", null),
            App("key2", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-Key-02-CultPodcasts", null),
            App("key3", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-Key-03-CultPodcasts", null),
            App("key4", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-Key-04-CultPodcasts", null),
            App("key8", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-Key-05-CultPodcasts", null),
            App("key9", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-Key-06-CultPodcasts", null),
            App("key10", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-Key-07-CultPodcasts", null),
            App("key11", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-Key-08-CultPodcasts", null),
            App("key15", "cultpodcasts", ApplicationUsage.Indexer, "Indexer-Key-09-CultPodcasts", null),
            App("key14", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-Key-10-CultPodcasts", null),
        ]
    };

    [Fact]
    public async Task ResolveSessionStartAsync_WhenSavedStateMatchesQuotaDay_ResumesAtSavedKey()
    {
        var indexerKeyStateStore = new Mock<IYouTubeIndexerKeyStateStore>();
        var strategy = CreateStrategy(CreateProductionLikeSettings(), hour: 0);
        var sut = new YouTubeIndexerKeyStateService(
            indexerKeyStateStore.Object,
            strategy,
            NullLogger<YouTubeIndexerKeyStateService>.Instance);
        indexerKeyStateStore.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new YouTubeIndexerKeyState
        {
            PacificQuotaDate = CurrentPacificQuotaDate,
            LastRingIndex = 8,
            LastApiKey = "key15",
            UpdatedUtc = DateTime.UtcNow
        });

        var session = await sut.ResolveSessionStartAsync();

        session.HourFallbackRingIndex.Should().Be(0);
        session.InitialRingIndex.Should().Be(8);
        session.Ring[session.InitialRingIndex].Application.ApiKey.Should().Be("key15");
    }

    [Fact]
    public async Task ResolveSessionStartAsync_WhenQuotaDayChanged_ResetsToHourFallback()
    {
        var indexerKeyStateStore = new Mock<IYouTubeIndexerKeyStateStore>();
        var strategy = CreateStrategy(CreateProductionLikeSettings(), hour: 0);
        var sut = new YouTubeIndexerKeyStateService(
            indexerKeyStateStore.Object,
            strategy,
            NullLogger<YouTubeIndexerKeyStateService>.Instance);

        indexerKeyStateStore.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new YouTubeIndexerKeyState
        {
            PacificQuotaDate = PreviousPacificQuotaDate,
            LastRingIndex = 8,
            LastApiKey = "key15",
            UpdatedUtc = DateTime.UtcNow
        });

        var session = await sut.ResolveSessionStartAsync();

        session.InitialRingIndex.Should().Be(0);
        session.Ring[0].Application.ApiKey.Should().Be("key1");
    }

    [Fact]
    public async Task ResolveSessionStartAsync_WhenSavedKeyMissingFromRing_FallsBackToHourFallback()
    {
        var indexerKeyStateStore = new Mock<IYouTubeIndexerKeyStateStore>();
        var strategy = CreateStrategy(CreateProductionLikeSettings(), hour: 0);
        var sut = new YouTubeIndexerKeyStateService(
            indexerKeyStateStore.Object,
            strategy,
            NullLogger<YouTubeIndexerKeyStateService>.Instance);
        indexerKeyStateStore.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new YouTubeIndexerKeyState
        {
            PacificQuotaDate = CurrentPacificQuotaDate,
            LastRingIndex = 8,
            LastApiKey = "removed-key",
            UpdatedUtc = DateTime.UtcNow
        });

        var session = await sut.ResolveSessionStartAsync();

        session.InitialRingIndex.Should().Be(0);
        session.Ring[0].Application.ApiKey.Should().Be("key1");
    }

    [Fact]
    public async Task PersistSessionEndAsync_SavesCurrentRingPosition()
    {
        var indexerKeyStateStore = new Mock<IYouTubeIndexerKeyStateStore>();
        var strategy = CreateStrategy(CreateProductionLikeSettings(), hour: 0);
        var sut = new YouTubeIndexerKeyStateService(
            indexerKeyStateStore.Object,
            strategy,
            NullLogger<YouTubeIndexerKeyStateService>.Instance);
        YouTubeIndexerKeyState? savedState = null;
        indexerKeyStateStore
            .Setup(x => x.SaveAsync(It.IsAny<YouTubeIndexerKeyState>(), It.IsAny<CancellationToken>()))
            .Callback<YouTubeIndexerKeyState, CancellationToken>((state, _) => savedState = state)
            .Returns(Task.CompletedTask);

        await sut.PersistSessionEndAsync(3, "key2");

        savedState.Should().NotBeNull();
        savedState!.LastRingIndex.Should().Be(3);
        savedState.LastApiKey.Should().Be("key2");
        savedState.PacificQuotaDate.Should().Be(CurrentPacificQuotaDate);
    }

    [Theory]
    [InlineData("key15", 8, 8)]
    [InlineData("missing-key", 8, 0)]
    public void ResolveInitialRingIndex_UsesSavedKeyWhenPresent(string savedApiKey, int savedRingIndex, int expectedIndex)
    {
        var strategy = CreateStrategy(CreateProductionLikeSettings(), hour: 0);
        var ring = strategy.BuildIndexerKeyRing(0);
        var savedState = new YouTubeIndexerKeyState
        {
            PacificQuotaDate = SamplePacificQuotaDate,
            LastRingIndex = savedRingIndex,
            LastApiKey = savedApiKey
        };

        var initialRingIndex = IndexerKeyRingSessionResolver.ResolveInitialRingIndex(
            ring,
            savedState,
            SamplePacificQuotaDate,
            hourFallbackRingIndex: 0);

        initialRingIndex.Should().Be(expectedIndex);
    }

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
