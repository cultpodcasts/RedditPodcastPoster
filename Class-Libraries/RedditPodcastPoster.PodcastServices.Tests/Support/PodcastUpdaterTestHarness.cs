using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RedditPodcastPoster.Common.Episodes;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.Text.EliminationTerms;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.PodcastServices.Enrichers;
using RedditPodcastPoster.PodcastServices.Updaters;

namespace RedditPodcastPoster.PodcastServices.Tests.Support;

internal sealed class PodcastUpdaterTestHarness
{
    public PodcastUpdaterTestHarness(SaveCallRecorder? saveCallRecorder = null)
    {
        SaveCallRecorder = saveCallRecorder ?? new SaveCallRecorder();
        EpisodeRepository = new InMemoryEpisodeRepository(SaveCallRecorder);
        PodcastRepository = new InMemoryPodcastRepository();
        EpisodeProvider = new Mock<IEpisodeProvider>();
        EpisodeEnricher = new Mock<IPodcastServicesEpisodeEnricher>();
        PodcastFilter = new Mock<IPodcastFilter>();
        YouTubeQuotaUsageTracker = new Mock<IYouTubeQuotaUsageTracker>();
        EliminationTermsProvider = new Mock<IEliminationTermsProvider>();
        EliminationTermsProvider.Setup(x => x.GetEliminationTerms()).Returns(new EliminationTerms());

        var eliminationTermsInstance = new Mock<IAsyncInstance<IEliminationTermsProvider>>();
        eliminationTermsInstance
            .Setup(x => x.GetAsync())
            .ReturnsAsync(EliminationTermsProvider.Object);

        Updater = new PodcastUpdater(
            PodcastRepository,
            EpisodeRepository,
            EpisodeDomainTestServices.CreateMerger(),
            EpisodeProvider.Object,
            EpisodeEnricher.Object,
            PodcastFilter.Object,
            eliminationTermsInstance.Object,
            Options.Create(DefaultPostingCriteria),
            YouTubeQuotaUsageTracker.Object,
            NullLogger<PodcastUpdater>.Instance);

        EpisodeEnricher
            .Setup(x => x.EnrichEpisodes(
                It.IsAny<Podcast>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<IList<Episode>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync(new EnrichmentResults([]));

        PodcastFilter
            .Setup(x => x.Filter(
                It.IsAny<Podcast>(),
                It.IsAny<IEnumerable<Episode>>(),
                It.IsAny<List<string>>()))
            .Returns(new FilterResult([]));
    }

    public static PostingCriteria DefaultPostingCriteria { get; } = new()
    {
        MinimumDuration = TimeSpan.FromMinutes(10),
        TweetDays = 7,
        RedditDays = 7,
        BlueSkyDays = 7,
        CategoriserDays = 7
    };

    public SaveCallRecorder SaveCallRecorder { get; }

    public InMemoryEpisodeRepository EpisodeRepository { get; }

    public InMemoryPodcastRepository PodcastRepository { get; }

    public Mock<IEpisodeProvider> EpisodeProvider { get; }

    public Mock<IPodcastServicesEpisodeEnricher> EpisodeEnricher { get; }

    public Mock<IPodcastFilter> PodcastFilter { get; }

    public Mock<IYouTubeQuotaUsageTracker> YouTubeQuotaUsageTracker { get; }

    public Mock<IEliminationTermsProvider> EliminationTermsProvider { get; }

    public PodcastUpdater Updater { get; }

    public static IndexingContext DefaultIndexingContext(DateTime? releasedSince = null) =>
        new(ReleasedSince: releasedSince ?? DomainTestFixture.UtcDateDaysAgo(400))
        {
            SkipShortEpisodes = false
        };
}
