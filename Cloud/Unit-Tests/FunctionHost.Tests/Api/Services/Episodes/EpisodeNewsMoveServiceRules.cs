using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Episodes;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.Configuration.Options;
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
using RedditPodcastPoster.UrlShortening.Services;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Episodes;

/// <summary>
/// #1388: Move selected podcast episodes to a news organisation without kind-transferring or deleting the podcast.
/// </summary>
public class EpisodeNewsMoveServiceRules
{
    /// <summary>Global short-episode floor used when the podcast has no <see cref="Podcast.MinimumDuration"/>.</summary>
    private static readonly TimeSpan PostingMinimumDuration = TimeSpan.FromMinutes(10);

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
    private readonly List<(Guid OrganisationId, DateTime LatestReleased)> _latestReleasedRaises = [];
    private readonly List<Guid> _shortLinkDeletes = [];
    private Func<DeleteResult> _shortLinkDeleteResult = () => new DeleteResult(true);
    private bool _searchUploadSucceeds = true;
    private int _failUploadNumber;
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
        organisations
            .Setup(r => r.RaiseLatestReleased(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .Callback<Guid, DateTime>((id, value) => _latestReleasedRaises.Add((id, value)))
            .ReturnsAsync(true);
        _mocker.Use(Options.Create(new PostingCriteria
        {
            MinimumDuration = PostingMinimumDuration,
            TweetDays = 2,
            RedditDays = 2,
            BlueSkyDays = 2,
            CategoriserDays = 2
        }));
        _mocker.GetMock<IShortnerService>()
            .Setup(s => s.Delete(It.IsAny<IEnumerable<PodcastEpisode>>()))
            .Callback<IEnumerable<PodcastEpisode>>(targets =>
                _shortLinkDeletes.AddRange(targets.Select(target => target.Episode.Id)))
            .ReturnsAsync(() => _shortLinkDeleteResult());
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
            .ReturnsAsync(() => _searchUploadSucceeds && _uploads.Count != _failUploadNumber);
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
        // Long enough for any short-episode threshold, so the kept ignore is a genuine (manual) one.
        var episode = SeedEpisode(podcast, item =>
        {
            item.Posted = true;
            item.OldTweeted = true;
            item.Ignored = true;
            item.Removed = true;
            item.Length = PostingMinimumDuration + TimeSpan.FromHours(10);
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

    private Podcast SeedPodcastEnforcingShortEpisodes()
    {
        var podcast = SeedChannelScopedPodcast();
        podcast.MinimumDuration = null;
        podcast.BypassShortEpisodeChecking = false;
        _podcasts.Seed(podcast);
        return podcast;
    }

    [Fact(DisplayName =
        "A podcast episode ignored only because it is under the podcast's short-episode threshold is not ignored as a news report after Move to news, the same as the kind transfer, because a news organisation bypasses short-episode checking.")]
    public async Task short_length_ignored_episode_is_not_ignored_as_report()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var podcast = SeedPodcastEnforcingShortEpisodes();
        var episode = SeedEpisode(podcast, item =>
        {
            item.Ignored = true;
            item.Length = PostingMinimumDuration - TimeSpan.FromSeconds(1);
        });

        // Act
        var result = await MoveOne(podcast.Id, episode.Id,
            new EpisodeNewsMoveRequest { NewsOrganisationId = organisation.Id });

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Accepted);
        _savedReports.Should().ContainSingle().Which.Ignored.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A long podcast episode that was ignored stays ignored as a news report after Move to news, because its ignore did not come from the short-episode threshold.")]
    public async Task long_ignored_episode_stays_ignored_as_report()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var podcast = SeedPodcastEnforcingShortEpisodes();
        var episode = SeedEpisode(podcast, item =>
        {
            item.Ignored = true;
            item.Length = PostingMinimumDuration;
        });

        // Act
        await MoveOne(podcast.Id, episode.Id, new EpisodeNewsMoveRequest { NewsOrganisationId = organisation.Id });

        // Assert
        _savedReports.Should().ContainSingle().Which.Ignored.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Moving into an existing news organisation with an older latestReleased raises it to the newest moved report with a conditional patch, because a created organisation has no YouTube channel and no Indexer pass would raise it.")]
    public async Task existing_organisation_latest_released_is_raised()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var podcast = SeedChannelScopedPodcast();
        var episodes = Enumerable.Range(0, 3).Select(_ => SeedEpisode(podcast)).ToList();
        organisation.LatestReleased = DateTime.SpecifyKind(DateTime.MinValue.AddYears(1), DateTimeKind.Utc);

        // Act
        var result = await MoveMany(podcast.Id, episodes.Select(item => item.Id).ToList(),
            new EpisodeNewsMoveRequest { NewsOrganisationId = organisation.Id });

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Accepted);
        _savedOrganisations.Should().BeEmpty();
        _latestReleasedRaises.Should().ContainSingle()
            .Which.Should().Be((organisation.Id, _savedReports.Max(report => report.ReleaseSort)));
    }

    [Fact(DisplayName =
        "Moving into an existing news organisation whose latestReleased is already newer does not patch it, so it is never lowered.")]
    public async Task newer_latest_released_is_not_lowered()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);
        organisation.LatestReleased = DateTime.SpecifyKind(DateTime.MaxValue.AddYears(-1), DateTimeKind.Utc);

