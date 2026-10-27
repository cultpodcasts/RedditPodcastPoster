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
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;
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
    private readonly List<(Guid Id, CatalogueParentIndexerPatch Patch)> _showPatches = [];
    private readonly List<(Guid Id, CatalogueParentIndexerPatch Patch)> _organisationPatches = [];
    private readonly Dictionary<Guid, TvShow> _shows = [];
    private readonly Dictionary<Guid, NewsOrganisation> _organisations = [];
    private List<TvShowEpisode> _storedTvEpisodes = [];
    private List<NewsReport> _storedReports = [];
    private IList<Episode> _discovered = [];
    private IList<Episode>? _existingPassedToProvider;
    private Func<IList<Episode>, IList<Episode>, EpisodeMergeResult> _merge =
        (_, added) => new EpisodeMergeResult(added, added, [], []);
    private Func<IEnumerable<Episode>, FilterResult> _filter = _ => new FilterResult([]);
    private readonly List<Podcast> _podcasts = [];
    private readonly List<Episode> _podcastEpisodes = [];

    public CatalogueParentIndexingRules()
    {
        _mocker.Use(Options.Create(PodcastUpdaterTestHarness.DefaultPostingCriteria));
        _mocker.Use<TimeProvider>(TimeProvider.System);

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
        var patch = _showPatches.Should().ContainSingle().Subject;
        patch.Id.Should().Be(show.Id);
        patch.Patch.LastIndexed.Should().NotBeNull();
        patch.Patch.LatestReleased.Should().Be(discovered.ReleaseUtc);
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
        _organisationPatches.Should().ContainSingle().Which.Id.Should().Be(organisation.Id);
        _savedTvEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when the podcast a news organisation was migrated from still exists under the same " +
        "id (a move that stopped before deleting the source), then the organisation is skipped with a warning and " +
        "nothing is saved for it, because the podcast is still indexed and indexing both would save each video twice.")]
    public async Task half_moved_news_organisation_is_not_indexed()
    {
        // Arrange
        var source = AutoIndexedYouTubePodcast();
        var organisation = SeedOrganisation(source);
        _podcasts.Add(source);
        _discovered = [_fixture.CreateYouTubeCatalogueEpisode(b => b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(PostableDuration))];

        // Act
        var success = await Sut.UpdateParents([organisation.Id], Context());

        // Assert
        success.Should().BeTrue("a half-finished move is not an indexing failure");
        _savedReports.Should().BeEmpty();
        _organisationPatches.Should().BeEmpty();
        _mocker.GetMock<IEpisodeProvider>().Verify(
            x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Theory(DisplayName =
        "Catalogue parent indexing: when a podcast with the news organisation's id still exists but the podcast pass " +
        "would not index it (removed, not auto-indexed, or only a whitespace title regex), then the organisation is " +
        "indexed, because skipping it would leave nothing indexing the outlet.")]
    [InlineData(true, true, "")]
    [InlineData(false, false, "")]
    [InlineData(false, false, " ")]
    [InlineData(null, false, " \t ")]
    public async Task same_id_podcast_not_indexed_by_podcast_pass_does_not_defer_the_parent(
        bool? removed, bool indexAll, string regex)
    {
        // Arrange
        var source = AutoIndexedYouTubePodcast();
        var organisation = SeedOrganisation(source);
        source.Removed = removed;
        source.IndexAllEpisodes = indexAll;
        source.EpisodeIncludeTitleRegex = regex;
        _podcasts.Add(source);
        _discovered = [_fixture.CreateYouTubeCatalogueEpisode(b => b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(PostableDuration))];

        // Act
        var success = await Sut.UpdateParents([organisation.Id], Context());

        // Assert
        success.Should().BeTrue();
        _savedReports.Should().ContainSingle().Which.NewsOrganisationId.Should().Be(organisation.Id);
    }

    [Theory(DisplayName =
        "Catalogue parent indexing: when a podcast with the news organisation's id still exists and the podcast pass " +
        "would index it (index-all, or a title regex only, with removed absent or false), then the organisation is " +
        "skipped, because the podcast is still indexed and indexing both would save each video twice.")]
    [InlineData(null, true, "")]
    [InlineData(false, true, "")]
    [InlineData(null, false, "Episode \\d+")]
    [InlineData(false, false, "Episode \\d+")]
    public async Task same_id_podcast_indexed_by_podcast_pass_defers_the_parent(
        bool? removed, bool indexAll, string regex)
    {
        // Arrange
        var source = AutoIndexedYouTubePodcast();
        var organisation = SeedOrganisation(source);
        source.Removed = removed;
        source.IndexAllEpisodes = indexAll;
        source.EpisodeIncludeTitleRegex = regex;
        _podcasts.Add(source);
        _discovered = [_fixture.CreateYouTubeCatalogueEpisode(b => b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(PostableDuration))];

        // Act
        var success = await Sut.UpdateParents([organisation.Id], Context());

        // Assert
        success.Should().BeTrue("a half-finished move is not an indexing failure");
        _savedReports.Should().BeEmpty();
        _organisationPatches.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when the podcast a TV show was migrated from still exists under the same id, then " +
        "the show is skipped and the next parent is still indexed, because only the half-moved parent defers to its podcast.")]
    public async Task half_moved_tv_show_is_skipped_and_next_parent_indexed()
    {
        // Arrange
        var halfMovedSource = AutoIndexedYouTubePodcast();
        var halfMoved = SeedTvShow(halfMovedSource);
        _podcasts.Add(halfMovedSource);
        var moved = SeedTvShow(AutoIndexedYouTubePodcast());
        _discovered = [_fixture.CreateYouTubeCatalogueEpisode(b => b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(PostableDuration))];

        // Act
        var success = await Sut.UpdateParents([halfMoved.Id, moved.Id], Context());

        // Assert
        success.Should().BeTrue();
        _savedTvEpisodes.Should().ContainSingle().Which.TvShowId.Should().Be(moved.Id);
        _showPatches.Select(patch => patch.Id).Should().Equal(moved.Id);
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when a news organisation indexes a whole YouTube channel it shares with a " +
        "podcast scoped to one playlist of that channel, then a video the podcast already holds is not also saved " +
        "as a NewsReport, while other channel videos are, because the playlist's videos belong to the podcast.")]
    public async Task shared_channel_video_held_by_playlist_podcast_is_not_a_news_report()
    {
        // Arrange
        var source = AutoIndexedYouTubePodcast();
        source.YouTubePlaylistId = string.Empty;
        var organisation = SeedOrganisation(source);
        var playlistShow = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = source.YouTubeChannelId;
            p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
        });
        _podcasts.Add(playlistShow);
        var inPlaylist = _fixture.CreateYouTubeCatalogueEpisode(b => b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(PostableDuration));
        var channelOnly = _fixture.CreateYouTubeCatalogueEpisode(b => b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(PostableDuration));
        _podcastEpisodes.Add(_fixture.CreateStoredEpisode(playlistShow, e => e.Ids = new EpisodeIds { YouTube = inPlaylist.Ids!.YouTube }));
        _discovered = [inPlaylist, channelOnly];

        // Act
        var success = await Sut.UpdateParents([organisation.Id], Context());

        // Assert
        success.Should().BeTrue();
        _savedReports.Should().ContainSingle().Which.Id.Should().Be(channelOnly.Id);
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: a channel-wide podcast on the same channel does not exclude its videos from " +
        "the parent, because only playlist-scoped podcasts carve videos out of a channel.")]
    public async Task only_playlist_scoped_podcasts_exclude_videos()
    {
        // Arrange
        var source = AutoIndexedYouTubePodcast();
        source.YouTubePlaylistId = string.Empty;
        var organisation = SeedOrganisation(source);
        var channelWide = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = source.YouTubeChannelId;
            p.YouTubePlaylistId = string.Empty;
        });
        _podcasts.Add(channelWide);
        var video = _fixture.CreateYouTubeCatalogueEpisode(b => b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(PostableDuration));
        _podcastEpisodes.Add(_fixture.CreateStoredEpisode(channelWide, e => e.Ids = new EpisodeIds { YouTube = video.Ids!.YouTube }));
        _discovered = [video];

        // Act
        var success = await Sut.UpdateParents([organisation.Id], Context());

        // Assert
        success.Should().BeTrue();
        _savedReports.Should().ContainSingle().Which.Id.Should().Be(video.Id);
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
        _showPatches.Should().BeEmpty();
        _organisationPatches.Should().BeEmpty();
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

    [Fact(DisplayName =
        "Catalogue parent indexing: the parent is never upserted in full; only the Indexer-owned fields it changed " +
        "(lastIndexed and the indexing flags discovery moved) are patched, because a curator edit, rename or removal " +
        "saved during the pass must not be overwritten by the pass's stale snapshot.")]
    public async Task only_indexer_owned_fields_that_changed_are_patched()
    {
        // Arrange
        var organisation = SeedOrganisation(AutoIndexedYouTubePodcast());
        organisation.Indexing!.YouTubePlaylistQueryIsExpensive = false;
        _mocker.GetMock<IEpisodeProvider>()
            .Setup(x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .Callback((Podcast p, IEnumerable<Episode> _, IndexingContext _) => p.YouTubePlaylistQueryIsExpensive = true)
            .ReturnsAsync([]);

        // Act
        var success = await Sut.UpdateParents([organisation.Id], Context());

        // Assert
        success.Should().BeTrue();
        _mocker.GetMock<INewsOrganisationRepository>().Verify(x => x.Save(It.IsAny<NewsOrganisation>()), Times.Never);
        var patch = _organisationPatches.Should().ContainSingle().Subject.Patch;
        patch.LastIndexed.Should().NotBeNull();
        patch.LatestReleased.Should().BeNull();
        patch.ChangedIndexingProperties.Should().Equal("youTubePlaylistQueryIsExpensive");
        patch.Indexing.YouTubePlaylistQueryIsExpensive.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when a pass changes nothing on the parent (it did not complete, found nothing " +
        "new and moved no discovery flag), then the parent is not written at all, because an unchanged parent must " +
        "cost no Cosmos write.")]
    public async Task unchanged_parent_is_not_written()
    {
        // Arrange
        var show = SeedTvShow(AutoIndexedYouTubePodcast());
        _discovered = [_fixture.CreateYouTubeCatalogueEpisode()];
        _merge = (_, incoming) => new EpisodeMergeResult([], [], [], [incoming]);

        // Act
        await Sut.UpdateParents([show.Id], Context());

        // Assert
        _showPatches.Should().BeEmpty();
        _mocker.GetMock<ITvShowRepository>().Verify(x => x.Save(It.IsAny<TvShow>()), Times.Never);
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when the parent was deleted during the pass, then the pass still succeeds, " +
        "because the patch finding no document is not an indexing failure.")]
    public async Task parent_deleted_mid_pass_is_not_a_failure()
    {
        // Arrange
        var show = SeedTvShow(AutoIndexedYouTubePodcast());
        _mocker.GetMock<ITvShowRepository>()
            .Setup(x => x.PatchIndexerState(show.Id, It.IsAny<CatalogueParentIndexerPatch>()))
            .ReturnsAsync(false);

        // Act
        var success = await Sut.UpdateParents([show.Id], Context());

        // Assert
        success.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: YouTube indexing hints the pass refreshed on the transient podcast are patched as " +
        "/indexing/youTubeIndexingHints alone, because they live in the parent's indexing block and only the indexer " +
        "writes them.")]
    public async Task refreshed_youtube_indexing_hints_are_patched()
    {
        // Arrange
        var organisation = SeedOrganisation(AutoIndexedYouTubePodcast());
        var hints = new YouTubeIndexingHints
        {
            PlaylistId = _fixture.CreateYouTubePlaylistId(),
            LastFullWalk = DomainTestFixture.UtcDaysAgo(0),
            PlaylistItemCount = 400
        };
        _mocker.GetMock<IEpisodeProvider>()
            .Setup(x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .Callback((Podcast p, IEnumerable<Episode> _, IndexingContext _) => p.YouTubeIndexingHints = hints)
            .ReturnsAsync([]);

        // Act
        await Sut.UpdateParents([organisation.Id], Context());

        // Assert
        var patch = _organisationPatches.Should().ContainSingle().Subject.Patch;
        patch.ChangedIndexingProperties.Should().Equal("youTubeIndexingHints");
        patch.Indexing.YouTubeIndexingHints.Should().Be(hints);
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when a found episode fails to merge, the parent keeps its previous YouTube " +
        "indexing hints and the patch does not write them, so the next pass offers that episode again.")]
    public async Task failed_merge_keeps_previous_youtube_indexing_hints()
    {
        // Arrange
        var organisation = SeedOrganisation(AutoIndexedYouTubePodcast());
        var previous = new YouTubeIndexingHints
        {
            PlaylistId = _fixture.CreateYouTubePlaylistId(),
            LastFullWalk = DomainTestFixture.UtcDaysAgo(1),
            PlaylistItemCount = 400
        };
        organisation.Indexing!.YouTubeIndexingHints = previous.Copy();
        _discovered = [_fixture.CreateYouTubeCatalogueEpisode()];
        _merge = (_, incoming) => new EpisodeMergeResult([], [], [], [incoming]);
        _mocker.GetMock<IEpisodeProvider>()
            .Setup(x => x.GetEpisodes(It.IsAny<Podcast>(), It.IsAny<IEnumerable<Episode>>(), It.IsAny<IndexingContext>()))
            .Callback((Podcast p, IEnumerable<Episode> _, IndexingContext _) =>
                p.YouTubeIndexingHints = previous with { PlaylistItemCount = 401 })
            .ReturnsAsync(() => _discovered.ToList());

        // Act
        await Sut.UpdateParents([organisation.Id], Context());

        // Assert
        _organisationPatches.SelectMany(x => x.Patch.ChangedIndexingProperties)
            .Should().NotContain("youTubeIndexingHints");
    }

    private static readonly TimeSpan ShortDuration =
        PodcastUpdaterTestHarness.DefaultPostingCriteria.MinimumDuration - TimeSpan.FromSeconds(30);

    [Fact(DisplayName =
        "Catalogue parent indexing: when a news organisation has bypass-short-episodes saved on (the news creation " +
        "default) and a short video is found, then the report is saved and not ignored, and the pass does not write " +
        "the flag, because the stored value drives the check.")]
    public async Task news_organisation_saved_bypass_on_keeps_short_video()
    {
        // Arrange
        var organisation = SeedOrganisation(AutoIndexedYouTubePodcast());
        organisation.Indexing!.BypassShortEpisodeChecking = true;
        organisation.Indexing.MinimumDuration = null;
        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b =>
            b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(ShortDuration));
        _discovered = [discovered];

        // Act
        var success = await Sut.UpdateParents([organisation.Id], Context());

        // Assert
        success.Should().BeTrue();
        _savedReports.Should().ContainSingle().Which.Ignored.Should().BeFalse();
        _organisationPatches.SelectMany(x => x.Patch.ChangedIndexingProperties)
            .Should().NotContain("bypassShortEpisodeChecking");
        organisation.Indexing.BypassShortEpisodeChecking.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when a news organisation has bypass-short-episodes unset and the pass keeps short " +
        "items, then a short video is saved ignored and the flag is not written, because there is no read-time news " +
        "default (news organisations are saved with it on instead).")]
    public async Task news_organisation_unset_bypass_applies_short_episode_check()
    {
        // Arrange
        var organisation = SeedOrganisation(AutoIndexedYouTubePodcast());
        organisation.Indexing!.BypassShortEpisodeChecking = null;
        organisation.Indexing.MinimumDuration = null;
        _discovered = [_fixture.CreateYouTubeCatalogueEpisode(b =>
            b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(ShortDuration))];

        // Act
        await Sut.UpdateParents([organisation.Id], Context() with { SkipShortEpisodes = false });

        // Assert
        _savedReports.Should().ContainSingle().Which.Ignored.Should().BeTrue();
        _organisationPatches.SelectMany(x => x.Patch.ChangedIndexingProperties)
            .Should().NotContain("bypassShortEpisodeChecking");
        organisation.Indexing.BypassShortEpisodeChecking.Should().BeNull();
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when a news organisation explicitly turns bypass-short-episodes off and the pass keeps " +
        "short items, then a short video is saved ignored, because a curator may switch the saved news default off.")]
    public async Task news_organisation_explicit_bypass_off_ignores_short_video()
    {
        // Arrange
        var organisation = SeedOrganisation(AutoIndexedYouTubePodcast());
        organisation.Indexing!.BypassShortEpisodeChecking = false;
        organisation.Indexing.MinimumDuration = null;
        _discovered = [_fixture.CreateYouTubeCatalogueEpisode(b =>
            b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(ShortDuration))];

        // Act
        await Sut.UpdateParents([organisation.Id], Context() with { SkipShortEpisodes = false });

        // Assert
        _savedReports.Should().ContainSingle().Which.Ignored.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when a TV show leaves bypass-short-episodes unset and the pass keeps short items, " +
        "then a short video is saved ignored, because TV shows have no bypass default.")]
    public async Task tv_show_unset_bypass_applies_short_episode_check()
    {
        // Arrange
        var show = SeedTvShow(AutoIndexedYouTubePodcast());
        show.Indexing!.BypassShortEpisodeChecking = null;
        show.Indexing.MinimumDuration = null;
        _discovered = [_fixture.CreateYouTubeCatalogueEpisode(b =>
            b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(ShortDuration))];

        // Act
        await Sut.UpdateParents([show.Id], Context() with { SkipShortEpisodes = false });

        // Assert
        _savedTvEpisodes.Should().ContainSingle().Which.Ignored.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when a parent has ignore-all-episodes set, then a new video is saved ignored, " +
        "because the flag must behave as it does for a podcast.")]
    public async Task ignore_all_episodes_saves_new_playables_ignored()
    {
        // Arrange
        var organisation = SeedOrganisation(AutoIndexedYouTubePodcast());
        organisation.Indexing!.IgnoreAllEpisodes = true;
        _discovered = [_fixture.CreateYouTubeCatalogueEpisode(b =>
            b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(PostableDuration))];

        // Act
        await Sut.UpdateParents([organisation.Id], Context());

        // Assert
        _savedReports.Should().ContainSingle().Which.Ignored.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when a news organisation is flagged always-promote-as-hero, then a new report from " +
        "this week is appended to the hero list after it is saved, and one older than the week window is not, " +
        "because the flag must behave as it does for a podcast.")]
    public async Task always_promote_as_hero_news_organisation_promotes_new_reports()
    {
        // Arrange
        var organisation = SeedOrganisation(AutoIndexedYouTubePodcast());
        organisation.Indexing!.AlwaysPromoteAsHero = true;
        var recent = _fixture.CreateYouTubeCatalogueEpisode(b =>
            b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(PostableDuration));
        var old = _fixture.CreateYouTubeCatalogueEpisode(b =>
            b.WithRelease(DomainTestFixture.UtcDateDaysAgo(20)).WithDuration(PostableDuration));
        _discovered = [recent, old];
        IReadOnlyList<Guid>? promoted = null;
        _mocker.GetMock<IHeroEpisodePromoter>()
            .Setup(x => x.PromoteAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyList<Guid> ids, CancellationToken _) => promoted = ids)
            .Returns(Task.CompletedTask);

        // Act
        await Sut.UpdateParents([organisation.Id], Context());

        // Assert
        _savedReports.Select(x => x.Id).Should().BeEquivalentTo([recent.Id, old.Id]);
        promoted.Should().Equal(recent.Id);
    }

    [Fact(DisplayName =
        "Catalogue parent indexing: when a TV show is flagged always-promote-as-hero, then a new episode is appended to " +
        "the hero list, because TV shows have the same flag as podcasts.")]
    public async Task always_promote_as_hero_tv_show_promotes_new_episodes()
    {
        // Arrange
        var show = SeedTvShow(AutoIndexedYouTubePodcast());
        show.Indexing!.AlwaysPromoteAsHero = true;
        var discovered = _fixture.CreateYouTubeCatalogueEpisode(b =>
            b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(PostableDuration));
        _discovered = [discovered];

        // Act
        await Sut.UpdateParents([show.Id], Context());

        // Assert
        _mocker.GetMock<IHeroEpisodePromoter>().Verify(
            x => x.PromoteAsync(
                It.Is<IReadOnlyList<Guid>>(ids => ids.SequenceEqual(new[] { discovered.Id })),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory(DisplayName =
        "Catalogue parent indexing: when a parent is not flagged always-promote-as-hero, or its new item is ignored " +
        "(ignore-all-episodes), then nothing is appended to the hero list, because hero promotion is opt-in and " +
        "never promotes ignored items.")]
    [InlineData(null, null)]
    [InlineData(false, null)]
    [InlineData(true, true)]
    public async Task no_hero_promotion_without_flag_or_for_ignored_items(bool? alwaysPromote, bool? ignoreAll)
    {
        // Arrange
        var organisation = SeedOrganisation(AutoIndexedYouTubePodcast());
        organisation.Indexing!.AlwaysPromoteAsHero = alwaysPromote;
        organisation.Indexing.IgnoreAllEpisodes = ignoreAll;
        _discovered = [_fixture.CreateYouTubeCatalogueEpisode(b =>
            b.WithRelease(DomainTestFixture.UtcDateDaysAgo(1)).WithDuration(PostableDuration))];

        // Act
        await Sut.UpdateParents([organisation.Id], Context());

        // Assert
        _savedReports.Should().ContainSingle();
        _mocker.GetMock<IHeroEpisodePromoter>().Verify(
            x => x.PromoteAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
