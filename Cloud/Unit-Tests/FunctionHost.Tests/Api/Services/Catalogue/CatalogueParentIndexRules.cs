using System.Linq.Expressions;
using Api.Models;
using Api.Services.Catalogue;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.Search.Models;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Catalogue;

public class CatalogueParentIndexRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();
    private readonly List<TvShow> _shows = [];
    private readonly List<NewsOrganisation> _organisations = [];
    private readonly List<EpisodeSearchRecord> _uploaded = [];
    private readonly List<(Guid Id, IndexingContext Context)> _indexed = [];
    private CatalogueParentIndexResult _indexResult = new(CatalogueParentIndexStatus.Indexed);

    public CatalogueParentIndexRules()
    {
        _mocker.Use(NullLogger<CatalogueParentIndexService>.Instance);
        _mocker.Use(Options.Create(new IndexerOptions { ReleasedDaysAgo = 4 }));

        var tvShows = _mocker.GetMock<ITvShowRepository>();
        tvShows.Setup(x => x.GetTvShow(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _shows.SingleOrDefault(x => x.Id == id));
        tvShows.Setup(x => x.GetAllBy(It.IsAny<Expression<Func<TvShow, bool>>>()))
            .Returns((Expression<Func<TvShow, bool>> predicate) => _shows.Where(predicate.Compile()).ToAsyncEnumerable());

        var organisations = _mocker.GetMock<INewsOrganisationRepository>();
        organisations.Setup(x => x.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _organisations.SingleOrDefault(x => x.Id == id));
        organisations.Setup(x => x.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns((Expression<Func<NewsOrganisation, bool>> predicate) =>
                _organisations.Where(predicate.Compile()).ToAsyncEnumerable());

        _mocker.GetMock<ICatalogueParentsUpdater>()
            .Setup(x => x.IndexParent(It.IsAny<Guid>(), It.IsAny<IndexingContext>()))
            .Callback((Guid id, IndexingContext context) => _indexed.Add((id, context)))
            .ReturnsAsync(() => _indexResult);

        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<EpisodeSearchRecord>, CancellationToken>((documents, _) => _uploaded.AddRange(documents))
            .ReturnsAsync(true);
        _mocker.Use(_search.Cleanup);
        _mocker.Use(NullLogger<CataloguePlayableSearchSync>.Instance);
        _mocker.Use(_mocker.CreateInstance<CataloguePlayableSearchSync>());
    }

    private CatalogueParentIndexService Sut => _mocker.CreateInstance<CatalogueParentIndexService>();

    private TvShow SeedTvShow(string? name = null)
    {
        var show = new TvShow(name ?? _fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _shows.Add(show);
        return show;
    }

    private NewsOrganisation SeedOrganisation(string? name = null)
    {
        var organisation = new NewsOrganisation(name ?? _fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _organisations.Add(organisation);
        return organisation;
    }

    private NewsReport ReportUnder(NewsOrganisation organisation)
    {
        var report = new NewsReport(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Length = _fixture.CreateDuration() };
        report.SetNewsOrganisationProperties(organisation);
        return report;
    }

    [Fact(DisplayName =
        "A curator indexes a news organisation by its name (any case): the organisation is indexed by id, the saved " +
        "reports are pushed to search, and the result lists their ids, because the hub page links by name.")]
    public async Task index_by_name_indexes_and_pushes_to_search()
    {
        // Arrange
        var organisation = SeedOrganisation();
        var report = ReportUnder(organisation);
        _indexResult = new CatalogueParentIndexResult(CatalogueParentIndexStatus.Indexed, [report]);

        // Act
        var result = await Sut.IndexNewsOrganisation(organisation.Name.ToUpperInvariant(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(CatalogueIndexStatus.Ok);
        result.IndexedIds.Should().Equal(report.Id);
        result.SearchIndexer!.IndexerState.Should().Be(IndexerState.Executed);
        _indexed.Should().ContainSingle().Which.Id.Should().Be(organisation.Id);
        _uploaded.Should().ContainSingle().Which.Id.Should().Be(report.Id.ToString());
    }

    [Fact(DisplayName =
        "A curator index runs with the same context as the podcast index: every resolver and expensive query on, " +
        "Spotify indexed, no podcast discovery, no YouTube indexing hints.")]
    public async Task index_uses_curator_context()
    {
        // Arrange
        var show = SeedTvShow();

        // Act
        await Sut.IndexTvShow(show.Id.ToString(), CancellationToken.None);

        // Assert
        var context = _indexed.Should().ContainSingle().Subject.Context;
        context.ReleasedSince.Should().NotBeNull();
        context.IndexSpotify.Should().BeTrue();
        context.SkipYouTubeUrlResolving.Should().BeFalse();
        context.SkipSpotifyUrlResolving.Should().BeFalse();
        context.SkipExpensiveYouTubeQueries.Should().BeFalse();
        context.SkipExpensiveSpotifyQueries.Should().BeFalse();
        context.SkipPodcastDiscovery.Should().BeTrue();
        context.UseYouTubeIndexingHints.Should().BeFalse();
    }

    [Fact(DisplayName =
        "An index that saved nothing is Ok with no ids and NoDocuments, because finding nothing new is not a failure.")]
    public async Task index_with_no_changes_is_ok()
    {
        // Arrange
        var show = SeedTvShow();
        _indexResult = new CatalogueParentIndexResult(CatalogueParentIndexStatus.Indexed, []);

        // Act
        var result = await Sut.IndexTvShow(show.Name, CancellationToken.None);

        // Assert
        result.Status.Should().Be(CatalogueIndexStatus.Ok);
        result.IndexedIds.Should().BeEmpty();
        result.SearchIndexer!.EpisodeIndexRequestState.Should().Be(EpisodeIndexRequestState.NoDocuments);
        _uploaded.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "An unknown name or id is NotFound and nothing is indexed, because the API answers 404.")]
    public async Task unknown_identifier_is_not_found()
    {
        // Arrange
        SeedTvShow();
        var unknownName = _fixture.CreateTitle();
        var unknownId = _fixture.CreateGuid().ToString();

        // Act
        var byName = await Sut.IndexTvShow(unknownName, CancellationToken.None);
        var byId = await Sut.IndexTvShow(unknownId, CancellationToken.None);

        // Assert
        byName.Status.Should().Be(CatalogueIndexStatus.NotFound);
        byId.Status.Should().Be(CatalogueIndexStatus.NotFound);
        _indexed.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A TV show name is looked up among TV shows only, so a news organisation with that name is NotFound as a TV show.")]
    public async Task name_lookup_is_per_kind()
    {
        // Arrange
        var organisation = SeedOrganisation();

        // Act
        var result = await Sut.IndexTvShow(organisation.Name, CancellationToken.None);

        // Assert
        result.Status.Should().Be(CatalogueIndexStatus.NotFound);
        _indexed.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "A name shared by two TV shows is a Conflict with both ids and nothing is indexed, because the API answers 409 " +
        "like rename.")]
    public async Task ambiguous_name_is_conflict()
    {
        // Arrange
        var first = SeedTvShow();
        var second = SeedTvShow(first.Name);

        // Act
        var result = await Sut.IndexTvShow(first.Name, CancellationToken.None);

        // Assert
        result.Status.Should().Be(CatalogueIndexStatus.Conflict);
        result.AmbiguousIds.Should().BeEquivalentTo([first.Id, second.Id]);
        _indexed.Should().BeEmpty();
    }

    [Theory(DisplayName =
        "A refused or cut-off index maps to its status and nothing is pushed to search: NotIndexable, " +
        "SourcePodcastStillExists, BudgetExceeded, and NotFound when the parent vanished between lookup and index.")]
    [InlineData(CatalogueParentIndexStatus.NotIndexable, CatalogueIndexStatus.NotIndexable)]
    [InlineData(CatalogueParentIndexStatus.SourcePodcastStillExists, CatalogueIndexStatus.SourcePodcastStillExists)]
    [InlineData(CatalogueParentIndexStatus.BudgetExceeded, CatalogueIndexStatus.BudgetExceeded)]
    [InlineData(CatalogueParentIndexStatus.NotFound, CatalogueIndexStatus.NotFound)]
    public async Task refused_index_maps_status(CatalogueParentIndexStatus indexStatus, CatalogueIndexStatus expected)
    {
        // Arrange
        var organisation = SeedOrganisation();
        _indexResult = new CatalogueParentIndexResult(indexStatus);

        // Act
        var result = await Sut.IndexNewsOrganisation(organisation.Name, CancellationToken.None);

        // Assert
        result.Status.Should().Be(expected);
        _uploaded.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "An exception while indexing is Failed, not thrown, because the API answers 500 with an error body.")]
    public async Task exception_is_failed()
    {
        // Arrange
        var show = SeedTvShow();
        _mocker.GetMock<ICatalogueParentsUpdater>()
            .Setup(x => x.IndexParent(It.IsAny<Guid>(), It.IsAny<IndexingContext>()))
            .ThrowsAsync(new InvalidOperationException(_fixture.CreateTitle()));

        // Act
        var result = await Sut.IndexTvShow(show.Name, CancellationToken.None);

        // Assert
        result.Status.Should().Be(CatalogueIndexStatus.Failed);
    }
}
