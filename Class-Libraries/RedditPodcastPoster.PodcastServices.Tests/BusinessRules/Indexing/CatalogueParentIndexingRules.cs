using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Episodes;
using RedditPodcastPoster.Catalogue.Podcasts;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Tests.Support;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.Text.EliminationTerms;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

public class CatalogueParentIndexingRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(60);
    private static readonly TimeSpan PostableDuration =
        PodcastUpdaterTestHarness.DefaultPostingCriteria.MinimumDuration + TimeSpan.FromMinutes(5);

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<TvShowEpisode> _savedTvEpisodes = [];
    private readonly List<NewsReport> _savedReports = [];
    private readonly List<TvShow> _savedShows = [];
    private readonly List<NewsOrganisation> _savedOrganisations = [];
    private readonly Dictionary<Guid, TvShow> _shows = [];
    private readonly Dictionary<Guid, NewsOrganisation> _organisations = [];
    private List<TvShowEpisode> _storedTvEpisodes = [];
    private List<NewsReport> _storedReports = [];
    private IList<Episode> _discovered = [];
    private IList<Episode>? _existingPassedToProvider;
    private Func<IList<Episode>, IList<Episode>, EpisodeMergeResult> _merge =
        (_, added) => new EpisodeMergeResult(added, added, [], []);
    private Func<IEnumerable<Episode>, FilterResult> _filter = _ => new FilterResult([]);

    public CatalogueParentIndexingRules()
    {
        _mocker.Use(Options.Create(PodcastUpdaterTestHarness.DefaultPostingCriteria));
        _mocker.Use<TimeProvider>(TimeProvider.System);

        var tvShows = _mocker.GetMock<ITvShowRepository>();
        tvShows.Setup(x => x.GetTvShow(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _shows.GetValueOrDefault(id));
        tvShows.Setup(x => x.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns(() => _shows.Values.ToAsyncEnumerable());
        tvShows.Setup(x => x.Save(It.IsAny<TvShow>()))
            .Callback((TvShow show) => _savedShows.Add(show))
            .Returns(Task.CompletedTask);

        var organisations = _mocker.GetMock<INewsOrganisationRepository>();
        organisations.Setup(x => x.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _organisations.GetValueOrDefault(id));
        organisations.Setup(x => x.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns(() => _organisations.Values.ToAsyncEnumerable());
        organisations.Setup(x => x.Save(It.IsAny<NewsOrganisation>()))
            .Callback((NewsOrganisation organisation) => _savedOrganisations.Add(organisation))
            .Returns(Task.CompletedTask);

        var tvEpisodes = _mocker.GetMock<ITvShowEpisodeRepository>();
        tvEpisodes.Setup(x => x.GetByTvShowId(It.IsAny<Guid>(), It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .Returns((Guid id, Expression<Func<TvShowEpisode, bool>> _) =>
                _storedTvEpisodes.Where(x => x.TvShowId == id).ToAsyncEnumerable());
        tvEpisodes.Setup(x => x.Save(It.IsAny<IEnumerable<TvShowEpisode>>()))
            .Callback((IEnumerable<TvShowEpisode> episodes) => _savedTvEpisodes.AddRange(episodes))
            .Returns(Task.CompletedTask);

        var reports = _mocker.GetMock<INewsReportRepository>();
        reports.Setup(x => x.GetByNewsOrganisationId(It.IsAny<Guid>(), It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .Returns((Guid id, Expression<Func<NewsReport, bool>> _) =>
                _storedReports.Where(x => x.NewsOrganisationId == id).ToAsyncEnumerable());
        reports.Setup(x => x.Save(It.IsAny<IEnumerable<NewsReport>>()))
            .Callback((IEnumerable<NewsReport> items) => _savedReports.AddRange(items))
            .Returns(Task.CompletedTask);

        _mocker.GetMock<IEpisodeProvider>()
            .Setup(x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync((Podcast _, IEnumerable<Episode> existing, IndexingContext _) =>
            {
                _existingPassedToProvider = existing.ToList();
                return _discovered.ToList();
            });
        _mocker.GetMock<IEpisodeMerger>()
            .Setup(x => x.MergeEpisodes(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IEnumerable<Episode>>()))
            .Returns((Podcast _, IEnumerable<Episode> existing, IEnumerable<Episode> incoming) =>
                _merge(existing.ToList(), incoming.ToList()));
        _mocker.GetMock<IPodcastFilter>()
            .Setup(x => x.Filter(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<List<string>>()))
            .Returns((Podcast _, IEnumerable<Episode> episodes, List<string> _) => _filter(episodes));

        var terms = new Mock<IEliminationTermsProvider>();
        terms.Setup(x => x.GetEliminationTerms()).Returns(new EliminationTerms());
        _mocker.GetMock<IAsyncInstance<IEliminationTermsProvider>>()
            .Setup(x => x.GetAsync())
            .ReturnsAsync(terms.Object);
    }

    private CatalogueParentsUpdater Sut => _mocker.CreateInstance<CatalogueParentsUpdater>();

    private static IndexingContext Context() => new(ReleasedSince);

    private Podcast AutoIndexedYouTubePodcast() =>
        _fixture.CreateYouTubeReleaseAuthorityPodcast(_fixture.CreateYouTubeChannelId(), 0) is var podcast
            ? WithIndexAll(podcast)
            : throw new InvalidOperationException();

    private static Podcast WithIndexAll(Podcast podcast)
    {
        podcast.IndexAllEpisodes = true;
        return podcast;
    }

    private TvShow SeedTvShow(Podcast podcast)
    {
        var show = CatalogueParentKindMapper.ToTvShow(podcast);
        _shows[show.Id] = show;
        return show;
    }

    private NewsOrganisation SeedOrganisation(Podcast podcast)
    {
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);
        _organisations[organisation.Id] = organisation;
        return organisation;
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when an auto-indexed TV show has a new YouTube video, then it is saved as a " +
        "TvShowEpisode under that show with the show's name and LastIndexed is set, because migrated TV shows must " +
        "keep getting new episodes.")]
    public async Task new_video_on_tv_show_is_saved_as_tv_show_episode()
    {
        // Arrange
        var show = SeedTvShow(AutoIndexedYouTubePodcast());
        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(PostableDuration));
        _discovered = [discovered];

        // Act
        var success = await Sut.UpdateParents([show.Id], Context());

        // Assert
        success.Should().BeTrue();
        _savedTvEpisodes.Should().ContainSingle();
        var saved = _savedTvEpisodes.Single();
        saved.Id.Should().Be(discovered.Id);
        saved.TvShowId.Should().Be(show.Id);
        saved.TvShowName.Should().Be(show.Name);
        saved.Title.Should().Be(discovered.Title);
        saved.Services.Should().ContainKey(ServiceKeys.YouTube);
        _savedShows.Should().ContainSingle().Which.LastIndexed.Should().NotBeNull();
        show.LatestReleased.Should().Be(discovered.ReleaseUtc);
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when an auto-indexed news organisation has a new video, then it is saved as a " +
        "NewsReport under that organisation, because migrated news channels must keep getting new reports.")]
    public async Task new_video_on_news_organisation_is_saved_as_news_report()
    {
        // Arrange
        var organisation = SeedOrganisation(AutoIndexedYouTubePodcast());
        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b => b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(PostableDuration));
        _discovered = [discovered];

        // Act
        var success = await Sut.UpdateParents([organisation.Id], Context());

        // Assert
        success.Should().BeTrue();
        var saved = _savedReports.Should().ContainSingle().Subject;
        saved.Id.Should().Be(discovered.Id);
        saved.NewsOrganisationId.Should().Be(organisation.Id);
        saved.NewsOrganisationName.Should().Be(organisation.Name);
        _savedOrganisations.Should().ContainSingle();
        _savedTvEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when a parent is not auto-indexed (no indexing settings, or neither index-all " +
        "nor an include-title regex), then nothing is discovered or saved, because only auto-indexed podcasts were " +
        "indexed before migration.")]
    public async Task parent_without_auto_index_is_skipped()
    {
        // Arrange
        var notAutoIndexed = _fixture.CreateYouTubeReleaseAuthorityPodcast(_fixture.CreateYouTubeChannelId(), 0);
        notAutoIndexed.IndexAllEpisodes = false;
        notAutoIndexed.EpisodeIncludeTitleRegex = string.Empty;
        var show = SeedTvShow(notAutoIndexed);
        var bare = SeedOrganisation(AutoIndexedYouTubePodcast());
        bare.Indexing = null;
        _discovered = [_fixture.CreateYouTubeCatalogueEpisode()];

        // Act
        var success = await Sut.UpdateParents([show.Id, bare.Id], Context());

        // Assert
        success.Should().BeTrue();
        _existingPassedToProvider.Should().BeNull();
        _savedTvEpisodes.Should().BeEmpty();
        _savedReports.Should().BeEmpty();
        _savedShows.Should().BeEmpty();
        _savedOrganisations.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when a parent is removed, then it is not indexed, because removed podcasts are " +
        "not indexed either.")]
    public async Task removed_parent_is_skipped()
    {
        // Arrange
        var show = SeedTvShow(AutoIndexedYouTubePodcast());
        show.Removed = true;
        _discovered = [_fixture.CreateYouTubeCatalogueEpisode()];

        // Act
        await Sut.UpdateParents([show.Id], Context());

        // Assert
        _existingPassedToProvider.Should().BeNull();
        _savedTvEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: stored playables are passed to discovery with their YouTube id read from the " +
        "services URL, because playables store no provider ids and the merger matches on them.")]
    public async Task stored_playables_carry_provider_ids_from_services()
    {
        // Arrange
        var podcast = AutoIndexedYouTubePodcast();
        var show = SeedTvShow(podcast);
        var storedEpisode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, release: DomainTestFixture.UtcDateDaysAgo(5));
        _storedTvEpisodes = [CatalogueParentKindMapper.ToTvShowEpisode(storedEpisode, show)];

        // Act
        await Sut.UpdateParents([show.Id], Context());

        // Assert
        var passed = _existingPassedToProvider.Should().ContainSingle().Subject;
        passed.Id.Should().Be(storedEpisode.Id);
        passed.PodcastId.Should().Be(show.Id);
        passed.Ids!.YouTube.Should().Be(storedEpisode.Ids!.YouTube);
        _savedTvEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when elimination terms remove a stored playable, then that playable is saved " +
        "as removed, because term filtering applies to every kind the indexer reads.")]
    public async Task term_filtered_playable_is_saved_removed()
    {
        // Arrange
        var podcast = AutoIndexedYouTubePodcast();
        var organisation = SeedOrganisation(podcast);
        var storedEpisode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast, release: DomainTestFixture.UtcDateDaysAgo(5));
        _storedReports = [CatalogueParentKindMapper.ToNewsReport(storedEpisode, organisation)];
        _filter = episodes => new FilterResult(
            episodes.Where(x => x.Id == storedEpisode.Id).Select(x => new FilteredEpisode(x, ["term"])).ToList());

        // Act
        await Sut.UpdateParents([organisation.Id], Context());

        // Assert
        var saved = _savedReports.Should().ContainSingle().Subject;
        saved.Id.Should().Be(storedEpisode.Id);
        saved.Removed.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when one parent fails, then the others are still indexed and the pass reports " +
        "failure, because one bad channel must not stop the hourly pass.")]
    public async Task one_failing_parent_does_not_stop_the_rest()
    {
        // Arrange
        var failing = SeedTvShow(AutoIndexedYouTubePodcast());
        var healthy = SeedOrganisation(AutoIndexedYouTubePodcast());
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(x => x.GetByTvShowId(failing.Id, It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .Throws(new InvalidOperationException("synthetic failure"));
        _discovered = [_fixture.CreateYouTubeCatalogueEpisode(b => b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(PostableDuration))];

        // Act
        var success = await Sut.UpdateParents([failing.Id, healthy.Id], Context());

        // Assert
        success.Should().BeFalse();
        _savedReports.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: indexable parent ids are the auto-indexed, non-removed TV shows and news " +
        "organisations in id order, because the hourly passes split them like podcast ids.")]
    public async Task indexable_parent_ids_are_auto_indexed_and_ordered()
    {
        // Arrange
        var show = SeedTvShow(AutoIndexedYouTubePodcast());
        var organisation = SeedOrganisation(AutoIndexedYouTubePodcast());
        var removed = SeedTvShow(AutoIndexedYouTubePodcast());
        removed.Removed = true;
        var bare = SeedOrganisation(AutoIndexedYouTubePodcast());
        bare.Indexing = null;

        // Act
        var ids = await Sut.GetIndexableParentIds();

        // Assert
        ids.Should().Equal(new[] { show.Id, organisation.Id }.Order());
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: discovery runs with the podcast settings the parent kept (channel, release " +
        "authority, include-title regex), because the parent must be discovered exactly as its podcast was.")]
    public async Task discovery_uses_the_kept_podcast_settings()
    {
        // Arrange
        var podcast = AutoIndexedYouTubePodcast();
        podcast.IndexAllEpisodes = false;
        podcast.EpisodeIncludeTitleRegex = "Episode \\d+";
        var show = SeedTvShow(podcast);
        Podcast? transient = null;
        _mocker.GetMock<IEpisodeProvider>()
            .Setup(x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .Callback((Podcast p, IEnumerable<Episode> _, IndexingContext _) => transient = p)
            .ReturnsAsync([]);

        // Act
        await Sut.UpdateParents([show.Id], Context());

        // Assert
        transient.Should().NotBeNull();
        transient!.Id.Should().Be(show.Id);
        transient.YouTubeChannelId.Should().Be(podcast.YouTubeChannelId);
        transient.ReleaseAuthority.Should().Be(podcast.ReleaseAuthority);
        transient.EpisodeIncludeTitleRegex.Should().Be(podcast.EpisodeIncludeTitleRegex);
    }
}
