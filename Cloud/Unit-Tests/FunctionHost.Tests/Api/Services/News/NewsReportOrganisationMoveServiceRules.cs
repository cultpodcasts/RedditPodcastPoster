using System.Linq.Expressions;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.News;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlShortening.Services;
using Xunit;

namespace FunctionHost.Tests.Api.Services.News;

/// <summary>
/// #1397: moving news reports between news organisations (same ids), with the search swap, short-link delete, resume
/// rules and the optional YouTube channel move.
/// </summary>
public class NewsReportOrganisationMoveServiceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    // Stored rows keyed by (organisation id, report id), held as JSON so a write never aliases the caller's object.
    private readonly Dictionary<(Guid OrganisationId, Guid ReportId), string> _stored = [];
    private readonly List<NewsOrganisation> _organisations = [];
    private readonly List<NewsOrganisation> _savedOrganisations = [];
    private readonly List<(Guid OrganisationId, Guid ReportId)> _reportDeletes = [];
    private readonly List<TvShowEpisode> _tvEpisodes = [];
    private readonly List<Film> _films = [];
    private readonly List<(Guid OrganisationId, DateTime LatestReleased)> _raises = [];
    private readonly List<IReadOnlyList<EpisodeSearchRecord>> _uploads = [];
    private readonly List<IReadOnlyList<Guid>> _searchDeletes = [];
    private readonly List<Guid> _shortLinkDeletes = [];
    private readonly HashSet<Guid> _organisationSaveThrowsFor = [];
    private readonly HashSet<Guid> _reportDeleteThrowsFor = [];
    private readonly InMemoryParentIndexingLockStore _locks = new();

    // The service saves and deletes in parallel, so the fake store is guarded.
    private readonly object _gate = new();
    private bool _searchUploadSucceeds = true;
    private bool _organisationSaveThrows;

    public NewsReportOrganisationMoveServiceRules()
    {
        var reports = _mocker.GetMock<INewsReportRepository>();
        reports
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .Returns((Expression<Func<NewsReport, bool>> selector) =>
                StoredReports().Where(selector.Compile()).ToAsyncEnumerable());
        reports
            .Setup(r => r.GetByNewsOrganisationId(It.IsAny<Guid>()))
            .Returns((Guid organisationId) =>
                StoredReports().Where(report => report.NewsOrganisationId == organisationId).ToAsyncEnumerable());
        reports
            .Setup(r => r.GetNewsReport(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((Guid organisationId, Guid reportId) =>
                _stored.TryGetValue((organisationId, reportId), out var json) ? Read(json) : null);
        reports
            .Setup(r => r.Save(It.IsAny<NewsReport>()))
            .Callback<NewsReport>(Store)
            .Returns(Task.CompletedTask);
        reports
            .Setup(r => r.Delete(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Callback<Guid, Guid>((organisationId, reportId) =>
            {
                lock (_gate)
                {
                    if (_reportDeleteThrowsFor.Remove(reportId))
                    {
                        throw new InvalidOperationException(reportId.ToString());
                    }

                    _reportDeletes.Add((organisationId, reportId));
                    _stored.Remove((organisationId, reportId));
                }
            })
            .Returns(Task.CompletedTask);
        reports
            .Setup(r => r.Count(It.IsAny<Guid>()))
            .ReturnsAsync((Guid organisationId) => _stored.Keys.Count(key => key.OrganisationId == organisationId));
        var organisations = _mocker.GetMock<INewsOrganisationRepository>();
        organisations
            .Setup(r => r.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _organisations.SingleOrDefault(item => item.Id == id));
        organisations
            .Setup(r => r.Save(It.IsAny<NewsOrganisation>()))
            .Callback<NewsOrganisation>(organisation =>
            {
                if (_organisationSaveThrows || _organisationSaveThrowsFor.Contains(organisation.Id))
                {
                    throw new InvalidOperationException(_fixture.Create<string>());
                }

                _savedOrganisations.Add(organisation);
            })
            .Returns(Task.CompletedTask);
        organisations
            .Setup(r => r.RaiseLatestReleased(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .Callback<Guid, DateTime>((id, released) => _raises.Add((id, released)))
            .ReturnsAsync(true);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .Returns((Expression<Func<TvShowEpisode, bool>> selector) =>
                _tvEpisodes.Where(selector.Compile()).ToAsyncEnumerable());
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .Returns((Expression<Func<Film, bool>> selector) => _films.Where(selector.Compile()).ToAsyncEnumerable());
        var swap = _mocker.GetMock<IPlayableSearchDocumentSwap>();
        swap
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<EpisodeSearchRecord>, CancellationToken>((documents, _) => _uploads.Add(documents))
            .ReturnsAsync(() => _searchUploadSucceeds);
        swap
            .Setup(s => s.DeleteAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<Guid>, CancellationToken>((ids, _) => _searchDeletes.Add(ids))
            .ReturnsAsync(true);
        _mocker.GetMock<IShortnerService>()
            .Setup(s => s.Delete(It.IsAny<PodcastEpisode>()))
            .Callback<PodcastEpisode>(target => _shortLinkDeletes.Add(target.Episode.Id))
            .ReturnsAsync(new DeleteResult(true));
        _mocker.Use<IParentIndexingLockStore>(_locks);
        _mocker.Use(TimeProvider.System);
        _mocker.GetMock<ILogger<NewsReportOrganisationMoveService>>();
    }

    private static NewsReport Read(string json) => JsonSerializer.Deserialize<NewsReport>(json)!;

    private void Store(NewsReport report)
    {
        var json = JsonSerializer.Serialize(report);
        lock (_gate)
        {
            _stored[(report.NewsOrganisationId, report.Id)] = json;
        }
    }

    private List<NewsReport> StoredReports() => _stored.Values.Select(Read).ToList();

    private List<NewsReport> ReportsIn(NewsOrganisation organisation) =>
        StoredReports().Where(report => report.NewsOrganisationId == organisation.Id).ToList();

    private NewsOrganisation CreateOrganisation(Action<NewsOrganisation>? configure = null)
    {
        var organisation = new NewsOrganisation(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        configure?.Invoke(organisation);
        _organisations.Add(organisation);
        return organisation;
    }

    private NewsReport SeedReport(NewsOrganisation organisation, Action<NewsReport>? configure = null)
    {
        var report = CatalogueParentKindMapper.ToNewsReport(
            _fixture.CreateStoredEpisodeWithYouTubeOnly(_fixture.CreatePodcast()), organisation);
        configure?.Invoke(report);
        Store(report);
        return report;
    }

    private Task<NewsReportOrganisationMoveResult> Move(
        NewsOrganisation source,
        NewsOrganisation target,
        IReadOnlyList<Guid>? reportIds = null,
        bool moveYouTubeChannels = false) =>
        MoveBetween(source.Id, target.Id, reportIds, moveYouTubeChannels);

    private Task<NewsReportOrganisationMoveResult> MoveBetween(
        Guid sourceId,
        Guid targetId,
        IReadOnlyList<Guid>? reportIds = null,
        bool moveYouTubeChannels = false) =>
        _mocker.CreateInstance<NewsReportOrganisationMoveService>().MoveAsync(
            new NewsReportOrganisationMoveCommand(sourceId, targetId, reportIds, moveYouTubeChannels),
            CancellationToken.None);

    [Fact(DisplayName =
        "Moving named reports writes each into the target with the same id, title, services and posted state and the target's id and name, deletes only those source rows, and leaves the source's other reports where they are.")]
    public async Task named_reports_move_into_the_target_with_the_same_ids()
    {
        // Arrange
        var source = CreateOrganisation();
        var target = CreateOrganisation();
        var report = SeedReport(source, item =>
        {
            item.Posted = true;
            item.YouTubeId = _fixture.CreateYouTubeId();
        });
        var stays = SeedReport(source);

        // Act
        var result = await Move(source, target, [report.Id]);

        // Assert
        result.Status.Should().Be(NewsReportOrganisationMoveStatus.Accepted);
        result.MovedReportIds.Should().Equal(report.Id);
        result.TargetNewsOrganisationName.Should().Be(target.Name);
        var moved = ReportsIn(target).Should().ContainSingle().Subject;
        moved.Id.Should().Be(report.Id);
        moved.NewsOrganisationName.Should().Be(target.Name);
        moved.Title.Should().Be(report.Title);
        moved.Posted.Should().BeTrue();
        moved.YouTubeId.Should().Be(report.YouTubeId);
        moved.Services.Should().BeEquivalentTo(report.Services);
        _reportDeletes.Should().Equal((source.Id, report.Id));
        ReportsIn(source).Should().ContainSingle().Which.Id.Should().Be(stays.Id);
        result.RemainingReportCount.Should().Be(1);
        result.EmptiedNewsOrganisationId.Should().BeNull();
    }

    [Fact(DisplayName =
        "A moved report's search document is re-uploaded under the same id with the target's name as series, and its short-link record is deleted, so search and short links point at the target's page.")]
    public async Task search_document_and_short_link_follow_the_target()
    {
        // Arrange
        var source = CreateOrganisation();
        var target = CreateOrganisation();
        var report = SeedReport(source);

        // Act
        await Move(source, target, [report.Id]);

        // Assert
        var document = _uploads.Should().ContainSingle().Which.Should().ContainSingle().Subject;
        document.Id.Should().Be(report.Id.ToString());
        document.SeriesName.Should().Be(target.Name);
        _shortLinkDeletes.Should().Equal(report.Id);
        _raises.Should().ContainSingle().Which.OrganisationId.Should().Be(target.Id);
    }

    [Fact(DisplayName =
        "With no report ids every report of the source moves, removed ones included, and the emptied source is named so it can be retired, because re-sending until empty is how a whole organisation is folded in.")]
    public async Task omitted_ids_move_every_report_and_name_the_emptied_source()
    {
        // Arrange
        var source = CreateOrganisation();
        var target = CreateOrganisation();
        var first = SeedReport(source);
        var removed = SeedReport(source, item => item.Removed = true);

        // Act
        var result = await Move(source, target);

        // Assert
        result.Status.Should().Be(NewsReportOrganisationMoveStatus.Accepted);
        result.MovedReportIds.Should().BeEquivalentTo([first.Id, removed.Id]);
        ReportsIn(source).Should().BeEmpty();
        ReportsIn(target).Should().ContainSingle(item => item.Id == removed.Id).Which.Removed.Should().BeTrue();
        result.RemainingReportCount.Should().Be(0);
        result.EmptiedNewsOrganisationId.Should().Be(source.Id);
        _mocker.GetMock<INewsOrganisationRepository>().Verify(r => r.Delete(It.IsAny<Guid>()), Times.Never);
    }

    [Fact(DisplayName =
        "A removed report keeps removed in the target and its search document is deleted rather than uploaded, because removed items never reach search.")]
    public async Task removed_report_is_deleted_from_search()
    {
        // Arrange
        var source = CreateOrganisation();
        var target = CreateOrganisation();
        var removed = SeedReport(source, item => item.Removed = true);

        // Act
        await Move(source, target, [removed.Id]);

        // Assert
        _uploads.Should().BeEmpty();
        _searchDeletes.Should().ContainSingle().Which.Should().Equal(removed.Id);
    }

    [Fact(DisplayName =
        "An id that is a report of a third organisation refuses the whole request with 409 and writes nothing, so a report is never pulled out of an organisation the request did not name.")]
    public async Task report_of_a_third_organisation_is_a_conflict()
    {
        // Arrange
        var source = CreateOrganisation();
        var target = CreateOrganisation();
        var other = SeedReport(CreateOrganisation());
        var mine = SeedReport(source);

        // Act
        var result = await Move(source, target, [mine.Id, other.Id]);

        // Assert
        result.Status.Should().Be(NewsReportOrganisationMoveStatus.Conflict);
        result.ConflictReportIds.Should().Equal(other.Id);
        ReportsIn(target).Should().BeEmpty();
        _reportDeletes.Should().BeEmpty();
        _uploads.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "An id that is a TV episode or a film refuses the request with 409, because ids are unique across playables.")]
    public async Task tv_episode_or_film_id_is_a_conflict()
    {
        // Arrange
        var source = CreateOrganisation();
        var target = CreateOrganisation();
        var report = SeedReport(source);
        _films.Add(new Film(_fixture.CreateTitle()) { Id = report.Id });

        // Act
        var result = await Move(source, target, [report.Id]);

        // Assert
        result.Status.Should().Be(NewsReportOrganisationMoveStatus.Conflict);
        ReportsIn(source).Should().ContainSingle();
        ReportsIn(target).Should().BeEmpty();
    }

    [Fact(DisplayName =
        "An id that is not a report of the source refuses the whole request with 404 and writes nothing.")]
    public async Task unknown_id_is_not_found()
    {
        // Arrange
        var source = CreateOrganisation();
        var target = CreateOrganisation();
        var report = SeedReport(source);
        var unknown = _fixture.CreateGuid();

        // Act
        var result = await Move(source, target, [report.Id, unknown]);

        // Assert
        result.Status.Should().Be(NewsReportOrganisationMoveStatus.NotFound);
        result.NotFoundReportIds.Should().Equal(unknown);
        ReportsIn(source).Should().ContainSingle();
        _reportDeletes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A request with no target, the source as target, or a removed target is a 400, and a missing source or target is a 404; none writes anything.")]
    public async Task invalid_targets_are_refused()
    {
        // Arrange
        var source = CreateOrganisation();
        var removedTarget = CreateOrganisation(item => item.Removed = true);
        SeedReport(source);

        // Act
        var noTarget = await MoveBetween(source.Id, Guid.Empty);
        var self = await Move(source, source);
        var removed = await Move(source, removedTarget);
        var missingTarget = await MoveBetween(source.Id, _fixture.CreateGuid());
        var missingSource = await MoveBetween(_fixture.CreateGuid(), removedTarget.Id);

        // Assert
        noTarget.Status.Should().Be(NewsReportOrganisationMoveStatus.BadRequest);
        self.Status.Should().Be(NewsReportOrganisationMoveStatus.BadRequest);
        removed.Status.Should().Be(NewsReportOrganisationMoveStatus.BadRequest);
        missingTarget.Status.Should().Be(NewsReportOrganisationMoveStatus.NotFound);
        missingSource.Status.Should().Be(NewsReportOrganisationMoveStatus.NotFound);
        ReportsIn(source).Should().ContainSingle();
        _reportDeletes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the search swap fails the move is Failed with failureIndexing and the source rows are kept, so a re-send resumes.")]
    public async Task search_failure_keeps_the_source_rows()
    {
        // Arrange
        var source = CreateOrganisation();
        var target = CreateOrganisation();
        var report = SeedReport(source);
        _searchUploadSucceeds = false;

        // Act
        var result = await Move(source, target, [report.Id]);

        // Assert
        result.Status.Should().Be(NewsReportOrganisationMoveStatus.Failed);
        result.FailureIndexing.Should().BeTrue();
        ReportsIn(source).Should().ContainSingle().Which.Id.Should().Be(report.Id);
        _reportDeletes.Should().BeEmpty();
        _shortLinkDeletes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Re-sending resumes: a report already in the target whose source row is gone is reported as already moved, and one in both (an interrupted move) is rewritten from the source and its source row deleted.")]
    public async Task resend_resumes_an_interrupted_move()
    {
        // Arrange
        var source = CreateOrganisation();
        var target = CreateOrganisation();
        var done = SeedReport(target);
        var interrupted = SeedReport(source);
        var copy = Read(JsonSerializer.Serialize(interrupted));
        copy.SetNewsOrganisationProperties(target);
        Store(copy);

        // Act
        var result = await Move(source, target, [done.Id, interrupted.Id]);

        // Assert
        result.Status.Should().Be(NewsReportOrganisationMoveStatus.Accepted);
        result.AlreadyMovedReportIds.Should().Equal(done.Id);
        result.MovedReportIds.Should().Equal(interrupted.Id);
        ReportsIn(source).Should().BeEmpty();
        ReportsIn(target).Select(item => item.Id).Should().BeEquivalentTo([done.Id, interrupted.Id]);
    }

    [Fact(DisplayName =
        "With moveYouTubeChannels the source's main channel and playlist and its additional channels become additional channels of the target, and the source is saved with no channel before the target is saved with them, so no channel is ever claimed twice.")]
    public async Task youtube_channels_move_to_the_target_additional_channels()
    {
        // Arrange
        var mainChannel = _fixture.CreateYouTubeChannelId();
        var playlist = _fixture.Create<string>();
        var extraChannel = _fixture.CreateYouTubeChannelId();
        var source = CreateOrganisation(item =>
        {
            item.Indexing = new CatalogueIndexingSource
            {
                YouTubeChannelId = mainChannel,
                YouTubePlaylistId = playlist,
                YouTubePlaylistQueryIsExpensive = true
            };
            item.AdditionalYouTubeChannels = [AdditionalYouTubeChannel.ChannelWide(extraChannel)];
        });
        var target = CreateOrganisation(item =>
            item.Indexing = new CatalogueIndexingSource { YouTubeChannelId = _fixture.CreateYouTubeChannelId() });
        SeedReport(source);

        // Act
        var result = await Move(source, target, moveYouTubeChannels: true);

        // Assert
        result.Status.Should().Be(NewsReportOrganisationMoveStatus.Accepted);
        result.MovedYouTubeChannelIds.Should().Equal(mainChannel, extraChannel);
        _savedOrganisations.Select(item => item.Id).Should().Equal(source.Id, target.Id);
        source.Indexing!.YouTubeChannelId.Should().BeNull();
        source.Indexing.YouTubePlaylistId.Should().BeNull();
        source.AdditionalYouTubeChannels.Should().BeNull();
        target.AdditionalYouTubeChannels.Should().HaveCount(2);
        var moved = target.AdditionalYouTubeChannelFor(mainChannel)!;
        moved.YouTubePlaylistId.Should().Be(playlist);
        moved.YouTubePlaylistQueryIsExpensive.Should().BeTrue();
        target.HoldsAdditionalYouTubeChannel(extraChannel).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A channel the target already holds is not added twice, so a re-send after a partial channel move finishes it cleanly.")]
    public async Task channel_already_on_the_target_is_not_duplicated()
    {
        // Arrange
        var channel = _fixture.CreateYouTubeChannelId();
        var source = CreateOrganisation(item =>
            item.Indexing = new CatalogueIndexingSource { YouTubeChannelId = channel });
        var target = CreateOrganisation(item =>
            item.AdditionalYouTubeChannels = [AdditionalYouTubeChannel.ChannelWide(channel)]);

        // Act
        var result = await Move(source, target, moveYouTubeChannels: true);

        // Assert
        result.Status.Should().Be(NewsReportOrganisationMoveStatus.Accepted);
        target.AdditionalYouTubeChannels.Should().ContainSingle();
        _savedOrganisations.Select(item => item.Id).Should().Equal(source.Id);
        source.Indexing!.YouTubeChannelId.Should().BeNull();
    }

    [Fact(DisplayName =
        "Without moveYouTubeChannels neither organisation is written, so a report move never changes what either organisation indexes.")]
    public async Task without_the_flag_no_organisation_is_written()
    {
        // Arrange
        var source = CreateOrganisation(item =>
            item.Indexing = new CatalogueIndexingSource { YouTubeChannelId = _fixture.CreateYouTubeChannelId() });
        var target = CreateOrganisation();
        SeedReport(source);

        // Act
        await Move(source, target);

        // Assert
        _savedOrganisations.Should().BeEmpty();
        source.Indexing!.YouTubeChannelId.Should().NotBeNull();
    }

    [Fact(DisplayName =
        "When the channel move fails after the reports moved, the request is still accepted with youTubeChannelMoveFailed and an error, because the report move is done and a re-send retries the channels.")]
    public async Task channel_move_failure_does_not_fail_the_report_move()
    {
        // Arrange
        var source = CreateOrganisation(item =>
            item.Indexing = new CatalogueIndexingSource { YouTubeChannelId = _fixture.CreateYouTubeChannelId() });
        var target = CreateOrganisation();
        var report = SeedReport(source);
        _organisationSaveThrows = true;

        // Act
        var result = await Move(source, target, moveYouTubeChannels: true);

        // Assert
        result.Status.Should().Be(NewsReportOrganisationMoveStatus.Accepted);
        result.MovedReportIds.Should().Equal(report.Id);
        result.YouTubeChannelMoveFailed.Should().BeTrue();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName =
        "An explicit empty reportIds, more than 500 ids, or an empty guid is a 400 and nothing is written; only an omitted list means the next batch.")]
    public async Task bad_report_id_lists_are_refused()
    {
        // Arrange
        var source = CreateOrganisation();
        var target = CreateOrganisation();
        SeedReport(source);
        var tooMany = Enumerable.Range(0, NewsReportOrganisationMoveLimits.MaxReportsPerRequest + 1)
            .Select(_ => _fixture.CreateGuid())
            .ToList();

        // Act
        var empty = await Move(source, target, []);
        var overLimit = await Move(source, target, tooMany);
        var emptyGuid = await Move(source, target, [Guid.Empty]);

        // Assert
        empty.Status.Should().Be(NewsReportOrganisationMoveStatus.BadRequest);
        overLimit.Status.Should().Be(NewsReportOrganisationMoveStatus.BadRequest);
        emptyGuid.Status.Should().Be(NewsReportOrganisationMoveStatus.BadRequest);
        ReportsIn(source).Should().ContainSingle();
        ReportsIn(target).Should().BeEmpty();
        _uploads.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a source-row delete fails part-way the result is Failed and lists only the reports that really moved, and a re-send finishes the move and clears the short links and raises latestReleased for the reports the first attempt already moved.")]
    public async Task partial_source_delete_reports_truthfully_and_a_resend_converges()
    {
        // Arrange
        var source = CreateOrganisation();
        var target = CreateOrganisation();
        var first = SeedReport(source);
        var stuck = SeedReport(source);
        _reportDeleteThrowsFor.Add(stuck.Id);

        // Act
        var failed = await Move(source, target, [first.Id, stuck.Id]);
        _shortLinkDeletes.Clear();
        _raises.Clear();
        var resent = await Move(source, target, [first.Id, stuck.Id]);

        // Assert
        failed.Status.Should().Be(NewsReportOrganisationMoveStatus.Failed);
        failed.MovedReportIds.Should().Equal(first.Id);
        failed.Error.Should().Contain("1 of 2");
        failed.RemainingReportCount.Should().Be(1);
        resent.Status.Should().Be(NewsReportOrganisationMoveStatus.Accepted);
        resent.MovedReportIds.Should().Equal(stuck.Id);
        resent.AlreadyMovedReportIds.Should().Equal(first.Id);
        _shortLinkDeletes.Should().BeEquivalentTo([first.Id, stuck.Id]);
        _raises.Should().ContainSingle().Which.OrganisationId.Should().Be(target.Id);
        ReportsIn(source).Should().BeEmpty();
    }

    [Fact(DisplayName =
        "latestReleased is patched on the target to the newest moved report's release, and not patched when the target already has a newer one, so it is never lowered.")]
    public async Task latest_released_is_raised_to_the_newest_moved_report_and_never_lowered()
    {
        // Arrange
        var source = CreateOrganisation();
        var target = CreateOrganisation();
        var older = SeedReport(source);
        var newer = SeedReport(source);
        var newest = new[] { older, newer }.Max(report => report.ReleaseSort);
        var laterSource = CreateOrganisation();
        var laterReport = SeedReport(laterSource);
        var newerTarget = CreateOrganisation(item => item.LatestReleased = laterReport.ReleaseSort.AddDays(1));

        // Act
        var first = await Move(source, target, [older.Id, newer.Id]);
        var raisesAfterFirst = _raises.ToList();
        await Move(laterSource, newerTarget, [laterReport.Id]);

        // Assert
        first.Status.Should().Be(NewsReportOrganisationMoveStatus.Accepted, first.Error);
        target.LatestReleased.Should().BeNull();
        newest.Should().NotBe(default);
        raisesAfterFirst.Should().Equal((target.Id, newest));
        _raises.Should().Equal((target.Id, newest));
    }

    [Fact(DisplayName =
        "A report whose YouTube video the target already has as a different report refuses the request with 409 and writes nothing, so one organisation never ends up with two reports for one video.")]
    public async Task duplicate_video_in_the_target_is_a_conflict()
    {
        // Arrange
        var source = CreateOrganisation();
        var target = CreateOrganisation();
        var videoId = _fixture.CreateYouTubeId();
        var report = SeedReport(source, item => item.YouTubeId = videoId);
        SeedReport(target, item => item.YouTubeId = videoId);

        // Act
        var result = await Move(source, target, [report.Id]);

        // Assert
        result.Status.Should().Be(NewsReportOrganisationMoveStatus.Conflict);
        result.ConflictReportIds.Should().Equal(report.Id);
        ReportsIn(source).Should().ContainSingle().Which.Id.Should().Be(report.Id);
        _uploads.Should().BeEmpty();
        _reportDeletes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "While either organisation is being indexed (its indexing lease is held) the move is refused with 409 and nothing is written, and the move releases its own leases when done.")]
    public async Task move_is_refused_while_an_organisation_is_being_indexed()
    {
        // Arrange
        var source = CreateOrganisation();
        var target = CreateOrganisation();
        var report = SeedReport(source);
        var held = await _locks.TryAcquire(target.Id, DateTime.UtcNow, TimeSpan.FromMinutes(5),
            ParentIndexingLock.SourceHourly);

        // Act
        var refused = await Move(source, target, [report.Id]);
        await _locks.Release(held.Handle!);
        var accepted = await Move(source, target, [report.Id]);

        // Assert
        refused.Status.Should().Be(NewsReportOrganisationMoveStatus.Conflict);
        refused.Error.Should().Contain("being indexed");
        accepted.Status.Should().Be(NewsReportOrganisationMoveStatus.Accepted);
        (await _locks.TryGet(source.Id)).Should().BeNull();
        (await _locks.TryGet(target.Id)).Should().BeNull();
    }

    [Fact(DisplayName =
        "When saving the channels on the target fails, the source's channels are restored and saved back, the result says to re-send, and no channel is lost.")]
    public async Task failed_target_save_restores_the_source_channels()
    {
        // Arrange
        var mainChannel = _fixture.CreateYouTubeChannelId();
        var playlist = _fixture.Create<string>();
        var extraChannel = _fixture.CreateYouTubeChannelId();
        var source = CreateOrganisation(item =>
        {
            item.Indexing = new CatalogueIndexingSource { YouTubeChannelId = mainChannel, YouTubePlaylistId = playlist };
            item.AdditionalYouTubeChannels = [AdditionalYouTubeChannel.ChannelWide(extraChannel)];
        });
        var target = CreateOrganisation();
        _organisationSaveThrowsFor.Add(target.Id);

        // Act
        var result = await Move(source, target, moveYouTubeChannels: true);

        // Assert
        result.Status.Should().Be(NewsReportOrganisationMoveStatus.Accepted);
        result.YouTubeChannelMoveFailed.Should().BeTrue();
        result.Error.Should().Be(NewsReportOrganisationMoveService.ChannelMoveRetryError);
        result.MovedYouTubeChannelIds.Should().BeEmpty();
        source.Indexing!.YouTubeChannelId.Should().Be(mainChannel);
        source.Indexing.YouTubePlaylistId.Should().Be(playlist);
        source.HoldsAdditionalYouTubeChannel(extraChannel).Should().BeTrue();
        _savedOrganisations.Select(item => item.Id).Should().Equal(source.Id, source.Id);
    }

    [Fact(DisplayName =
        "A source main channel that indexes a playlist owned by another channel is not moved (the target would index the whole channel); no organisation is written and the error points to the organisation PATCH.")]
    public async Task main_channel_with_a_foreign_playlist_is_refused()
    {
        // Arrange
        var mainChannel = _fixture.CreateYouTubeChannelId();
        var source = CreateOrganisation(item => item.Indexing = new CatalogueIndexingSource
        {
            YouTubeChannelId = mainChannel,
            YouTubePlaylistId = _fixture.Create<string>(),
            YouTubePlaylistOwnerChannelId = _fixture.CreateYouTubeChannelId()
        });
        var target = CreateOrganisation();
        var report = SeedReport(source);

        // Act
        var result = await Move(source, target, moveYouTubeChannels: true);

        // Assert
        result.Status.Should().Be(NewsReportOrganisationMoveStatus.Accepted);
        result.MovedReportIds.Should().Equal(report.Id);
        result.YouTubeChannelMoveFailed.Should().BeTrue();
        result.Error.Should().Be(NewsReportOrganisationMoveService.ForeignPlaylistError);
        _savedOrganisations.Should().BeEmpty();
        source.Indexing!.YouTubeChannelId.Should().Be(mainChannel);
    }

    [Fact(DisplayName =
        "movedYouTubeChannelIds lists only the channels added to the target, not ones it already held.")]
    public async Task moved_channel_ids_are_only_the_ones_added()
    {
        // Arrange
        var held = _fixture.CreateYouTubeChannelId();
        var added = _fixture.CreateYouTubeChannelId();
        var source = CreateOrganisation(item =>
        {
            item.Indexing = new CatalogueIndexingSource { YouTubeChannelId = held };
            item.AdditionalYouTubeChannels = [AdditionalYouTubeChannel.ChannelWide(added)];
        });
        var target = CreateOrganisation(item =>
            item.AdditionalYouTubeChannels = [AdditionalYouTubeChannel.ChannelWide(held)]);

        // Act
        var result = await Move(source, target, moveYouTubeChannels: true);

        // Assert
        result.MovedYouTubeChannelIds.Should().Equal(added);
        target.AdditionalYouTubeChannels.Should().HaveCount(2);
    }
}