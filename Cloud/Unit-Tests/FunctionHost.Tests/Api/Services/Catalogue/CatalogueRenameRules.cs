using System.Linq.Expressions;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Catalogue;
using RedditPodcastPoster.CloudflareRedirect.Models;
using RedditPodcastPoster.CloudflareRedirect.Services;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using Xunit;
using SearchDocument = Azure.Search.Documents.Models.SearchDocument;

namespace FunctionHost.Tests.Api.Services.Catalogue;

public class CatalogueRenameRules
{
    private const string OldName = "Synthetic Evening News";
    private const string NewName = "Synthetic Night News";

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _cleanup = new();
    private readonly Mock<SearchClient> _searchClient = new();
    private readonly List<(string Field, string Value, string[] Keys)> _merges = [];
    private readonly Dictionary<string, int> _mergeStatus = [];
    private readonly List<CatalogueRedirect> _redirects = [];
    private readonly List<string> _saveOrder = [];

    private readonly List<NewsOrganisation> _organisations = [];
    private readonly List<NewsReport> _reports = [];
    private readonly List<TvShow> _shows = [];
    private readonly List<TvShowEpisode> _episodes = [];
    private readonly List<Film> _films = [];

    public CatalogueRenameRules()
    {
        _mocker.Use(NullLogger<CatalogueRenameService>.Instance);
        _mocker.Use(_cleanup.Cleanup);
        _searchClient
            .Setup(c => c.MergeDocumentsAsync(
                It.IsAny<IEnumerable<SearchDocument>>(), It.IsAny<IndexDocumentsOptions>(), It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<SearchDocument>, IndexDocumentsOptions, CancellationToken>((documents, _, _) =>
            {
                var docs = documents.ToArray();
                var field = docs[0].Keys.Single(k => k != "id");
                _merges.Add((field, (string)docs[0][field], docs.Select(d => (string)d["id"]).ToArray()));
                var results = docs.Select(d =>
                {
                    var key = (string)d["id"];
                    var status = _mergeStatus.GetValueOrDefault(key, 200);
                    return SearchModelFactory.IndexingResult(key, status == 200 ? null : "synthetic", status == 200, status);
                }).ToList();
                return Task.FromResult(Response.FromValue(SearchModelFactory.IndexDocumentsResult(results),
                    Mock.Of<Response>()));
            });
        _mocker.Use(new PlayableSearchRename(_searchClient.Object, NullLogger<PlayableSearchRename>.Instance));

        _mocker.GetMock<IRedirectService>()
            .Setup(r => r.CreateCatalogueRedirect(It.IsAny<CatalogueRedirect>()))
            .Callback<CatalogueRedirect>(redirect =>
            {
                _saveOrder.Add("redirect");
                _redirects.Add(redirect);
            })
            .ReturnsAsync(true);

        Filterable<INewsOrganisationRepository, NewsOrganisation>(_organisations);
        Filterable<ITvShowRepository, TvShow>(_shows);
        Filterable<IFilmRepository, Film>(_films);

        var organisations = _mocker.GetMock<INewsOrganisationRepository>();
        organisations.Setup(r => r.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _organisations.SingleOrDefault(x => x.Id == id));
        organisations.Setup(r => r.Save(It.IsAny<NewsOrganisation>()))
            .Callback<NewsOrganisation>(_ => _saveOrder.Add("parent")).Returns(Task.CompletedTask);
        var reports = _mocker.GetMock<INewsReportRepository>();
        reports.Setup(r => r.GetByNewsOrganisationId(It.IsAny<Guid>()))
            .Returns((Guid id) => _reports.Where(x => x.NewsOrganisationId == id).ToAsyncEnumerable());
        reports.Setup(r => r.Save(It.IsAny<IEnumerable<NewsReport>>()))
            .Callback<IEnumerable<NewsReport>>(batch => _saveOrder.Add($"children:{batch.Count()}"))
            .Returns(Task.CompletedTask);

        var shows = _mocker.GetMock<ITvShowRepository>();
        shows.Setup(r => r.GetTvShow(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _shows.SingleOrDefault(x => x.Id == id));
        shows.Setup(r => r.Save(It.IsAny<TvShow>()))
            .Callback<TvShow>(_ => _saveOrder.Add("parent")).Returns(Task.CompletedTask);
        var episodes = _mocker.GetMock<ITvShowEpisodeRepository>();
        episodes.Setup(r => r.GetByTvShowId(It.IsAny<Guid>()))
            .Returns((Guid id) => _episodes.Where(x => x.TvShowId == id).ToAsyncEnumerable());
        episodes.Setup(r => r.Save(It.IsAny<IEnumerable<TvShowEpisode>>()))
            .Callback<IEnumerable<TvShowEpisode>>(batch => _saveOrder.Add($"children:{batch.Count()}"))
            .Returns(Task.CompletedTask);

        var films = _mocker.GetMock<IFilmRepository>();
        films.Setup(r => r.GetFilm(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _films.SingleOrDefault(x => x.Id == id));
        films.Setup(r => r.Save(It.IsAny<Film>()))
            .Callback<Film>(_ => _saveOrder.Add("parent")).Returns(Task.CompletedTask);
    }

    private void Filterable<TRepository, TEntity>(List<TEntity> store)
        where TRepository : class, IFilterableRepository<TEntity> =>
        _mocker.GetMock<TRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<TEntity, bool>>>()))
            .Returns((Expression<Func<TEntity, bool>> selector) => store.Where(selector.Compile()).ToAsyncEnumerable());

    private CatalogueRenameService Service => _mocker.CreateInstance<CatalogueRenameService>();

    private NewsOrganisation AddOrganisation(string name = OldName)
    {
        var organisation = new NewsOrganisation(name) { Id = _fixture.CreateGuid() };
        _organisations.Add(organisation);
        return organisation;
    }

    private NewsReport AddReport(NewsOrganisation organisation, bool removed = false)
    {
        var report = new NewsReport(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Removed = removed };
        report.SetNewsOrganisationProperties(organisation);
        _reports.Add(report);
        return report;
    }

    private TvShow AddShow(string name)
    {
        var show = new TvShow(name) { Id = _fixture.CreateGuid() };
        _shows.Add(show);
        return show;
    }

    private TvShowEpisode AddEpisode(TvShow show)
    {
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        episode.SetTvShowProperties(show);
        _episodes.Add(episode);
        return episode;
    }

    private Film AddFilm(string name)
    {
        var film = new Film(name) { Id = _fixture.CreateGuid() };
        _films.Add(film);
        return film;
    }

    [Fact(DisplayName =
        "Renaming a news organisation records the redirect news/{old} -> {new}, copies the name onto every report " +
        "(children saved before the parent), patches seriesName on the reports' search documents and keeps the fileKey, " +
        "because that is what a podcast rename does for its episodes.")]
    public async Task news_organisation_rename_redirects_and_propagates()
    {
        // Arrange
        var organisation = AddOrganisation();
        var fileKey = organisation.FileKey;
        var live = AddReport(organisation);
        var removed = AddReport(organisation, removed: true);

        // Act
        var result = await Service.RenameNewsOrganisation(
            new CatalogueRenameCommand(organisation.Id.ToString(), $"  {NewName} "), CancellationToken.None);

        // Assert
        result.Status.Should().Be(CatalogueRenameStatus.Ok);
        result.SearchIndexer!.IndexerState.Should().Be(IndexerState.Executed);
        _redirects.Should().ContainSingle().Which.Should()
            .Be(new CatalogueRedirect(CatalogueRedirectKind.NewsOrganisation, OldName, NewName));
        _redirects[0].Key.Should().Be($"news/{OldName}");
        organisation.Name.Should().Be(NewName);
        organisation.FileKey.Should().Be(fileKey);
        live.NewsOrganisationName.Should().Be(NewName);
        removed.NewsOrganisationName.Should().Be(NewName);
        removed.Removed.Should().BeTrue();
        _saveOrder.Should().Equal("redirect", "children:2", "parent");
        _merges.Should().ContainSingle();
        _merges[0].Field.Should().Be("seriesName");
        _merges[0].Value.Should().Be(NewName);
        _merges[0].Keys.Should().BeEquivalentTo([live.Id.ToString(), removed.Id.ToString()]);
    }

    [Fact(DisplayName =
        "Renaming a TV show by its percent-encoded route name (any case) records tv/{old} -> {new}, copies the name onto every episode's " +
        "tvShowName and patches seriesName in search.")]
    public async Task tv_show_rename_by_name()
    {
        // Arrange
        var show = AddShow("Synthetic Drama");
        var episode = AddEpisode(show);

        // Act
        var result = await Service.RenameTvShow(
            new CatalogueRenameCommand("synthetic%20drama", "Synthetic Drama Revisited"), CancellationToken.None);

        // Assert
        result.Status.Should().Be(CatalogueRenameStatus.Ok);
        _redirects.Single().Key.Should().Be("tv/Synthetic Drama");
        _redirects.Single().NewName.Should().Be("Synthetic Drama Revisited");
        show.Name.Should().Be("Synthetic Drama Revisited");
        episode.TvShowName.Should().Be("Synthetic Drama Revisited");
        _merges.Single().Field.Should().Be("seriesName");
        _merges.Single().Keys.Should().Equal(episode.Id.ToString());
    }

    [Fact(DisplayName =
        "Renaming a film records film/{old} -> {new} and patches only its own search title, because a film is standalone.")]
    public async Task film_rename_patches_title()
    {
        // Arrange
        var film = AddFilm("Synthetic Feature");

        // Act
        var result = await Service.RenameFilm(
            new CatalogueRenameCommand(film.Id.ToString(), "Synthetic Feature: Director's Cut"), CancellationToken.None);

        // Assert
        result.Status.Should().Be(CatalogueRenameStatus.Ok);
        _redirects.Single().Key.Should().Be("film/Synthetic Feature");
        film.Name.Should().Be("Synthetic Feature: Director's Cut");
        _saveOrder.Should().Equal("redirect", "parent");
        _merges.Single().Field.Should().Be("title");
        _merges.Single().Keys.Should().Equal(film.Id.ToString());
    }

    [Fact(DisplayName =
        "A new name already used by another entity of the same kind (any case) is a 409 with no redirect and no save, like the podcast rename.")]
    public async Task clash_is_conflict()
    {
        // Arrange
        var organisation = AddOrganisation();
        AddOrganisation(NewName.ToUpperInvariant());
        AddReport(organisation);

        // Act
        var result = await Service.RenameNewsOrganisation(
            new CatalogueRenameCommand(organisation.Id.ToString(), NewName), CancellationToken.None);

        // Assert
        result.Status.Should().Be(CatalogueRenameStatus.Conflict);
        result.AmbiguousIds.Should().BeNull();
        _redirects.Should().BeEmpty();
        _saveOrder.Should().BeEmpty();
        organisation.Name.Should().Be(OldName);
    }

    [Fact(DisplayName =
        "A name clash with a different kind is not a conflict: a TV show may take a news organisation's or a podcast's name.")]
    public async Task clash_is_per_kind()
    {
        // Arrange
        AddOrganisation(NewName);
        var show = AddShow(OldName);

        // Act
        var result = await Service.RenameTvShow(
            new CatalogueRenameCommand(show.Id.ToString(), NewName), CancellationToken.None);

        // Assert
        result.Status.Should().Be(CatalogueRenameStatus.Ok);
        show.Name.Should().Be(NewName);
    }

    [Fact(DisplayName =
        "An ambiguous name identifier is a 409 carrying the candidate ids (as the kind's GET does); an unknown one is a 404.")]
    public async Task ambiguous_and_missing_identifiers()
    {
        // Arrange
        var first = AddFilm("Synthetic Remake");
        var second = AddFilm("Synthetic Remake");

        // Act
        var ambiguous = await Service.RenameFilm(
            new CatalogueRenameCommand("Synthetic%20Remake", "Synthetic Remake 1999"), CancellationToken.None);
        var missing = await Service.RenameFilm(
            new CatalogueRenameCommand(_fixture.CreateGuid().ToString(), "Anything"), CancellationToken.None);

        // Assert
        ambiguous.Status.Should().Be(CatalogueRenameStatus.Conflict);
        ambiguous.AmbiguousIds.Should().BeEquivalentTo([first.Id, second.Id]);
        missing.Status.Should().Be(CatalogueRenameStatus.NotFound);
        _redirects.Should().BeEmpty();
        _saveOrder.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Renaming one of two films that share a title by guid renames it but writes no redirect, because the old URL still belongs to the other film.")]
    public async Task homonym_keeps_old_url()
    {
        // Arrange
        var renamed = AddFilm("Synthetic Remake");
        var other = AddFilm("Synthetic Remake");

        // Act
        var result = await Service.RenameFilm(
            new CatalogueRenameCommand(renamed.Id.ToString(), "Synthetic Remake 1999"), CancellationToken.None);

        // Assert
        result.Status.Should().Be(CatalogueRenameStatus.Ok);
        renamed.Name.Should().Be("Synthetic Remake 1999");
        other.Name.Should().Be("Synthetic Remake");
        _redirects.Should().BeEmpty();
        _saveOrder.Should().Equal("parent");
    }

    [Theory(DisplayName =
        "A new name with '/' is InvalidName (500, like the podcast rename) and a blank one is BadRequest; neither changes anything.")]
    [InlineData("Synthetic/News", CatalogueRenameStatus.InvalidName)]
    [InlineData("   ", CatalogueRenameStatus.BadRequest)]
    public async Task invalid_names(string newName, CatalogueRenameStatus expected)
    {
        // Arrange
        var organisation = AddOrganisation();

        // Act
        var result = await Service.RenameNewsOrganisation(
            new CatalogueRenameCommand(organisation.Id.ToString(), newName), CancellationToken.None);

        // Assert
        result.Status.Should().Be(expected);
        _redirects.Should().BeEmpty();
        _saveOrder.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the redirect loop guard refuses, the result is BadRequest and nothing is saved, like the podcast rename.")]
    public async Task unsafe_redirect_is_bad_request()
    {
        // Arrange
        var show = AddShow(OldName);
        AddEpisode(show);
        _mocker.GetMock<IRedirectService>()
            .Setup(r => r.CreateCatalogueRedirect(It.IsAny<CatalogueRedirect>()))
            .ReturnsAsync(false);

        // Act
        var result = await Service.RenameTvShow(
            new CatalogueRenameCommand(show.Id.ToString(), NewName), CancellationToken.None);

        // Assert
        result.Status.Should().Be(CatalogueRenameStatus.BadRequest);
        show.Name.Should().Be(OldName);
        _saveOrder.Should().BeEmpty();
        _merges.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "If saving the children fails the parent is not saved, and retrying the same request finishes the rename, " +
        "saving only the children that are still stale.")]
    public async Task retry_after_partial_failure()
    {
        // Arrange
        var organisation = AddOrganisation();
        var reports = Enumerable.Range(0, 3).Select(_ => AddReport(organisation)).ToArray();
        var repository = _mocker.GetMock<INewsReportRepository>();
        repository.Setup(r => r.Save(It.IsAny<IEnumerable<NewsReport>>()))
            .ThrowsAsync(new InvalidOperationException("synthetic Cosmos failure"));

        // Act
        var first = await Service.RenameNewsOrganisation(
            new CatalogueRenameCommand(organisation.Id.ToString(), NewName), CancellationToken.None);
        var parentSavedAfterFailure = _saveOrder.Contains("parent");
        // The in-memory reports kept the new name, as if two saved before the failure; one is still stale.
        reports[2].NewsOrganisationName = OldName;
        organisation.Name = OldName;
        repository.Setup(r => r.Save(It.IsAny<IEnumerable<NewsReport>>()))
            .Callback<IEnumerable<NewsReport>>(batch => _saveOrder.Add($"children:{batch.Count()}"))
            .Returns(Task.CompletedTask);
        _saveOrder.Clear();
        var retry = await Service.RenameNewsOrganisation(
            new CatalogueRenameCommand(organisation.Id.ToString(), NewName), CancellationToken.None);

        // Assert
        first.Status.Should().Be(CatalogueRenameStatus.Failed);
        parentSavedAfterFailure.Should().BeFalse();
        retry.Status.Should().Be(CatalogueRenameStatus.Ok);
        _saveOrder.Should().Equal("redirect", "children:1", "parent");
        reports.Should().OnlyContain(r => r.NewsOrganisationName == NewName);
        _redirects.Should().HaveCount(2).And.OnlyContain(r => r.Key == $"news/{OldName}" && r.NewName == NewName);
    }

    [Fact(DisplayName =
        "Repeating a completed rename by guid is idempotent: no new redirect, no parent save, unchanged children not " +
        "rewritten, and the search patch re-runs.")]
    public async Task repeat_is_idempotent()
    {
        // Arrange
        var show = AddShow(NewName);
        var episode = AddEpisode(show);

        // Act
        var result = await Service.RenameTvShow(
            new CatalogueRenameCommand(show.Id.ToString(), NewName), CancellationToken.None);

        // Assert
        result.Status.Should().Be(CatalogueRenameStatus.Ok);
        _redirects.Should().BeEmpty();
        _saveOrder.Should().BeEmpty();
        _merges.Single().Keys.Should().Equal(episode.Id.ToString());
    }

    [Fact(DisplayName =
        "A search document that is not indexed (404, e.g. a removed child) is skipped; any other per-document failure reports Failure.")]
    public async Task search_patch_outcomes()
    {
        // Arrange
        var organisation = AddOrganisation();
        var notIndexed = AddReport(organisation, removed: true);
        var failing = AddReport(organisation);
        _mergeStatus[notIndexed.Id.ToString()] = 404;

        // Act
        var skipped = await Service.RenameNewsOrganisation(
            new CatalogueRenameCommand(organisation.Id.ToString(), NewName), CancellationToken.None);
        _mergeStatus[failing.Id.ToString()] = 503;
        var failed = await Service.RenameNewsOrganisation(
            new CatalogueRenameCommand(organisation.Id.ToString(), NewName), CancellationToken.None);

        // Assert
        skipped.Status.Should().Be(CatalogueRenameStatus.Ok);
        skipped.SearchIndexer!.IndexerState.Should().Be(IndexerState.Executed);
        failed.Status.Should().Be(CatalogueRenameStatus.Ok);
        failed.SearchIndexer!.IndexerState.Should().Be(IndexerState.Failure);
    }

    [Fact(DisplayName =
        "A news organisation with no reports still renames; the search state is NoDocuments, as IndexEpisodes reports for none.")]
    public async Task no_children()
    {
        // Arrange
        var organisation = AddOrganisation();

        // Act
        var result = await Service.RenameNewsOrganisation(
            new CatalogueRenameCommand(organisation.Id.ToString(), NewName), CancellationToken.None);

        // Assert
        result.Status.Should().Be(CatalogueRenameStatus.Ok);
        result.SearchIndexer!.EpisodeIndexRequestState.Should().Be(EpisodeIndexRequestState.NoDocuments);
        _merges.Should().BeEmpty();
    }

    [Fact(DisplayName = "Search patches go in batches of at most 1000 documents (the Azure Search limit).")]
    public async Task search_patch_batches()
    {
        // Arrange
        var show = AddShow(OldName);
        for (var i = 0; i < PlayableSearchRename.MergeBatchSize + 5; i++)
        {
            AddEpisode(show);
        }

        // Act
        var result = await Service.RenameTvShow(
            new CatalogueRenameCommand(show.Id.ToString(), NewName), CancellationToken.None);

        // Assert
        result.Status.Should().Be(CatalogueRenameStatus.Ok);
        _merges.Select(m => m.Keys.Length).Should().Equal(PlayableSearchRename.MergeBatchSize, 5);
    }
}
