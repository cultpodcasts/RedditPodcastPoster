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

/// <summary>#1368: Move a news report to a film, with the same steps, identity gate and resume rules as Move to film.</summary>
public class NewsReportFilmMoveServiceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<NewsReport> _reports = [];
    private readonly List<NewsOrganisation> _organisations = [];
    private readonly List<(Guid OrganisationId, Guid ReportId)> _reportDeletes = [];
    private readonly List<Film> _films = [];
    private readonly List<Film> _savedFilms = [];
    private readonly List<TvShowEpisode> _tvEpisodes = [];
    private readonly List<IReadOnlyList<EpisodeSearchRecord>> _uploads = [];
    private bool _searchUploadSucceeds = true;
    private Action? _afterSearchUpload;
    private readonly List<Guid> _shortLinkDeletes = [];
    private Func<DeleteResult> _shortLinkDeleteResult = () => new DeleteResult(true);
    private bool _countThrows;
    private bool _organisationReadThrowsAfterDelete;

    public NewsReportFilmMoveServiceRules()
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
        // The film service reads the organisation twice: for the publisher fields before the move, and for the
        // emptied check after the report row is deleted. The flag fails only the second read.
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _organisationReadThrowsAfterDelete && _reportDeletes.Count > 0
                ? throw new InvalidOperationException(_fixture.Create<string>())
                : _organisations.SingleOrDefault(item => item.Id == id));
        var films = _mocker.GetMock<IFilmRepository>();
        films
            .Setup(r => r.GetFilm(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _films.SingleOrDefault(film => film.Id == id));
        films
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .Returns((Expression<Func<Film, bool>> selector) => _films.Where(selector.Compile()).ToAsyncEnumerable());
        films
            .Setup(r => r.GetAllBy(
                It.IsAny<Expression<Func<Film, bool>>>(),
                It.IsAny<Expression<Func<Film, string>>>()))
            .Returns((Expression<Func<Film, bool>> selector, Expression<Func<Film, string>> projection) =>
                _films.Where(selector.Compile()).Select(projection.Compile()).ToAsyncEnumerable());
        films
            .Setup(r => r.Save(It.IsAny<Film>()))
            .Callback<Film>(film =>
            {
                _savedFilms.Add(film);
                _films.Add(film);
            })
            .Returns(Task.CompletedTask);
        _mocker.Use<IFileKeyClaimStore>(new InMemoryFileKeyClaimStore());
        _mocker.Use<IFileKeyAllocator>(_mocker.CreateInstance<FileKeyAllocator>());
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync((Expression<Func<TvShowEpisode, bool>> selector) =>
                _tvEpisodes.FirstOrDefault(selector.Compile()));
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
        // Default: no identity match (move must not depend on authority). Tests that assert fill override this.
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(e => e.ResolveFilmAsync(
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CatalogueIdentityChoice>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CatalogueIdentityAdmission.Proceed());
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(e => e.ApplyMissing(It.IsAny<Film>(), It.IsAny<CatalogueAuthorityIds?>()))
            .Returns(false);
        _mocker.GetMock<ILogger<NewsReportFilmMoveService>>();
    }

    private NewsOrganisation SeedOrganisation(Action<NewsOrganisation>? configure = null)
    {
        var organisation = new NewsOrganisation(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        configure?.Invoke(organisation);
        _organisations.Add(organisation);
        return organisation;
    }

    private NewsReport SeedReport(NewsOrganisation? organisation = null, Action<NewsReport>? configure = null)
    {
        organisation ??= SeedOrganisation();
        var podcast = _fixture.CreatePodcast();
        var report = CatalogueParentKindMapper.ToNewsReport(
            _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast), organisation);
        configure?.Invoke(report);
        _reports.Add(report);
        return report;
    }

    private Task<EpisodeFilmMoveResult> Move(
        Guid reportId,
        string? title = null,
        string? identityDecision = null,
        string? selectedIdentityKey = null) =>
        _mocker.CreateInstance<NewsReportFilmMoveService>().MoveAsync(
            new NewsReportFilmMoveCommand(reportId, new EpisodeFilmMoveRequest
            {
                Title = title,
                IdentityDecision = identityDecision,
                SelectedIdentityKey = selectedIdentityKey
            }),
            CancellationToken.None);

    [Fact(DisplayName =
        "Moving a news report to a film saves a film with the report id, title, services, YouTube id and posted flags and the organisation's publisher fields, then deletes only that report row.")]
    public async Task move_saves_film_with_report_id_and_deletes_report_row()
    {
        // Arrange
        var organisation = SeedOrganisation(item =>
        {
            item.PublisherName = _fixture.Create<string>();
            item.TwitterHandle = _fixture.Create<string>();
        });
        var report = SeedReport(organisation, item =>
        {
            item.Posted = true;
            item.OldTweeted = true;
            item.YouTubeId = _fixture.CreateYouTubeId();
        });
        var sibling = SeedReport(organisation);

        // Act
        var result = await Move(report.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        result.FilmId.Should().Be(report.Id);
        result.Name.Should().Be(report.Title.Trim());
        result.EmptiedNewsOrganisationId.Should().BeNull();
        var film = _savedFilms.Should().ContainSingle().Subject;
        film.Id.Should().Be(report.Id);
        film.Name.Should().Be(report.Title.Trim());
        film.Posted.Should().BeTrue();
        film.Tweeted.Should().BeTrue();
        film.Services.Should().BeEquivalentTo(report.Services);
        film.YouTubeId.Should().Be(report.YouTubeId);
        film.Release.Should().Be(report.Release);
        film.PublisherName.Should().Be(organisation.PublisherName);
        film.TwitterHandle.Should().Be(organisation.TwitterHandle);
        film.Removed.Should().BeNull();
        film.FileKey.Should().NotBeNullOrWhiteSpace();
        _reportDeletes.Should().ContainSingle().Which.Should().Be((organisation.Id, report.Id));
        _reports.Should().ContainSingle().Which.Id.Should().Be(sibling.Id);
    }

    [Fact(DisplayName =
        "Moving the last report of an active news organisation names it as emptied and leaves it in place, so the curator can retire it.")]
    public async Task last_report_reports_emptied_organisation()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var report = SeedReport(organisation);

        // Act
        var result = await Move(report.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        result.EmptiedNewsOrganisationId.Should().Be(organisation.Id);
        _mocker.GetMock<INewsOrganisationRepository>().Verify(r => r.Delete(It.IsAny<Guid>()), Times.Never);
        _mocker.GetMock<INewsOrganisationRepository>().Verify(r => r.Save(It.IsAny<NewsOrganisation>()), Times.Never);
    }

    [Fact(DisplayName =
        "Moving the last report out of an already removed news organisation gives an active film and names no emptied organisation, because there is nothing left to retire.")]
    public async Task removed_organisation_gives_active_film_and_no_emptied_organisation()
    {
        // Arrange
        var organisation = SeedOrganisation(item => item.Removed = true);
        var report = SeedReport(organisation);

        // Act
        var result = await Move(report.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        result.EmptiedNewsOrganisationId.Should().BeNull();
        _savedFilms.Should().ContainSingle().Which.Removed.Should().BeNull();
        _reports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A report whose news organisation is gone still moves to a film, with no publisher fields, so an orphan can be rescued.")]
    public async Task orphaned_report_still_moves()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var report = SeedReport(organisation);
        _organisations.Clear();

        // Act
        var result = await Move(report.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        result.EmptiedNewsOrganisationId.Should().BeNull();
        _savedFilms.Should().ContainSingle().Which.Id.Should().Be(report.Id);
        _reports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the remaining reports cannot be counted, the move to film is still accepted with no emptied organisation, because the move itself is already done.")]
    public async Task count_failure_does_not_fail_the_move()
    {
        // Arrange
        var report = SeedReport();
        _countThrows = true;

        // Act
        var result = await Move(report.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        result.EmptiedNewsOrganisationId.Should().BeNull();
        _reports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the news organisation cannot be read after the report row is deleted, the move to film is still accepted with no emptied organisation, as for a failed count.")]
    public async Task organisation_read_failure_does_not_fail_the_move()
    {
        // Arrange
        var report = SeedReport();
        _organisationReadThrowsAfterDelete = true;

        // Act
        var result = await Move(report.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        result.EmptiedNewsOrganisationId.Should().BeNull();
        _reports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "An unexpected error mid-move to film returns Failed with no ids and keeps the report row, so the curator can re-send.")]
    public async Task unexpected_error_keeps_report_row()
    {
        // Arrange
        var report = SeedReport();
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.Save(It.IsAny<Film>()))
            .ThrowsAsync(new InvalidOperationException(_fixture.Create<string>()));

        // Act
        var result = await Move(report.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Failed);
        result.FilmId.Should().BeNull();
        result.FailureIndexing.Should().BeFalse();
        _reports.Should().ContainSingle();
        _shortLinkDeletes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A title in the request names the film and its search document; the document keeps the report guid with ContentKind Film and no series name.")]
    public async Task title_names_film_and_search_document_is_film_shape()
    {
        // Arrange
        var report = SeedReport();
        var title = _fixture.CreateTitle();

        // Act
        var result = await Move(report.Id, $"  {title} ");

        // Assert
        result.Name.Should().Be(title);
        _savedFilms.Single().Name.Should().Be(title);
        var document = _uploads.Should().ContainSingle().Which.Should().ContainSingle().Subject;
        document.Id.Should().Be(report.Id.ToString());
        document.ContentKind.Should().Be(SearchContentKind.Film);
        document.SeriesName.Should().BeNull();
        document.Should().BeEquivalentTo(CataloguePlayableSearchDocument.For(_savedFilms.Single()));
    }

    [Theory(DisplayName =
        "A film name with '/' or a blank name is a bad request and nothing is written, because the film route uses the name as one path segment.")]
    [InlineData("a/b", "x")]
    [InlineData(null, "  ")]
    public async Task invalid_name_is_bad_request(string? title, string reportTitle)
    {
        // Arrange
        var report = SeedReport(configure: item => item.Title = reportTitle);

        // Act
        var result = await Move(report.Id, title);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.BadRequest);
        _savedFilms.Should().BeEmpty();
        _reports.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "When a film already has the name, the move is a conflict naming that film, and nothing is written.")]
    public async Task name_clash_is_conflict()
    {
        // Arrange
        var report = SeedReport();
        var existing = new Film(report.Title.Trim()) { Id = _fixture.CreateGuid() };
        _films.Add(existing);

        // Act
        var result = await Move(report.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Conflict);
        result.FilmId.Should().Be(existing.Id);
        _savedFilms.Should().BeEmpty();
        _reports.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "An ambiguous IMDb/TMDB match returns the candidates and writes nothing, and a cancel answer is Cancelled with nothing written, as Move to film for an episode.")]
    public async Task ambiguous_identity_and_cancel_write_nothing()
    {
        // Arrange
        var report = SeedReport();
        var title = report.Title.Trim();
        var hold = new CatalogueIdentityHold(
            CatalogueAuthorityCandidate.FilmKind,
            title,
            [
                new CatalogueAuthorityCandidate(
                    CatalogueAuthorityCandidate.TmdbKey(1),
                    title,
                    Year: null,
                    CatalogueAuthorityCandidate.FilmKind,
                    CatalogueAuthorityCandidate.TmdbSource,
                    1,
                    TvdbId: null)
            ]);
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(e => e.ResolveFilmAsync(
                title, It.IsAny<int?>(), CatalogueIdentityChoice.Unset, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CatalogueIdentityAdmission.NeedsPrompt(hold));

        // Act
        var ambiguous = await Move(report.Id);
        var cancelled = await Move(report.Id, identityDecision: "cancel");

        // Assert
        ambiguous.Status.Should().Be(EpisodeFilmMoveStatus.IdentityDecisionRequired);
        ambiguous.IdentityHold.Should().BeSameAs(hold);
        cancelled.Status.Should().Be(EpisodeFilmMoveStatus.Cancelled);
        _savedFilms.Should().BeEmpty();
        _uploads.Should().BeEmpty();
        _reports.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "Move to film applies the selected IMDb/TMDB candidate on re-POST after an ambiguous prompt, then completes the move.")]
    public async Task selected_identity_is_applied()
    {
        // Arrange
        var report = SeedReport();
        var title = report.Title.Trim();
        var selectedKey = CatalogueAuthorityCandidate.TmdbKey(_fixture.CreateTmdbId());
        var imdb = _fixture.CreateImdbId();
        var tmdb = _fixture.CreateTmdbId();
        var ids = new CatalogueAuthorityIds(imdb, tmdb, TvdbId: null);
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(e => e.ResolveFilmAsync(
                title, It.IsAny<int?>(), CatalogueIdentityChoice.Select, selectedKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CatalogueIdentityAdmission.Proceed(ids));
        _mocker.GetMock<ICatalogueIdentityEnricher>()
            .Setup(e => e.ApplyMissing(It.IsAny<Film>(), ids))
            .Callback<Film, CatalogueAuthorityIds?>((doc, _) =>
            {
                doc.ImdbId = imdb;
                doc.TmdbId = tmdb;
            })
            .Returns(true);

        // Act
        var result = await Move(report.Id, identityDecision: "select", selectedIdentityKey: selectedKey);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        var film = _savedFilms.Should().ContainSingle().Subject;
        film.ImdbId.Should().Be(imdb);
        film.TmdbId.Should().Be(tmdb);
        _reports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A removed news report cannot be moved to a film and the search swap receives no upload.")]
    public async Task removed_report_is_bad_request()
    {
        // Arrange
        var report = SeedReport(configure: item => item.Removed = true);

        // Act
        var result = await Move(report.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.BadRequest);
        _uploads.Should().BeEmpty();
        _reports.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "When a TV episode already has the report id (an interrupted move to TV show), the move is a conflict and nothing is written, because one id cannot be both.")]
    public async Task tv_episode_with_report_id_is_conflict()
    {
        // Arrange
        var report = SeedReport();
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _tvEpisodes.Add(CatalogueParentKindMapper.ToTvShowEpisode(report, show));

        // Act
        var result = await Move(report.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Conflict);
        _savedFilms.Should().BeEmpty();
        _reports.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "An unknown news report with no film of that id is not found.")]
    public async Task unknown_report_is_not_found()
    {
        // Act
        var result = await Move(_fixture.CreateGuid());

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.NotFound);
        _savedFilms.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the search swap fails, the report row and its short link are kept and the result is Failed with failureIndexing; a re-send reuses the film (saved once) and finishes the move.")]
    public async Task search_failure_keeps_report_and_resend_resumes()
    {
        // Arrange
        var report = SeedReport();
        _searchUploadSucceeds = false;

        // Act
        var failed = await Move(report.Id);
        var reportsAfterFailure = _reports.Count;
        var shortLinksAfterFailure = _shortLinkDeletes.Count;
        _searchUploadSucceeds = true;
        var resumed = await Move(report.Id);

        // Assert
        failed.Status.Should().Be(EpisodeFilmMoveStatus.Failed);
        failed.FailureIndexing.Should().BeTrue();
        failed.FilmId.Should().Be(report.Id);
        reportsAfterFailure.Should().Be(1);
        shortLinksAfterFailure.Should().Be(0);
        resumed.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        _savedFilms.Should().ContainSingle();
        _uploads.Should().HaveCount(2);
        _reports.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Re-sending after a completed move is accepted for the film without writing again, so a double click or a lost response is not an error.")]
    public async Task resend_after_completed_move_is_accepted()
    {
        // Arrange
        var report = SeedReport();
        await Move(report.Id);

        // Act
        var result = await Move(report.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        result.FilmId.Should().Be(report.Id);
        _savedFilms.Should().ContainSingle();
        _uploads.Should().ContainSingle();
    }

    [Fact(DisplayName =
        "When the film is deleted after the search swap, the report row is kept and the result is Failed, because deleting it then would lose the report.")]
    public async Task film_deleted_after_swap_keeps_report_row()
    {
        // Arrange
        var report = SeedReport();
        _afterSearchUpload = () => _films.Clear();

        // Act
        var result = await Move(report.Id);

        // Assert
        result.Status.Should().Be(EpisodeFilmMoveStatus.Failed);
        _reports.Should().ContainSingle();
        _shortLinkDeletes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "After a move the report's short-link record is deleted, and a short-link delete failure does not fail the move.")]
    public async Task short_link_is_deleted_best_effort()
    {
        // Arrange
        var report = SeedReport();
        var failing = SeedReport();

        // Act
        var deleted = await Move(report.Id);
        _shortLinkDeleteResult = () => throw new InvalidOperationException(_fixture.Create<string>());
        var failed = await Move(failing.Id);

        // Assert
        deleted.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        failed.Status.Should().Be(EpisodeFilmMoveStatus.Accepted);
        _shortLinkDeletes.Should().Equal(report.Id, failing.Id);
        _reports.Should().BeEmpty();
    }
}
