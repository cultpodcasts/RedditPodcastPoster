using RedditPodcastPoster.EpisodeLookup;
using RedditPodcastPoster.Episodes.Matching.Candidates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Episodes;
using RedditPodcastPoster.Catalogue.Podcasts;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Quota;
using RedditPodcastPoster.Text.EliminationTerms;
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.PodcastServices.Enrichers;
using RedditPodcastPoster.PodcastServices.Updaters;

namespace RedditPodcastPoster.PodcastServices.Tests.Support;

internal sealed class PodcastUpdaterTestHarness
{
    private readonly AutoMocker _mocker = new();

    public PodcastUpdaterTestHarness(
        SaveCallRecorder? saveCallRecorder = null,
        ILogger<PodcastUpdater>? logger = null)
    {
        SaveCallRecorder = saveCallRecorder ?? new SaveCallRecorder();
        EpisodeRepository = new InMemoryEpisodeRepository(SaveCallRecorder);
        PodcastRepository = new InMemoryPodcastRepository();

        EliminationTermsProvider.Setup(x => x.GetEliminationTerms()).Returns(new EliminationTerms());
        _mocker.GetMock<IAsyncInstance<IEliminationTermsProvider>>()
            .Setup(x => x.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(EliminationTermsProvider.Object);

        HeroEpisodePromoter
            .Setup(x => x.PromoteAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        TvShows.Setup(x => x.GetTvShow(It.IsAny<Guid>())).ReturnsAsync((TvShow?)null);
        NewsOrganisations.Setup(x => x.GetNewsOrganisation(It.IsAny<Guid>())).ReturnsAsync((NewsOrganisation?)null);

        Updater = new PodcastUpdater(
            PodcastRepository,
            EpisodeRepository,
            EpisodeDomainTestServices.CreateMerger(),
            EpisodeProvider.Object,
            EpisodeEnricher.Object,
            PodcastFilter.Object,
            _mocker.GetMock<IAsyncInstance<IEliminationTermsProvider>>().Object,
            Options.Create(DefaultPostingCriteria),
            YouTubeQuotaUsageTracker.Object,
            HeroEpisodePromoter.Object,
            ShowEpisodeCounts,
            TvShows.Object,
            NewsOrganisations.Object,
            logger ?? NullLogger<PodcastUpdater>.Instance);

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

    public CatalogueShowEpisodeCounts ShowEpisodeCounts { get; } = new();

    public SaveCallRecorder SaveCallRecorder { get; }

    public InMemoryEpisodeRepository EpisodeRepository { get; }

    public InMemoryPodcastRepository PodcastRepository { get; }

    public Mock<IEpisodeProvider> EpisodeProvider => _mocker.GetMock<IEpisodeProvider>();

    public Mock<IPodcastServicesEpisodeEnricher> EpisodeEnricher =>
        _mocker.GetMock<IPodcastServicesEpisodeEnricher>();

    public Mock<IPodcastFilter> PodcastFilter => _mocker.GetMock<IPodcastFilter>();

    public Mock<IYouTubeQuotaUsageTracker> YouTubeQuotaUsageTracker =>
        _mocker.GetMock<IYouTubeQuotaUsageTracker>();

    public Mock<IHeroEpisodePromoter> HeroEpisodePromoter => _mocker.GetMock<IHeroEpisodePromoter>();

    public Mock<ITvShowRepository> TvShows => _mocker.GetMock<ITvShowRepository>();

    public Mock<INewsOrganisationRepository> NewsOrganisations =>
        _mocker.GetMock<INewsOrganisationRepository>();

    public Mock<IEliminationTermsProvider> EliminationTermsProvider =>
        _mocker.GetMock<IEliminationTermsProvider>();

    public PodcastUpdater Updater { get; }

    public static IndexingContext DefaultIndexingContext(DateTime? releasedSince = null) =>
        new(ReleasedSince: releasedSince ?? DomainTestFixture.UtcDateDaysAgo(400))
        {
            SkipShortEpisodes = false
        };
}