        // Act
        var result = await MoveOne(podcast.Id, episode.Id,
            new EpisodeNewsMoveRequest { NewsOrganisationId = organisation.Id });

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Accepted);
        _latestReleasedRaises.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A failed latestReleased patch does not fail the move, because the reports, search and episode rows are already done.")]
    public async Task latest_released_patch_failure_does_not_fail_the_move()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.RaiseLatestReleased(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .ThrowsAsync(new InvalidOperationException(_fixture.Create<string>()));

        // Act
        var result = await MoveOne(podcast.Id, episode.Id,
            new EpisodeNewsMoveRequest { NewsOrganisationId = organisation.Id });

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Accepted);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "After a move the moved episodes' short-link records are deleted, as in the TV and film moves, so the Shortner resolves each id from search (now NewsReport) in one hop.")]
    public async Task move_deletes_short_link_records()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var podcast = SeedChannelScopedPodcast();
        var listed = Enumerable.Range(0, 2).Select(_ => SeedEpisode(podcast)).ToList();
        var kept = SeedEpisode(podcast);

        // Act
        await MoveMany(podcast.Id, listed.Select(item => item.Id).ToList(),
            new EpisodeNewsMoveRequest { NewsOrganisationId = organisation.Id });

        // Assert
        _shortLinkDeletes.Should().BeEquivalentTo(listed.Select(item => item.Id));
        _shortLinkDeletes.Should().NotContain(kept.Id);
    }

    [Fact(DisplayName =
        "A short-link delete failure does not fail the move, because the reports, search and episode rows are already done.")]
    public async Task short_link_failure_does_not_fail_the_move()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);
        _shortLinkDeleteResult = () => throw new InvalidOperationException(_fixture.Create<string>());

        // Act
        var result = await MoveOne(podcast.Id, episode.Id,
            new EpisodeNewsMoveRequest { NewsOrganisationId = organisation.Id });

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Accepted);
        result.MovedEpisodeIds.Should().Equal(episode.Id);
    }

    [Fact(DisplayName =
        "An episode row deleted by someone else during the move is reported in movedEpisodeIds, because its news report and search document already exist, so the caller can see it.")]
    public async Task episode_deleted_during_move_is_reported_as_moved()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var podcast = SeedChannelScopedPodcast();
        var stays = SeedEpisode(podcast);
        var vanishes = SeedEpisode(podcast);
        _afterSearchUpload = () => _episodes.Delete(podcast.Id, vanishes.Id).GetAwaiter().GetResult();

        // Act
        var result = await MoveMany(podcast.Id, [stays.Id, vanishes.Id],
            new EpisodeNewsMoveRequest { NewsOrganisationId = organisation.Id });

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Accepted);
        result.MovedEpisodeIds.Should().BeEquivalentTo(new[] { stays.Id, vanishes.Id });
        _reports.Select(report => report.Id).Should().BeEquivalentTo(new[] { stays.Id, vanishes.Id });
        _shortLinkDeletes.Should().Equal(stays.Id);
    }

    [Fact(DisplayName =
        "An episode edited between the report save and the delete is remapped and re-swapped before its row goes, so the newer data reaches the news report and search and the edit is not lost.")]
    public async Task episode_edited_during_move_is_remapped_before_delete()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);
        var editedTitle = _fixture.CreateTitle();
        _afterSearchUpload = () =>
        {
            _afterSearchUpload = null;
            var edited = _episodes.GetEpisode(podcast.Id, episode.Id).GetAwaiter().GetResult()!;
            edited.Title = editedTitle;
            _episodes.Seed(edited);
        };

        // Act
        var result = await MoveOne(podcast.Id, episode.Id,
            new EpisodeNewsMoveRequest { NewsOrganisationId = organisation.Id });

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Accepted);
        _savedReports.Should().HaveCount(2);
        _reports.Should().ContainSingle().Which.Title.Should().Be(editedTitle);
        _uploads.Should().HaveCount(2);
        _uploads[1].Should().ContainSingle().Which.Id.Should().Be(episode.Id.ToString());
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "When the search swap of a remapped episode fails, the move is a failure with failureIndexing and the source episode row is kept, so the edit is not lost and a re-send resumes.")]
    public async Task failed_remap_swap_keeps_source_episode()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);
        _failUploadNumber = 2;
        _afterSearchUpload = () =>
        {
            _afterSearchUpload = null;
            var edited = _episodes.GetEpisode(podcast.Id, episode.Id).GetAwaiter().GetResult()!;
            edited.Title = _fixture.CreateTitle();
            _episodes.Seed(edited);
        };

        // Act
        var result = await MoveOne(podcast.Id, episode.Id,
            new EpisodeNewsMoveRequest { NewsOrganisationId = organisation.Id });

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Failed);
        result.FailureIndexing.Should().BeTrue();
        _uploads.Should().HaveCount(2);
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
        _shortLinkDeletes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "An id that is already a film is a conflict and nothing is written.")]
    public async Task film_id_is_conflict()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);
        _films.Add(new Film(episode.Title) { Id = episode.Id });

        // Act
        var result = await MoveOne(podcast.Id, episode.Id,
            new EpisodeNewsMoveRequest { NewsOrganisationId = organisation.Id });

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Conflict);
        result.ConflictEpisodeIds.Should().Equal(episode.Id);
        _savedReports.Should().BeEmpty();
        _uploads.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "Batch: more than MaxEpisodesPerRequest distinct ids is a bad request and nothing is written.")]
    public async Task batch_over_limit_is_bad_request()
    {
        // Arrange
        var podcast = SeedChannelScopedPodcast();
        var ids = Enumerable.Range(0, EpisodeNewsMoveService.MaxEpisodesPerRequest + 1)
            .Select(_ => _fixture.CreateGuid()).ToList();

        // Act
        var result = await MoveMany(podcast.Id, ids, CreateByName(_fixture.CreateTitle()));

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.BadRequest);
        _savedOrganisations.Should().BeEmpty();
        _savedReports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Batch: an episodeIds list containing an empty id is a bad request and nothing is written.")]
    public async Task batch_with_empty_id_is_bad_request()
    {
        // Arrange
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);

        // Act
        var result = await MoveMany(podcast.Id, [episode.Id, Guid.Empty], CreateByName(_fixture.CreateTitle()));

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.BadRequest);
        _savedOrganisations.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "A removed news organisation picked by id is a bad request and nothing is written, because a removed organisation cannot take reports.")]
    public async Task removed_organisation_by_id_is_bad_request()
    {
        // Arrange
        var organisation = SeedOrganisation();
        organisation.Removed = true;
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);

        // Act
        var result = await MoveOne(podcast.Id, episode.Id,
            new EpisodeNewsMoveRequest { NewsOrganisationId = organisation.Id });

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.BadRequest);
        _savedReports.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "A removed news organisation picked by name is a bad request and nothing is written, even with createOrganisation, so a second organisation with the name is never created.")]
    public async Task removed_organisation_by_name_is_bad_request()
    {
        // Arrange
        var organisation = SeedOrganisation();
        organisation.Removed = true;
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);

        // Act
        var result = await MoveOne(podcast.Id, episode.Id, CreateByName(organisation.Name));

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.BadRequest);
        _savedOrganisations.Should().BeEmpty();
        _savedReports.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }

    [Fact(DisplayName =
        "When several news organisations have the name the move is a conflict and nothing is written, so the curator picks the organisation by id.")]
    public async Task several_organisations_with_name_is_conflict()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        SeedOrganisation(name);
        SeedOrganisation(name);
        var podcast = SeedChannelScopedPodcast();
        var episode = SeedEpisode(podcast);

        // Act
        var result = await MoveOne(podcast.Id, episode.Id, new EpisodeNewsMoveRequest { NewsOrganisationName = name });

        // Assert
        result.Status.Should().Be(EpisodeNewsMoveStatus.Conflict);
        _savedOrganisations.Should().BeEmpty();
        _savedReports.Should().BeEmpty();
        (await _episodes.GetEpisode(podcast.Id, episode.Id)).Should().NotBeNull();
    }
}