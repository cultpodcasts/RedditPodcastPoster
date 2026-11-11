using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Catalogue.Episodes;
using RedditPodcastPoster.Catalogue.Podcasts;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;
using RedditPodcastPoster.PodcastServices.Abstractions.Matching;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Tests.Support;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.Text.EliminationTerms;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

public class CatalogueParentAdditionalYouTubeChannelRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(60);
    private static readonly TimeSpan PostableDuration =
        PodcastUpdaterTestHarness.DefaultPostingCriteria.MinimumDuration + TimeSpan.FromMinutes(5);

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<TvShowEpisode> _savedTvEpisodes = [];
    private readonly List<NewsReport> _savedReports = [];
    private readonly List<(Guid Id, CatalogueParentIndexerPatch Patch)> _showPatches = [];
    private readonly List<(Guid Id, CatalogueParentIndexerPatch Patch)> _organisationPatches = [];
    private readonly Dictionary<Guid, TvShow> _shows = [];
    private readonly Dictionary<Guid, NewsOrganisation> _organisations = [];
    private readonly List<TvShowEpisode> _storedTvEpisodes = [];
    private readonly List<NewsReport> _storedReports = [];
    private readonly Dictionary<string, List<Episode>> _discoveredByChannel = [];
    private readonly List<Podcast> _providerCalls = [];
    private Action<Podcast> _onProviderCall = _ => { };
    private readonly Func<IList<Episode>, IList<Episode>, EpisodeMergeResult> _merge =
        (_, added) => new EpisodeMergeResult(added, added, [], []);
    private readonly Func<IEnumerable<Episode>, FilterResult> _filter = _ => new FilterResult([]);
    private readonly List<Podcast> _podcasts = [];
    private readonly List<Episode> _podcastEpisodes = [];

    public CatalogueParentAdditionalYouTubeChannelRules()
    {
        _mocker.Use(Options.Create(PodcastUpdaterTestHarness.DefaultPostingCriteria));
        _mocker.Use<TimeProvider>(TimeProvider.System);
        _mocker.Use<IParentIndexingLockStore>(new InMemoryParentIndexingLockStore());

        var tvShows = _mocker.GetMock<ITvShowRepository>();
        tvShows.Setup(x => x.GetTvShow(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _shows.GetValueOrDefault(id));
        tvShows.Setup(x => x.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns(() => _shows.Values.ToAsyncEnumerable());
        tvShows.Setup(x => x.PatchIndexerState(It.IsAny<Guid>(), It.IsAny<CatalogueParentIndexerPatch>()))
            .Callback((Guid id, CatalogueParentIndexerPatch patch) => _showPatches.Add((id, patch)))
            .ReturnsAsync(true);

        var organisations = _mocker.GetMock<INewsOrganisationRepository>();
        organisations.Setup(x => x.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _organisations.GetValueOrDefault(id));
        organisations.Setup(x => x.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns(() => _organisations.Values.ToAsyncEnumerable());
        organisations.Setup(x => x.PatchIndexerState(It.IsAny<Guid>(), It.IsAny<CatalogueParentIndexerPatch>()))
            .Callback((Guid id, CatalogueParentIndexerPatch patch) => _organisationPatches.Add((id, patch)))
            .ReturnsAsync(true);

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
            .ReturnsAsync((Podcast podcast, IEnumerable<Episode> _, IndexingContext context) =>
            {
                _providerCalls.Add(podcast);
                _onProviderCall(podcast);
                return context.SkipYouTubeUrlResolving
                    ? []
                    : _discoveredByChannel.GetValueOrDefault(podcast.YouTubeChannelId, []).ToList();
            });
        _mocker.GetMock<IEpisodeMerger>()
            .Setup(x => x.MergeEpisodes(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IEnumerable<Episode>>()))
            .Returns((Podcast _, IEnumerable<Episode> existing, IEnumerable<Episode> incoming) =>
                _merge(existing.ToList(), incoming.ToList()));
        _mocker.GetMock<IPodcastFilter>()
            .Setup(x => x.Filter(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<List<string>>()))
            .Returns((Podcast _, IEnumerable<Episode> episodes, List<string> _) => _filter(episodes));

        _mocker.GetMock<IPodcastRepository>()
            .Setup(x => x.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns((Expression<Func<Podcast, bool>> predicate) =>
                _podcasts.Where(predicate.Compile()).ToAsyncEnumerable());
        _mocker.GetMock<IPodcastRepository>()
            .Setup(x => x.GetPodcast(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _podcasts.SingleOrDefault(x => x.Id == id));
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(x => x.GetByPodcastId(It.IsAny<Guid>(), It.IsAny<Expression<Func<Episode, bool>>>()))
            .Returns((Guid id, Expression<Func<Episode, bool>> _) =>
                _podcastEpisodes.Where(x => x.PodcastId == id).ToAsyncEnumerable());

        _mocker.GetMock<IEliminationTermsProvider>()
            .Setup(x => x.GetEliminationTerms())
            .Returns(new EliminationTerms());
        _mocker.GetMock<IAsyncInstance<IEliminationTermsProvider>>()
            .Setup(x => x.GetAsync())
            .ReturnsAsync(() => _mocker.GetMock<IEliminationTermsProvider>().Object);
    }

    private CatalogueParentsUpdater Sut => _mocker.CreateInstance<CatalogueParentsUpdater>();

    private static IndexingContext Context() => new(ReleasedSince);

    private Episode NewVideo() =>
        _fixture.CreateYouTubeCatalogueEpisode(b =>
            b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(PostableDuration));

    /// <summary>An auto-indexed whole-channel news organisation on a main channel plus one additional channel.</summary>
    private (NewsOrganisation Organisation, string MainChannelId, string AdditionalChannelId) SeedMultiChannelOrganisation()
    {
        var podcast = _fixture.CreateYouTubeReleaseAuthorityPodcast(_fixture.CreateYouTubeChannelId(), 0);
        podcast.IndexAllEpisodes = true;
        podcast.YouTubePlaylistId = string.Empty;
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);
        var additionalChannelId = _fixture.CreateYouTubeChannelId();
        organisation.Indexing!.AdditionalYouTubeChannelIds = [additionalChannelId];
        _organisations[organisation.Id] = organisation;
        return (organisation, podcast.YouTubeChannelId, additionalChannelId);
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when a news organisation has an additional YouTube channel, then new videos on " +
        "the main channel and on the additional channel are both saved as NewsReports under the one organisation, " +
        "because an outlet with a main and an archive channel is one news organisation (#1370).")]
    public async Task videos_on_main_and_additional_channels_are_saved_under_one_organisation()
    {
        // Arrange
        var (organisation, mainChannelId, additionalChannelId) = SeedMultiChannelOrganisation();
        var mainVideo = NewVideo();
        var archiveVideo = NewVideo();
        _discoveredByChannel[mainChannelId] = [mainVideo];
        _discoveredByChannel[additionalChannelId] = [archiveVideo];

        // Act
        var success = await Sut.UpdateParents([organisation.Id], Context());

        // Assert
        success.Should().BeTrue();
        _savedReports.Select(x => x.Id).Should().BeEquivalentTo([mainVideo.Id, archiveVideo.Id]);
        _savedReports.Should().OnlyContain(x => x.NewsOrganisationId == organisation.Id);
        _providerCalls.Select(x => x.YouTubeChannelId).Should().Equal(mainChannelId, additionalChannelId);
        var additionalPass = _providerCalls[1];
        additionalPass.Id.Should().Be(organisation.Id);
        additionalPass.YouTubePlaylistId.Should().BeEmpty();
        additionalPass.SpotifyId.Should().BeEmpty();
        additionalPass.AppleId.Should().BeNull();
        _organisationPatches.Should().ContainSingle().Which.Patch.LastIndexed.Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: a video returned by both the main and an additional channel discovery is saved " +
        "once, because one YouTube video is one news report.")]
    public async Task video_found_on_two_channels_is_saved_once()
    {
        // Arrange
        var (organisation, mainChannelId, additionalChannelId) = SeedMultiChannelOrganisation();
        var video = NewVideo();
        _discoveredByChannel[mainChannelId] = [video];
        _discoveredByChannel[additionalChannelId] = [video];

        // Act
        var success = await Sut.UpdateParents([organisation.Id], Context());

        // Assert
        success.Should().BeTrue();
        _savedReports.Should().ContainSingle().Which.Id.Should().Be(video.Id);
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: indexer state the additional channel pass changes (an expensive-query flag) is " +
        "not written back to the organisation, because indexing.* bookmarks describe the main channel only; a " +
        "warning names the channel so the repeated search / playlist cost is visible.")]
    public async Task additional_channel_pass_does_not_write_indexer_state()
    {
        // Arrange
        var (organisation, mainChannelId, additionalChannelId) = SeedMultiChannelOrganisation();
        _discoveredByChannel[mainChannelId] = [];
        _discoveredByChannel[additionalChannelId] = [NewVideo()];
        _onProviderCall = podcast =>
        {
            if (podcast.YouTubeChannelId == additionalChannelId)
            {
                podcast.YouTubePlaylistQueryIsExpensive = true;
                podcast.YouTubeChannelSearchForbidden = true;
            }
        };

        // Act
        var success = await Sut.UpdateParents([organisation.Id], Context());

        // Assert
        success.Should().BeTrue();
        var patch = _organisationPatches.Should().ContainSingle().Subject.Patch;
        patch.ChangedIndexingProperties.Should().NotContain(["youTubePlaylistQueryIsExpensive", "youTubeChannelSearchForbidden"]);
        organisation.Indexing!.YouTubePlaylistQueryIsExpensive.Should().NotBe(true);
        organisation.Indexing.YouTubeChannelSearchForbidden.Should().NotBe(true);
        organisation.Indexing.AdditionalYouTubeChannelIds.Should().Equal(additionalChannelId);
        _mocker.GetMock<ILogger<CatalogueParentsUpdater>>().Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) =>
                    v.ToString()!.Contains(additionalChannelId) && v.ToString()!.Contains("not persisted")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: a pass that skips YouTube does not discover the additional channel and does not " +
        "move lastIndexed, because an additional channel is a YouTube-only source.")]
    public async Task youtube_skipped_pass_skips_additional_channel()
    {
        // Arrange
        var (organisation, mainChannelId, additionalChannelId) = SeedMultiChannelOrganisation();
        _discoveredByChannel[mainChannelId] = [];
        _discoveredByChannel[additionalChannelId] = [NewVideo()];

        // Act
        await Sut.UpdateParents([organisation.Id], Context() with { SkipYouTubeUrlResolving = true });

        // Assert
        _providerCalls.Select(x => x.YouTubeChannelId).Should().NotContain(additionalChannelId);
        _savedReports.Should().BeEmpty();
        _organisationPatches.Should().OnlyContain(x => x.Patch.LastIndexed == null);
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: a video on an additional channel that a playlist-scoped podcast of that channel " +
        "already holds is not also saved as a NewsReport, because the playlist's videos belong to the podcast, as on " +
        "the main channel.")]
    public async Task additional_channel_video_held_by_playlist_podcast_is_not_a_news_report()
    {
        // Arrange
        var (organisation, mainChannelId, additionalChannelId) = SeedMultiChannelOrganisation();
        var playlistShow = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = additionalChannelId;
            p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
        });
        _podcasts.Add(playlistShow);
        var inPlaylist = NewVideo();
        var channelOnly = NewVideo();
        _podcastEpisodes.Add(_fixture.CreateStoredEpisode(playlistShow,
            e => e.Ids = new EpisodeIds { YouTube = inPlaylist.Ids!.YouTube }));
        _discoveredByChannel[mainChannelId] = [];
        _discoveredByChannel[additionalChannelId] = [inPlaylist, channelOnly];

        // Act
        var success = await Sut.UpdateParents([organisation.Id], Context());

        // Assert
        success.Should().BeTrue();
        _savedReports.Should().ContainSingle().Which.Id.Should().Be(channelOnly.Id);
    }
}
