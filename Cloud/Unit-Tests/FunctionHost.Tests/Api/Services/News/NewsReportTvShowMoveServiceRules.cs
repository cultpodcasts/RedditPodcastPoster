using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.News;
using RedditPodcastPoster.Catalogue.Authority;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Persistence.FileKeys;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlShortening.Services;
using Xunit;

namespace FunctionHost.Tests.Api.Services.News;

/// <summary>#1368: Move a news report to a TV show, with the same steps and resume rules as Move to TV show (#1108).</summary>
public class NewsReportTvShowMoveServiceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<NewsReport> _reports = [];
    private readonly List<(Guid OrganisationId, Guid ReportId)> _reportDeletes = [];
    private readonly List<TvShow> _shows = [];
    private readonly List<TvShow> _savedShows = [];
    private readonly List<TvShowEpisode> _tvEpisodes = [];
    private readonly List<TvShowEpisode> _savedTvEpisodes = [];
    private readonly List<Film> _films = [];
    private readonly List<(Guid ShowId, DateTime LatestReleased)> _raises = [];
    private readonly List<IReadOnlyList<EpisodeSearchRecord>> _uploads = [];
    private bool _searchUploadSucceeds = true;
    private Action? _afterSearchUpload;
    private readonly List<Guid> _shortLinkDeletes = [];
    private Func<DeleteResult> _shortLinkDeleteResult = () => new DeleteResult(true);
    private bool _countThrows;

    public NewsReportTvShowMoveServiceRules()
    {
        var reports = _mocker.GetMock<INewsReportRepository>();
        reports
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .ReturnsAsync((Expression<Func<NewsReport, bool>> selector) => _reports.FirstOrDefault(selector.Compile()));
        reports
            .Setup(r => r.Delete(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .Callback<Guid, Guid>((organisationId, reportId) =>
            {
                _reportDeletes.Add((organisationId, reportId));
                _reports.RemoveAll(item => item.NewsOrganisationId == organisationId && item.Id == reportId);
            })
            .Returns(Task.CompletedTask);
        reports
            .Setup(r => r.Count(It.IsAny<Guid>()))
            .ReturnsAsync((Guid organisationId) => _countThrows
                ? throw new InvalidOperationException(_fixture.Create<string>())
                : _reports.Count(item => item.NewsOrganisationId == organisationId));
        var shows = _mocker.GetMock<ITvShowRepository>();
        shows
            .Setup(r => r.GetTvShow(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _shows.SingleOrDefault(show => show.Id == id));
        shows
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns((Expression<Func<TvShow, bool>> selector) => _shows.Where(selector.Compile()).ToAsyncEnumerable());
        shows
            .Setup(r => r.GetAllBy(
                It.IsAny<Expression<Func<TvShow, bool>>>(),
                It.IsAny<Expression<Func<TvShow, string>>>()))
            .Returns((Expression<Func<TvShow, bool>> selector, Expression<Func<TvShow, string>> projection) =>
                _shows.Where(selector.Compile()).Select(projection.Compile()).ToAsyncEnumerable());
        _mocker.Use<IFileKeyClaimStore>(new InMemoryFileKeyClaimStore());
        _mocker.Use<IFileKeyAllocator>(_mocker.CreateInstance<FileKeyAllocator>());
        shows
            .Setup(r => r.Save(It.IsAny<TvShow>()))
            .Callback<TvShow>(show =>
            {
                _savedShows.Add(show);
                _shows.Add(show);
            })
            .Returns(Task.CompletedTask);
        shows
            .Setup(r => r.RaiseLatestReleased(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .Callback<Guid, DateTime>((id, released) => _raises.Add((id, released)))
            .ReturnsAsync((Guid id, DateTime _) => _shows.Any(show => show.Id == id));
        var tvEpisodes = _mocker.GetMock<ITvShowEpisodeRepository>();
        tvEpisodes
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync((Expression<Func<TvShowEpisode, bool>> selector) =>
                _tvEpisodes.FirstOrDefault(selector.Compile()));
        tvEpisodes
            .Setup(r => r.GetTvShowEpisode(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((Guid showId, Guid id) =>
                _tvEpisodes.SingleOrDefault(item => item.TvShowId == showId && item.Id == id));
        tvEpisodes
            .Setup(r => r.Save(It.IsAny<TvShowEpisode>()))
            .Callback<TvShowEpisode>(item =>
            {
                _savedTvEpisodes.Add(item);
                _tvEpisodes.Add(item);
            })
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _films.SingleOrDefault(film => film.Id == id));
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(e => e.ResolveTvShowAsync(
                It.IsAny<string>(),
                It.IsAny<CatalogueIdentityChoice>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CatalogueIdentityAdmission.Proceed());
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(e => e.ApplyMissing(It.IsAny<ITvCanonical>(), It.IsAny<CatalogueAuthorityIds?>()))
            .Returns(false);
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<EpisodeSearchRecord>, CancellationToken>((documents, _) =>
            {
                _uploads.Add(documents);
                _afterSearchUpload?.Invoke();
            })
            .ReturnsAsync(() => _searchUploadSucceeds);
        _mocker.GetMock<IShortnerService>()
            .Setup(s => s.Delete(It.IsAny<PodcastEpisode>()))
            .Callback<PodcastEpisode>(target => _shortLinkDeletes.Add(target.Episode.Id))
            .ReturnsAsync(() => _shortLinkDeleteResult());
        _mocker.GetMock<ILogger<NewsReportTvShowMoveService>>();
    }

    private TvShow SeedShow(Action<TvShow>? configure = null)
    {
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        configure?.Invoke(show);
        _shows.Add(show);
        return show;
    }

    private NewsOrganisation CreateOrganisation() =>
        new(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };

    private NewsReport SeedReport(NewsOrganisation? organisation = null, Action<NewsReport>? configure = null)
    {
        organisation ??= CreateOrganisation();
        var podcast = _fixture.CreatePodcast();
        var report = CatalogueParentKindMapper.ToNewsReport(
            _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast), organisation);
        configure?.Invoke(report);
        _reports.Add(report);
        return report;
    }

    private Task<EpisodeTvShowMoveResult> Move(Guid reportId, EpisodeTvShowMoveRequest request) =>
        _mocker.CreateInstance<NewsReportTvShowMoveService>().MoveAsync(
            new NewsReportTvShowMoveCommand(reportId, request),
            CancellationToken.None);

    private Task<EpisodeTvShowMoveResult> MoveToShow(Guid reportId, Guid showId, string? title = null) =>
        Move(reportId, new EpisodeTvShowMoveRequest { TvShowId = showId, Title = title });

    [Fact(DisplayName =
        "Moving a news report into an existing TV show saves a TV episode with the report id, the show's id and name, title, services, YouTube id and posted flags, then deletes only that report row.")]
    public async Task move_to_existing_show_saves_tv_episode_and_deletes_report_row()
    {
        // Arrange
        var show = SeedShow();
        var organisation = CreateOrganisation();
        var report = SeedReport(organisation, item =>
        {
            item.Posted = true;
            item.OldTweeted = true;
            item.YouTubeId = _fixture.CreateYouTubeId();
        });
        var sibling = SeedReport(organisation);

        // Act
        var result = await MoveToShow(report.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Accepted);
        result.TvShowId.Should().Be(show.Id);
        result.TvShowName.Should().Be(show.Name);
        result.EpisodeId.Should().Be(report.Id);
        result.CreatedShow.Should().BeFalse();
        var tvEpisode = _savedTvEpisodes.Should().ContainSingle().Subject;
        tvEpisode.Id.Should().Be(report.Id);
        tvEpisode.TvShowId.Should().Be(show.Id);
        tvEpisode.TvShowName.Should().Be(show.Name);
        tvEpisode.Title.Should().Be(report.Title);
        tvEpisode.Posted.Should().BeTrue();
        tvEpisode.Tweeted.Should().BeTrue();
        tvEpisode.Services.Should().BeEquivalentTo(report.Services);
        tvEpisode.YouTubeId.Should().Be(report.YouTubeId);
        tvEpisode.Release.Should().Be(report.Release);
        _savedShows.Should().BeEmpty();
        _reportDeletes.Should().ContainSingle().Which.Should().Be((organisation.Id, report.Id));
        _reports.Should().ContainSingle().Which.Id.Should().Be(sibling.Id);
    }

    [Fact(DisplayName =
        "A move that leaves the news organisation with reports names no emptied organisation, and one that moves its last report names it, so the curator is offered to retire only an empty organisation.")]
    public async Task emptied_organisation_is_reported_only_when_no_reports_remain()
    {
        // Arrange
        var show = SeedShow();
        var organisation = CreateOrganisation();
        var first = SeedReport(organisation);
        var last = SeedReport(organisation);

        // Act
        var firstResult = await MoveToShow(first.Id, show.Id);
        var lastResult = await MoveToShow(last.Id, show.Id);

        // Assert
        firstResult.EmptiedNewsOrganisationId.Should().BeNull();
        lastResult.Status.Should().Be(EpisodeTvShowMoveStatus.Accepted);
        lastResult.EmptiedNewsOrganisationId.Should().Be(organisation.Id);
        _mocker.GetMock<INewsOrganisationRepository>().Verify(r => r.Delete(It.IsAny<Guid>()), Times.Never);
        _mocker.GetMock<INewsOrganisationRepository>().Verify(r => r.Save(It.IsAny<NewsOrganisation>()), Times.Never);
    }

    [Fact(DisplayName =
        "When the remaining reports cannot be counted, the move is still accepted with no emptied organisation, because the move itself is already done.")]
    public async Task count_failure_does_not_fail_the_move()
    {
        // Arrange
        var show = SeedShow();
        var report = SeedReport();
        _countThrows = true;

        // Act
        var result = await MoveToShow(report.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Accepted);
        result.EmptiedNewsOrganisationId.Should().BeNull();
        _reports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "createShow with a new name creates a show with a fresh id, the publisher, no indexing and the report's release as latestReleased, the same as Move to TV show for an episode.")]
    public async Task create_show_has_fresh_id_publisher_and_no_indexing()
    {
        // Arrange
        var organisation = CreateOrganisation();
        var report = SeedReport(organisation);
        var name = _fixture.CreateTitle();
        var publisher = _fixture.Create<string>();

        // Act
        var result = await Move(report.Id,
            new EpisodeTvShowMoveRequest { TvShowName = name, CreateShow = true, Publisher = publisher });

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Accepted);
        result.CreatedShow.Should().BeTrue();
        var show = _savedShows.Should().ContainSingle().Subject;
        show.Id.Should().NotBe(organisation.Id);
        show.Id.Should().NotBe(Guid.Empty);
        show.Name.Should().Be(name);
        show.PublisherName.Should().Be(publisher);
        show.Indexing.Should().BeNull();
        show.LatestReleased.Should().Be(_savedTvEpisodes.Single().ReleaseSort);
        result.TvShowId.Should().Be(show.Id);
        _savedTvEpisodes.Should().ContainSingle().Which.TvShowId.Should().Be(show.Id);
        _reports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A TV show name without createShow joins the one show with that name case-insensitively, and an unknown name is not found with nothing written, so a typo never creates a show.")]
    public async Task name_joins_existing_show_and_unknown_name_is_not_found()
    {
        // Arrange
        var show = SeedShow();
        var joined = SeedReport();
        var typo = SeedReport();

        // Act
        var joinedResult = await Move(joined.Id,
            new EpisodeTvShowMoveRequest { TvShowName = $"  {show.Name.ToUpperInvariant()} " });
        var typoResult = await Move(typo.Id, new EpisodeTvShowMoveRequest { TvShowName = _fixture.CreateTitle() });

        // Assert
        joinedResult.Status.Should().Be(EpisodeTvShowMoveStatus.Accepted);
        joinedResult.TvShowId.Should().Be(show.Id);
        typoResult.Status.Should().Be(EpisodeTvShowMoveStatus.NotFound);
        _savedShows.Should().BeEmpty();
        _savedTvEpisodes.Should().ContainSingle().Which.Id.Should().Be(joined.Id);
        _reports.Should().ContainSingle().Which.Id.Should().Be(typo.Id);
    }

    [Fact(DisplayName =
        "When several TV shows have the name, the move is a conflict and nothing is written, because the target would be a guess.")]
    public async Task several_shows_with_the_name_is_a_conflict()
    {
        // Arrange
        var show = SeedShow();
        SeedShow(item => item.Name = show.Name);
        var report = SeedReport();

        // Act
        var result = await Move(report.Id, new EpisodeTvShowMoveRequest { TvShowName = show.Name });

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Conflict);
        _savedTvEpisodes.Should().BeEmpty();
        _reports.Should().ContainSingle();
    }

    [Theory(DisplayName =
        "An invalid target is a bad request and nothing is read or written: exactly one of tvShowId or tvShowName, createShow only by name, publisher only with createShow, and no '/' in a show name.")]
    [InlineData(false, false, false, false, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(false, true, false, true, false)]
    [InlineData(false, true, false, false, true)]
    public async Task invalid_target_is_bad_request(
        bool withId,
        bool withName,
        bool createShow,
        bool publisher,
        bool slash)
    {
        // Arrange
        var report = SeedReport();
        var request = new EpisodeTvShowMoveRequest
        {
            TvShowId = withId ? _fixture.CreateGuid() : null,
            TvShowName = withName ? (slash ? "a/b" : _fixture.CreateTitle()) : null,
            CreateShow = createShow,
            Publisher = publisher ? _fixture.Create<string>() : null
        };

        // Act
        var result = await Move(report.Id, request);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.BadRequest);
        _mocker.GetMock<INewsReportRepository>()
            .Verify(r => r.GetBy(It.IsAny<Expression<Func<NewsReport, bool>>>()), Times.Never);
        _savedTvEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "An unknown news report with no TV episode of that id, or an unknown TV show id, is not found and nothing is written.")]
    public async Task unknown_report_or_show_is_not_found()
    {
        // Arrange
        var show = SeedShow();
        var report = SeedReport();

        // Act
        var unknownReport = await MoveToShow(_fixture.CreateGuid(), show.Id);
        var unknownShow = await MoveToShow(report.Id, _fixture.CreateGuid());

        // Assert
        unknownReport.Status.Should().Be(EpisodeTvShowMoveStatus.NotFound);
        unknownShow.Status.Should().Be(EpisodeTvShowMoveStatus.NotFound);
        _savedTvEpisodes.Should().BeEmpty();
        _reports.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "A removed news report cannot be moved to a TV show and the search swap receives no upload, as for a removed episode.")]
    public async Task removed_report_is_bad_request()
    {
        // Arrange
        var show = SeedShow();
        var report = SeedReport(configure: item => item.Removed = true);

        // Act
        var result = await MoveToShow(report.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.BadRequest);
        _uploads.Should().BeEmpty();
        _reports.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "A removed TV show cannot take a news report, because the report would vanish from the site with it.")]
    public async Task removed_show_is_bad_request()
    {
        // Arrange
        var show = SeedShow(item => item.Removed = true);
        var report = SeedReport();

        // Act
        var result = await MoveToShow(report.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.BadRequest);
        _savedTvEpisodes.Should().BeEmpty();
        _reports.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "A title in the request names the TV episode and its search document instead of the report title.")]
    public async Task title_overrides_report_title()
    {
        // Arrange
        var show = SeedShow();
        var report = SeedReport();
        var title = _fixture.CreateTitle();

        // Act
        await MoveToShow(report.Id, show.Id, $" {title} ");

        // Assert
        _savedTvEpisodes.Should().ContainSingle().Which.Title.Should().Be(title);
        _uploads.Should().ContainSingle().Which.Should().ContainSingle().Which.Title.Should().Be(title);
    }

    [Fact(DisplayName =
        "The search document keeps the report guid, switches ContentKind to TvShowEpisode with the show as series name, and is the shape the TV episode edit writes.")]
    public async Task search_swap_uses_same_guid_and_tv_shape()
    {
        // Arrange
        var show = SeedShow();
        var report = SeedReport();

        // Act
        await MoveToShow(report.Id, show.Id);

        // Assert
        var document = _uploads.Should().ContainSingle().Which.Should().ContainSingle().Subject;
        document.Id.Should().Be(report.Id.ToString());
        document.ContentKind.Should().Be(SearchContentKind.TvShowEpisode);
        document.SeriesName.Should().Be(show.Name);
        document.Should().BeEquivalentTo(CataloguePlayableSearchDocument.For(_savedTvEpisodes.Single()));
    }

    [Fact(DisplayName =
        "When the search swap fails, the report row and its short link are kept and the result is Failed with failureIndexing, so the source is never lost before search points at the TV episode.")]
    public async Task search_failure_keeps_report_row()
    {
        // Arrange
        var show = SeedShow();
        var report = SeedReport();
        _searchUploadSucceeds = false;

        // Act
        var result = await MoveToShow(report.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Failed);
        result.FailureIndexing.Should().BeTrue();
        result.TvShowId.Should().Be(show.Id);
        _reports.Should().ContainSingle();
        _reportDeletes.Should().BeEmpty();
        _shortLinkDeletes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Re-sending after a failed search swap resumes: the TV episode with the report id is reused, not saved again, then search is swapped and the report row is deleted.")]
    public async Task resend_after_search_failure_resumes()
    {
        // Arrange
        var show = SeedShow();
        var report = SeedReport();
        _searchUploadSucceeds = false;
        await MoveToShow(report.Id, show.Id);
        _searchUploadSucceeds = true;

        // Act
        var result = await MoveToShow(report.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Accepted);
        _savedTvEpisodes.Should().ContainSingle();
        _uploads.Should().HaveCount(2);
        _reports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Re-sending a create after a failed search swap joins the show the first attempt created, so an interrupted move never makes two shows.")]
    public async Task resend_create_after_search_failure_joins_created_show()
    {
        // Arrange
        var report = SeedReport();
        var request = new EpisodeTvShowMoveRequest
            { TvShowName = _fixture.CreateTitle(), CreateShow = true, Publisher = _fixture.Create<string>() };
        _searchUploadSucceeds = false;
        await Move(report.Id, request);
        _searchUploadSucceeds = true;

        // Act
        var result = await Move(report.Id, request);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Accepted);
        _savedShows.Should().ContainSingle();
        result.TvShowId.Should().Be(_savedShows.Single().Id);
        _reports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Re-sending after a completed move is accepted for the same show without writing again, and with a different show is a conflict naming the show the report is in.")]
    public async Task resend_after_completed_move()
    {
        // Arrange
        var show = SeedShow();
        var other = SeedShow();
        var report = SeedReport();
        await MoveToShow(report.Id, show.Id);
        var uploads = _uploads.Count;

        // Act
        var same = await MoveToShow(report.Id, show.Id);
        var different = await MoveToShow(report.Id, other.Id);

        // Assert
        same.Status.Should().Be(EpisodeTvShowMoveStatus.Accepted);
        same.TvShowId.Should().Be(show.Id);
        same.EpisodeId.Should().Be(report.Id);
        different.Status.Should().Be(EpisodeTvShowMoveStatus.Conflict);
        different.TvShowId.Should().Be(show.Id);
        _savedTvEpisodes.Should().ContainSingle();
        _uploads.Should().HaveCount(uploads);
    }

    [Fact(DisplayName =
        "A re-send by name after the show was renamed between attempts matches the show's current name, so the move resumes instead of a false conflict.")]
    public async Task resend_by_name_matches_current_show_name()
    {
        // Arrange
        var show = SeedShow();
        var report = SeedReport();
        _searchUploadSucceeds = false;
        await Move(report.Id, new EpisodeTvShowMoveRequest { TvShowName = show.Name });
        _searchUploadSucceeds = true;
        show.Name = _fixture.CreateTitle();

        // Act
        var result = await Move(report.Id, new EpisodeTvShowMoveRequest { TvShowName = show.Name });

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Accepted);
        _savedTvEpisodes.Should().ContainSingle();
        _reports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the report id is already a TV episode of another show while the report row exists, the move is a conflict and the report row is kept.")]
    public async Task existing_tv_episode_in_other_show_is_conflict()
    {
        // Arrange
        var show = SeedShow();
        var other = SeedShow();
        var report = SeedReport();
        _tvEpisodes.Add(CatalogueParentKindMapper.ToTvShowEpisode(report, other));

        // Act
        var result = await MoveToShow(report.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Conflict);
        result.TvShowId.Should().Be(other.Id);
        _savedTvEpisodes.Should().BeEmpty();
        _reports.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "When a film already has the report id (an interrupted move to film), the move is a conflict and nothing is written, because one id cannot be both.")]
    public async Task film_with_report_id_is_conflict()
    {
        // Arrange
        var show = SeedShow();
        var report = SeedReport();
        _films.Add(new Film(report.Title) { Id = report.Id });

        // Act
        var result = await MoveToShow(report.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Conflict);
        _savedTvEpisodes.Should().BeEmpty();
        _reports.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "Resuming an interrupted move into a show that was removed since is a bad request with the report row kept, the same as a new move into a removed show.")]
    public async Task resume_into_removed_show_is_bad_request()
    {
        // Arrange
        var show = SeedShow();
        var report = SeedReport();
        _searchUploadSucceeds = false;
        await MoveToShow(report.Id, show.Id);
        _searchUploadSucceeds = true;
        show.Removed = true;

        // Act
        var result = await MoveToShow(report.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.BadRequest);
        _reports.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "When the show is deleted after the search swap, the report row is kept and the result is Failed, because deleting it then would lose the report.")]
    public async Task show_deleted_after_swap_keeps_report_row()
    {
        // Arrange
        var show = SeedShow();
        var report = SeedReport();
        _afterSearchUpload = () => _shows.Remove(show);

        // Act
        var result = await MoveToShow(report.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Failed);
        _reports.Should().ContainSingle();
        _shortLinkDeletes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the new TV episode is deleted after the search swap, the report row is kept and the result is Failed.")]
    public async Task tv_episode_deleted_after_swap_keeps_report_row()
    {
        // Arrange
        var show = SeedShow();
        var report = SeedReport();
        _afterSearchUpload = () => _tvEpisodes.Clear();

        // Act
        var result = await MoveToShow(report.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Failed);
        _reports.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "A move of a report newer than the show's latest release raises only latestReleased with the conditional patch; an older report leaves the show untouched.")]
    public async Task latest_released_is_raised_only_when_newer()
    {
        // Arrange
        var newer = SeedReport();
        var older = SeedReport();
        var show = SeedShow(item => item.LatestReleased = newer.ReleaseSort.AddDays(-1));
        var olderShow = SeedShow(item => item.LatestReleased = older.ReleaseSort.AddDays(1));

        // Act
        await MoveToShow(newer.Id, show.Id);
        await MoveToShow(older.Id, olderShow.Id);

        // Assert
        _raises.Should().ContainSingle().Which.Should().Be((show.Id, newer.ReleaseSort));
        _mocker.GetMock<ITvShowRepository>().Verify(r => r.Save(It.IsAny<TvShow>()), Times.Never);
    }

    [Fact(DisplayName =
        "After a move the report's short-link record is deleted, and a short-link delete failure does not fail the move, because the TV episode, search and report row are already done.")]
    public async Task short_link_is_deleted_best_effort()
    {
        // Arrange
        var show = SeedShow();
        var report = SeedReport();
        var failing = SeedReport();

        // Act
        var deleted = await MoveToShow(report.Id, show.Id);
        _shortLinkDeleteResult = () => throw new InvalidOperationException(_fixture.Create<string>());
        var failed = await MoveToShow(failing.Id, show.Id);

        // Assert
        deleted.Status.Should().Be(EpisodeTvShowMoveStatus.Accepted);
        failed.Status.Should().Be(EpisodeTvShowMoveStatus.Accepted);
        _shortLinkDeletes.Should().Equal(report.Id, failing.Id);
        _reports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "An unexpected error mid-move returns Failed with no ids and keeps the report row, so the curator can re-send.")]
    public async Task unexpected_error_keeps_report_row()
    {
        // Arrange
        var show = SeedShow();
        var report = SeedReport();
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<TvShowEpisode>()))
            .ThrowsAsync(new InvalidOperationException(_fixture.Create<string>()));

        // Act
        var result = await MoveToShow(report.Id, show.Id);

        // Assert
        result.Status.Should().Be(EpisodeTvShowMoveStatus.Failed);
        result.TvShowId.Should().BeNull();
        _reports.Should().ContainSingle();
    }
}
