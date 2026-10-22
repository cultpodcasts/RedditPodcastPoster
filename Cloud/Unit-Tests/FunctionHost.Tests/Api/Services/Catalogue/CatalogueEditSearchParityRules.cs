using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Catalogue;
using Api.Services.Films;
using FunctionHost.Tests.Api.Services.Films;
using Api.Services.News;
using Api.Services.TvShows;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlShortening.Services;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Catalogue;

/// <summary>The news, TV and film edit APIs re-index and drop short links the way the podcast episode edit does.</summary>
public class CatalogueEditSearchParityRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();
    private readonly List<EpisodeSearchRecord> _uploaded = [];
    private readonly List<Guid> _shortLinkDeletes = [];

    public CatalogueEditSearchParityRules()
    {
        _mocker.GetMock<IFilmRepository>().ReplaceThroughSave();
        _mocker.Use(NullLogger<NewsReportService>.Instance);
        _mocker.Use(NullLogger<TvShowEpisodeService>.Instance);
        _mocker.Use(NullLogger<FilmService>.Instance);
        _mocker.Use(NullLogger<CataloguePlayableSearchSync>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<EpisodeSearchRecord>, CancellationToken>((documents, _) => _uploaded.AddRange(documents))
            .ReturnsAsync(true);
        _mocker.GetMock<IShortnerService>()
            .Setup(s => s.Delete(It.IsAny<PodcastEpisode>()))
            .Callback<PodcastEpisode>(target => _shortLinkDeletes.Add(target.Episode.Id))
            .ReturnsAsync(new DeleteResult(true));
        _mocker.Use(_mocker.CreateInstance<CataloguePlayableSearchSync>());
    }

    private NewsReport StoreReport()
    {
        var organisation = new NewsOrganisation(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        var report = new NewsReport(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        report.SetNewsOrganisationProperties(organisation);
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .ReturnsAsync(report);
        return report;
    }

    [Fact(DisplayName =
        "Plain English rule: when a news report's title is edited, then the edit is accepted and the report's search document is pushed with the new title in the same request, as the podcast episode edit re-indexes.")]
    public async Task news_report_edit_pushes_search_document_now()
    {
        // Arrange
        var report = StoreReport();
        var title = _fixture.CreateTitle();
        var sut = _mocker.CreateInstance<NewsReportService>();

        // Act
        var result = await sut.UpdateAsync(
            new NewsReportChangeRequestWrapper(report.Id, new NewsReportChangeRequest { Title = title }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.Accepted);
        result.Outcome!.SearchIndexer!.IndexerState.Should().Be(IndexerState.Executed);
        var document = _uploaded.Should().ContainSingle().Subject;
        document.Id.Should().Be(report.Id.ToString());
        document.Title.Should().Be(title);
        _shortLinkDeletes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: when a news report is removed, then its search document and its short-link record are deleted, because a migrated report keeps the short link it had as a podcast episode.")]
    public async Task news_report_removal_deletes_search_document_and_short_link()
    {
        // Arrange
        var report = StoreReport();
        var sut = _mocker.CreateInstance<NewsReportService>();

        // Act
        var result = await sut.UpdateAsync(
            new NewsReportChangeRequestWrapper(report.Id, new NewsReportChangeRequest { Removed = true }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(NewsReportUpdateStatus.Accepted);
        _search.DeletedKeys.Should().Equal(report.Id.ToString());
        _shortLinkDeletes.Should().Equal(report.Id);
        _uploaded.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Plain English rule: when a TV episode is removed, then its search document and its short-link record are deleted, as a podcast episode removal does.")]
    public async Task tv_episode_removal_deletes_search_document_and_short_link()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        episode.SetTvShowProperties(show);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync(episode);
        var sut = _mocker.CreateInstance<TvShowEpisodeService>();

        // Act
        var result = await sut.UpdateAsync(
            new TvShowEpisodeChangeRequestWrapper(episode.Id, new TvShowEpisodeChangeRequest { Removed = true }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(TvShowEpisodeUpdateStatus.Accepted);
        _search.DeletedKeys.Should().Equal(episode.Id.ToString());
        _shortLinkDeletes.Should().Equal(episode.Id);
    }

    [Fact(DisplayName =
        "Plain English rule: when a film's description is edited, then the film's search document is pushed in the same request, as the podcast episode edit re-indexes.")]
    public async Task film_edit_pushes_search_document_now()
    {
        // Arrange
        var film = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _mocker.GetMock<IFilmRepository>().Setup(r => r.GetFilm(film.Id)).ReturnsAsync(film);
        var description = _fixture.CreateTitle();
        var sut = _mocker.CreateInstance<FilmService>();

        // Act
        var result = await sut.UpdateAsync(
            new FilmChangeRequestWrapper(film.Id, new FilmChangeRequest { Description = description }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(FilmUpdateStatus.Accepted);
        var document = _uploaded.Should().ContainSingle().Subject;
        document.ContentKind.Should().Be(SearchContentKind.Film);
        document.Description.Should().Be(description);
        _shortLinkDeletes.Should().BeEmpty();
    }
}
