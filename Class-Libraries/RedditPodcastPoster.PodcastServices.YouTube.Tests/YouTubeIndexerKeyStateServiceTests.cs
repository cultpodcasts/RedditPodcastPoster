using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.PodcastServices.YouTube.Strategies;

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
            App("key1", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-1-CultPodcasts", null),
            App("key2", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-2-CultPodcasts", null),
            App("key3", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-3-CultPodcasts", null),
            App("key4", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-4-CultPodcasts", null),
            App("key8", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-1-Reattempt1-CultPodcasts", 1),
            App("key9", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-2-Reattempt1-CultPodcasts", 1),
            App("key10", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-3-Reattempt1-CultPodcasts", 1),
            App("key11", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-4-Reattempt1-CultPodcasts", 1),
            App("key15", "cultpodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-1-Reattempt2-CultPodcasts", 2),
            App("key14", "CultPodcasts", ApplicationUsage.Indexer, "Indexer-HourPrimary-2-Reattempt2-CultPodcasts", 2),
        ]
    };

    [Fact]
    public async Task ResolveSessionStartAsync_WhenSavedStateMatchesQuotaDay_ResumesAtSavedKey()
    {
        var lookupRepository = new Mock<ILookupRepository>();
        var strategy = CreateStrategy(CreateProductionLikeSettings(), hour: 0);
        var sut = new YouTubeIndexerKeyStateService(
            lookupRepository.Object,
            strategy,
            NullLogger<YouTubeIndexerKeyStateService>.Instance);
        lookupRepository.Setup(x => x.GetYouTubeIndexerKeyState()).ReturnsAsync(new YouTubeIndexerKeyState
        {
            PacificQuotaDate = CurrentPacificQuotaDate,
            LastRingIndex = 2,
            LastApiKey = "key15",
            UpdatedUtc = DateTime.UtcNow
        });

        var session = await sut.ResolveSessionStartAsync();

        session.StartPrimaryIndex.Should().Be(0);
        session.InitialRingIndex.Should().Be(2);
        session.Ring[session.InitialRingIndex].Application.ApiKey.Should().Be("key15");
    }

    [Fact]
    public async Task ResolveSessionStartAsync_WhenQuotaDayChanged_ResetsToHourPrimary()
    {
        var lookupRepository = new Mock<ILookupRepository>();
        var strategy = CreateStrategy(CreateProductionLikeSettings(), hour: 0);
        var sut = new YouTubeIndexerKeyStateService(
            lookupRepository.Object,
            strategy,
            NullLogger<YouTubeIndexerKeyStateService>.Instance);

        lookupRepository.Setup(x => x.GetYouTubeIndexerKeyState()).ReturnsAsync(new YouTubeIndexerKeyState
        {
            PacificQuotaDate = PreviousPacificQuotaDate,
            LastRingIndex = 2,
            LastApiKey = "key15",
            UpdatedUtc = DateTime.UtcNow
        });

        var session = await sut.ResolveSessionStartAsync();

        session.InitialRingIndex.Should().Be(0);
        session.Ring[0].Application.ApiKey.Should().Be("key1");
    }

    [Fact]
    public async Task ResolveSessionStartAsync_WhenSavedKeyMissingFromRing_FallsBackToHourPrimary()
    {
        var lookupRepository = new Mock<ILookupRepository>();
        var strategy = CreateStrategy(CreateProductionLikeSettings(), hour: 0);
        var sut = new YouTubeIndexerKeyStateService(
            lookupRepository.Object,
            strategy,
            NullLogger<YouTubeIndexerKeyStateService>.Instance);
        lookupRepository.Setup(x => x.GetYouTubeIndexerKeyState()).ReturnsAsync(new YouTubeIndexerKeyState
        {
            PacificQuotaDate = CurrentPacificQuotaDate,
            LastRingIndex = 2,
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
        var lookupRepository = new Mock<ILookupRepository>();
        var strategy = CreateStrategy(CreateProductionLikeSettings(), hour: 0);
        var sut = new YouTubeIndexerKeyStateService(
            lookupRepository.Object,
            strategy,
            NullLogger<YouTubeIndexerKeyStateService>.Instance);
        YouTubeIndexerKeyState? savedState = null;
        lookupRepository
            .Setup(x => x.SaveYouTubeIndexerKeyState(It.IsAny<YouTubeIndexerKeyState>()))
            .Callback<YouTubeIndexerKeyState>(state => savedState = state)
            .Returns(Task.CompletedTask);

        await sut.PersistSessionEndAsync(3, "key2");

        savedState.Should().NotBeNull();
        savedState!.LastRingIndex.Should().Be(3);
        savedState.LastApiKey.Should().Be("key2");
        savedState.PacificQuotaDate.Should().Be(CurrentPacificQuotaDate);
    }

    [Theory]
    [InlineData("key15", 2, 2)]
    [InlineData("missing-key", 2, 0)]
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
            SamplePacificQuotaDate);

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
