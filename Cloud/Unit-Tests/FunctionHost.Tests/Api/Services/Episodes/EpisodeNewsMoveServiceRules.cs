using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Episodes;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.FileKeys;
using RedditPodcastPoster.Search.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Episodes;

/// <summary>
/// #1388: Move selected podcast episodes to a news organisation without kind-transferring or deleting the podcast.
/// </summary>
public class EpisodeNewsMoveServiceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryPodcastRepository _podcasts = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private readonly List<NewsOrganisation> _organisations = [];
    private readonly List<NewsOrganisation> _savedOrganisations = [];
    private readonly List<NewsReport> _reports = [];
    private readonly List<NewsReport> _savedReports = [];
    private readonly List<TvShowEpisode> _tvEpisodes = [];
    private readonly List<Film> _films = [];
    private readonly List<IReadOnlyList<EpisodeSearchRecord>> _uploads = [];
    private readonly List<IReadOnlyList<Guid>> _searchDeletes = [];
    private bool _searchUploadSucceeds = true;
    private Action? _afterSearchUpload;
    private int _reportSaveFailures;

    public EpisodeNewsMoveServiceRules()
    {
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<IEpisodeRepository>(_episodes);
        var organisations = _mocker.GetMock<INewsOrganisationRepository>();
        organisations
            .Setup(r => r.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _organisations.SingleOrDefault(item => item.Id == id));
        organisations
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns((Expression<Func<NewsOrganisation, bool>> selector) =>
                _organisations.Where(selector.Compile()).ToAsyncEnumerable());
        organisations
            .Setup(r => r.GetAllBy(
                It.IsAny<Expression<Func<NewsOrganisation, bool>>>(),
                It.IsAny<Expression<Func<NewsOrganisation, string>>>()))
            .Returns((Expression<Func<NewsOrganisation, bool>> selector,
                    Expression<Func<NewsOrganisation, string>> projection) =>
                _organisations.Where(selector.Compile()).Select(projection.Compile()).ToAsyncEnumerable());
        organisations
            .Setup(r => r.Save(It.IsAny<NewsOrganisation>()))
            .Callback<NewsOrganisation>(item =>
            {
                _savedOrganisations.Add(item);
                _organisations.RemoveAll(existing => existing.Id == item.Id);
                _organisations.Add(item);
            })
            .Returns(Task.CompletedTask);
        _mocker.Use<IFileKeyClaimStore>(new InMemoryFileKeyClaimStore());
        _mocker.Use<IFileKeyAllocator>(_mocker.CreateInstance<FileKeyAllocator>());
        var reports = _mocker.GetMock<INewsReportRepository>();
        reports
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .Returns((Expression<Func<NewsReport, bool>> selector) =>
                _reports.Where(selector.Compile()).ToList().ToAsyncEnumerable());
        reports
            .Setup(r => r.GetNewsReport(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((Guid organisationId, Guid id) =>
                _reports.SingleOrDefault(item => item.NewsOrganisationId == organisationId && item.Id == id));
        reports
            .Setup(r => r.Save(It.IsAny<NewsReport>()))
            .Returns((NewsReport item) =>
            {
                lock (_reports)
                {
                    if (_reportSaveFailures > 0)
                    {
                        _reportSaveFailures--;
                        throw new InvalidOperationException(_fixture.Create<string>());
                    }

                    _savedReports.Add(item);
                    _reports.RemoveAll(existing => existing.Id == item.Id);
                    _reports.Add(item);
                }

                return Task.CompletedTask;
            });
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .Returns((Expression<Func<TvShowEpisode, bool>> selector) =>
                _tvEpisodes.Where(selector.Compile()).ToAsyncEnumerable());
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .Returns((Expression<Func<Film, bool>> selector) => _films.Where(selector.Compile()).ToAsyncEnumerable());
        var search = _mocker.GetMock<IPlayableSearchDocumentSwap>();
        search
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<EpisodeSearchRecord>, CancellationToken>((documents, _) =>
            {
                _uploads.Add(documents);
                _afterSearchUpload?.Invoke();
            })
            .ReturnsAsync(() => _searchUploadSucceeds);
        search
            .Setup(s => s.DeleteAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<Guid>, CancellationToken>((ids, _) => _searchDeletes.Add(ids))
            .ReturnsAsync(true);
        _mocker.GetMock<ILogger<EpisodeNewsMoveService>>();
    }

    /// <summary>A channel-scoped podcast with Spotify, Apple and YouTube show ids, like one that keeps audio episodes.</summary>
    private Podcast SeedChannelScopedPodcast()
    {
        var podcast = _fixture.CreatePodcast(item =>
        {
            item.SpotifyId = _fixture.CreateSpotifyId();
            item.AppleId = _fixture.CreateAppleId();
            item.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            item.YouTubePlaylistId = _fixture.Create<string>();
            item.FileKey = _fixture.Create<string>();
        });
        _podcasts.Seed(podcast);
        return podcast;
    }

    private Episode SeedEpisode(Podcast podcast, Action<Episode>? configure = null)
    {
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        configure?.Invoke(episode);
        _episodes.Seed(episode);
        return episode;
    }

    private NewsOrganisation SeedOrganisation(string? name = null)
    {
        var organisation = new NewsOrganisation(name ?? _fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _organisations.Add(organisation);
        return organisation;
    }

    private Task<EpisodeNewsMoveResult> MoveOne(Guid podcastId, Guid episodeId, EpisodeNewsMoveRequest request) =>
        _mocker.CreateInstance<EpisodeNewsMoveService>().MoveAsync(
            new EpisodeNewsMoveCommand(podcastId, episodeId, request),
            CancellationToken.None);

    private Task<EpisodeNewsMoveResult> MoveMany(
        Guid podcastId,
        IReadOnlyList<Guid> episodeIds,
        EpisodeNewsMoveRequest request) =>
        _mocker.CreateInstance<EpisodeNewsMoveService>().MoveManyAsync(
            new PodcastNewsMoveCommand(podcastId, episodeIds, request),
            CancellationToken.None);

    private static EpisodeNewsMoveRequest CreateByName(string name, string? publisher = null) =>
        new() { NewsOrganisationName = name, CreateOrganisation = true, Publisher = publisher };

    [Fact(DisplayName =
        "Moving an episode with createOrganisation and the podcast's own name creates a news organisation with a new id (never the podcast id), a new news- file key, the request's publisher and the news creation defaults, because name uniqueness is per kind and the podcast must keep its own id and file key.")]
    public async Task create_organisation_gets_new_id_new_file_key_and_news_defaults()
    {
        // Arrange
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);
        var publisher = _fixture.Create<string>();

        // Act
        var result = await MoveOne(podcast.Id, episode.Id, CreateByName(podcast.Name, publisher));

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Accepted);
        result.CreatedOrganisation.Should().BeTrue();
        result.EpisodeId.Should().Be(episode.Id);
        var organisation = _savedOrganisations.Should().ContainSingle().Subject;
        organisation.Id.Should().NotBe(podcast.Id).And.NotBe(Guid.Empty);
        result.NewsOrganisationId.Should().Be(organisation.Id);
        organisation.Name.Should().Be(podcast.Name.Trim());
        organisation.FileKey.Should().StartWith("news-").And.NotBe(podcast.FileKey);
        organisation.PublisherName.Should().Be(publisher);
        organisation.Indexing.Should().NotBeNull();
        organisation.Indexing!.BypassShortEpisodeChecking.Should().BeTrue();
        organisation.Indexing.AlwaysPromoteAsHero.Should().BeTrue();
        organisation.Indexing.IndexAllEpisodes.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A created news organisation does not get the podcast's YouTube channel or playlist, Spotify or Apple id, because a news organisation on a channel a channel-scoped podcast holds would be refused by the channel-wide claim guard.")]
    public async Task created_organisation_does_not_copy_youtube_channel_or_show_ids()
    {
        // Arrange
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);

        // Act
        await MoveOne(podcast.Id, episode.Id, CreateByName(_fixture.CreateTitle()));

        // Assert
        var indexing = _savedOrganisations.Should().ContainSingle().Subject.Indexing!;
        indexing.YouTubeChannelId.Should().BeNull();
        indexing.YouTubePlaylistId.Should().BeNull();
        indexing.SpotifyId.Should().BeNull();
        indexing.AppleId.Should().BeNull();
    }

    [Fact(DisplayName =
        "After a move the podcast row is kept unchanged with its Spotify, Apple and YouTube show ids and file key, even when it has no episodes left, because this is not a kind transfer.")]
    public async Task podcast_is_kept_with_show_ids_even_when_empty()
    {
        // Arrange
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);

        // Act
        var result = await MoveOne(podcast.Id, episode.Id, CreateByName(_fixture.CreateTitle()));

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Accepted);
        _podcasts.SavedPodcasts.Should().BeEmpty();
        var kept = await _podcasts.GetPodcast(podcast.Id);
        kept.Should().NotBeNull();
        kept!.SpotifyId.Should().Be(podcast.SpotifyId);
        kept.AppleId.Should().Be(podcast.AppleId);
        kept.YouTubeChannelId.Should().Be(podcast.YouTubeChannelId);
        kept.YouTubePlaylistId.Should().Be(podcast.YouTubePlaylistId);
        kept.FileKey.Should().Be(podcast.FileKey);
        (await _episodes.Count(podcast.Id)).Should().Be(0);
    }

    [Fact(DisplayName =
        "Moving an episode into an existing news organisation by id saves a news report with the episode id and the organisation's id and name, swaps its search document to NewsReport, deletes only that episode row and creates no organisation.")]
    public async Task move_to_existing_organisation_by_id_keeps_episode_id()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);
        var sibling = SeedEpisode(podcast);

        // Act
        var result = await MoveOne(podcast.Id, episode.Id,
            new EpisodeNewsMoveRequest { NewsOrganisationId = organisation.Id });

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Accepted);
        result.CreatedOrganisation.Should().BeFalse();
        result.MovedEpisodeIds.Should().Equal(episode.Id);
        _savedOrganisations.Should().BeEmpty();
        var report = _savedReports.Should().ContainSingle().Subject;
        report.Id.Should().Be(episode.Id);
        report.NewsOrganisationId.Should().Be(organisation.Id);
        report.NewsOrganisationName.Should().Be(organisation.Name);
        report.Services.Should().BeEquivalentTo(episode.Services);
        var document = _uploads.Should().ContainSingle().Subject.Should().ContainSingle().Subject;
        document.Id.Should().Be(episode.Id.ToString());
        document.ContentKind.Should().Be(SearchContentKind.NewsReport);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
        (await _episodes.GetEpisode(podcast.Id, sibling.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "A moved episode keeps its posted, tweeted, ignored and removed flags on the news report, and a removed report is deleted from search instead of being uploaded as the new kind.")]
    public async Task move_keeps_flags_and_drops_removed_from_search()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast, item =>
        {
            item.Posted = true;
            item.OldTweeted = true;
            item.Ignored = true;
            item.Removed = true;
        });

        // Act
        var result = await MoveOne(podcast.Id, episode.Id,
            new EpisodeNewsMoveRequest { NewsOrganisationId = organisation.Id });

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Accepted);
        var report = _savedReports.Should().ContainSingle().Subject;
        report.Posted.Should().BeTrue();
        report.Tweeted.Should().BeTrue();
        report.Ignored.Should().BeTrue();
        report.Removed.Should().BeTrue();
        _uploads.Should().BeEmpty();
        _searchDeletes.Should().ContainSingle().Which.Should().Equal(episode.Id);
    }

    [Fact(DisplayName =
        "With createOrganisation and a name one news organisation already has, the episode joins that organisation and its publisher is not changed, so a re-send after an interrupted create never makes a second organisation.")]
    public async Task create_with_existing_name_joins_existing_organisation()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var publisher = organisation.PublisherName;
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);

        // Act
        var result = await MoveOne(podcast.Id, episode.Id,
            CreateByName(organisation.Name.ToUpperInvariant(), _fixture.Create<string>()));

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Accepted);
        result.CreatedOrganisation.Should().BeFalse();
        result.NewsOrganisationId.Should().Be(organisation.Id);
        _savedOrganisations.Should().BeEmpty();
        _organisations.Single().PublisherName.Should().Be(publisher);
    }

    [Fact(DisplayName =
        "A name no news organisation has, without createOrganisation, is a not-found and writes nothing, so a typo never creates an organisation or loses the episode.")]
    public async Task missing_organisation_without_create_writes_nothing()
    {
        // Arrange
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);

        // Act
        var result = await MoveOne(podcast.Id, episode.Id,
            new EpisodeNewsMoveRequest { NewsOrganisationName = podcast.Name });

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.NotFound);
        _savedOrganisations.Should().BeEmpty();
        _savedReports.Should().BeEmpty();
        _uploads.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "A publisher without createOrganisation is a bad request, because the move must not change an existing organisation's publisher.")]
    public async Task publisher_without_create_is_bad_request()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);

        // Act
        var result = await MoveOne(podcast.Id, episode.Id, new EpisodeNewsMoveRequest
        {
            NewsOrganisationId = organisation.Id,
            Publisher = _fixture.Create<string>()
        });

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.BadRequest);
        _savedReports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the search swap fails the move is a failure with failureIndexing and the source episode row is kept, so a re-send can resume.")]
    public async Task search_failure_keeps_source_episode()
    {
        // Arrange
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);
        _searchUploadSucceeds = false;

        // Act
        var result = await MoveOne(podcast.Id, episode.Id, CreateByName(_fixture.CreateTitle()));

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Failed);
        result.FailureIndexing.Should().BeTrue();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
        (await _podcasts.GetPodcast(podcast.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "When saving the news report fails the move is a failure and the source episode row is kept.")]
    public async Task report_save_failure_keeps_source_episode()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);
        _reportSaveFailures = 1;

        // Act
        var result = await MoveOne(podcast.Id, episode.Id,
            new EpisodeNewsMoveRequest { NewsOrganisationId = organisation.Id });

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Failed);
        _uploads.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "When the news organisation is deleted concurrently after the search swap, the source episode row is kept, because the episode would have nowhere to live.")]
    public async Task organisation_deleted_concurrently_keeps_source_episode()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);
        _afterSearchUpload = () => _organisations.Clear();

        // Act
        var result = await MoveOne(podcast.Id, episode.Id,
            new EpisodeNewsMoveRequest { NewsOrganisationId = organisation.Id });

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Failed);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "An episode id that is not on the given podcast is refused as not found and nothing is moved, even when it is an episode of another podcast.")]
    public async Task episode_of_another_podcast_is_refused()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var podcast = SeedChannelScopedPodcast();
        var other = SeedChannelScopedPodcast();
        var foreign = SeedEpisode(other);

        // Act
        var result = await MoveOne(podcast.Id, foreign.Id,
            new EpisodeNewsMoveRequest { NewsOrganisationId = organisation.Id });

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.NotFound);
        result.NotFoundEpisodeIds.Should().Equal(foreign.Id);
        _savedReports.Should().BeEmpty();
        (await _episodes.GetEpisode(other.Id, foreign.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Batch: the news organisation is created once and only the listed episode ids move; the podcast's other episodes stay on the podcast.")]
    public async Task batch_creates_organisation_once_and_moves_only_listed_ids()
    {
        // Arrange
        var podcast = SeedChannelScopedPodcast();
        var listed = Enumerable.Range(0, 3).Select(_ => SeedEpisode(podcast)).ToList();
        var kept = SeedEpisode(podcast);

        // Act
        var result = await MoveMany(podcast.Id, listed.Select(item => item.Id).ToList(),
            CreateByName(podcast.Name));

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Accepted);
        result.CreatedOrganisation.Should().BeTrue();
        result.MovedEpisodeIds.Should().BeEquivalentTo(listed.Select(item => item.Id));
        var organisation = _savedOrganisations.Should().ContainSingle().Subject;
        _savedReports.Select(report => report.Id).Should().BeEquivalentTo(listed.Select(item => item.Id));
        _savedReports.Should().OnlyContain(report => report.NewsOrganisationId == organisation.Id);
        foreach (var item in listed)
        {
            (await _episodes.GetEpisode(podcast.Id, item.Id)).Should().BeNull();
        }

        (await _episodes.GetEpisode(podcast.Id, kept.Id)).Should().NotBeNull();
        _podcasts.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Batch: when any listed id is not an episode of the podcast, the whole request is refused and nothing is written, not even the organisation.")]
    public async Task batch_with_unknown_id_writes_nothing()
    {
        // Arrange
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);
        var unknown = _fixture.CreateGuid();

        // Act
        var result = await MoveMany(podcast.Id, [episode.Id, unknown], CreateByName(_fixture.CreateTitle()));

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.NotFound);
        result.NotFoundEpisodeIds.Should().Equal(unknown);
        result.NewsOrganisationId.Should().BeNull();
        _savedOrganisations.Should().BeEmpty();
        _savedReports.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Batch: an id that is already a TV episode or a news report of another organisation is a conflict and nothing is written.")]
    public async Task batch_with_conflicting_ids_writes_nothing()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var other = SeedOrganisation();
        var podcast = SeedChannelScopedPodcast();
        var asTv = SeedEpisode(podcast);
        var asOtherReport = SeedEpisode(podcast);
        _tvEpisodes.Add(new TvShowEpisode(asTv.Title) { Id = asTv.Id, TvShowId = _fixture.CreateGuid() });
        _reports.Add(CatalogueParentKindMapper.ToNewsReport(asOtherReport, other));

        // Act
        var result = await MoveMany(podcast.Id, [asTv.Id, asOtherReport.Id],
            new EpisodeNewsMoveRequest { NewsOrganisationId = organisation.Id });

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Conflict);
        result.ConflictEpisodeIds.Should().BeEquivalentTo(new[] { asTv.Id, asOtherReport.Id });
        _savedReports.Should().BeEmpty();
        (await _episodes.Count(podcast.Id)).Should().Be(2);
    }

    [Fact(DisplayName =
        "Batch: re-sending after a completed move reports the ids as already moved and is accepted, so a lost 202 is safe to retry.")]
    public async Task batch_resend_after_completed_move_is_accepted()
    {
        // Arrange
        var podcast = SeedChannelScopedPodcast();
        var episodes = Enumerable.Range(0, 2).Select(_ => SeedEpisode(podcast)).ToList();
        var ids = episodes.Select(item => item.Id).ToList();
        var request = CreateByName(_fixture.CreateTitle());
        var first = await MoveMany(podcast.Id, ids, request);

        // Act
        var result = await MoveMany(podcast.Id, ids, request);

        // Assert
        first.Status.Should().Be(EpisodeNewsMoveStatus.Accepted);
        result.Status.Should().Be(EpisodeNewsMoveStatus.Accepted);
        result.CreatedOrganisation.Should().BeFalse();
        result.NewsOrganisationId.Should().Be(first.NewsOrganisationId);
        result.AlreadyMovedEpisodeIds.Should().BeEquivalentTo(ids);
        result.MovedEpisodeIds.Should().BeEmpty();
        _savedOrganisations.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "Batch: an empty episodeIds list is a bad request and nothing is written.")]
    public async Task batch_without_ids_is_bad_request()
    {
        // Arrange
        var podcast = SeedChannelScopedPodcast();
        SeedEpisode(podcast);

        // Act
        var result = await MoveMany(podcast.Id, [], CreateByName(_fixture.CreateTitle()));

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.BadRequest);
        _savedOrganisations.Should().BeEmpty();
        (await _episodes.Count(podcast.Id)).Should().Be(1);
    }
}
