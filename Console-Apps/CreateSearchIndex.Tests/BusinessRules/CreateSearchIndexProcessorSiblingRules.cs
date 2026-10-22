using System.Net;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using CreateSearchIndex;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Persistence.Configuration;

namespace CreateSearchIndex.Tests;

/// <summary>
/// Processor calls against mocked Azure Search clients (no network). Synthetic index and container names.
/// </summary>
public class CreateSearchIndexProcessorSiblingRules
{
    private const string IndexName = "synthetic-index";

    private static readonly string[] SiblingIndexers =
    [
        $"{IndexName}-tvshowepisode-indexer",
        $"{IndexName}-newsreport-indexer"
    ];

    private static readonly string[] SiblingDataSources =
    [
        $"{IndexName}-tvshowepisode-ds",
        $"{IndexName}-newsreport-ds"
    ];

    private const string RetiredFilmIndexer = $"{IndexName}-film-indexer";
    private const string RetiredFilmDataSource = $"{IndexName}-film-ds";

    private readonly AutoMocker _mocker = new();
    private readonly List<string> _createdOrUpdatedIndexers = [];
    private readonly List<string> _createdOrUpdatedDataSources = [];
    private readonly List<(string Call, string Indexer)> _indexerCallOrder = [];

    public CreateSearchIndexProcessorSiblingRules()
    {
        _mocker.Use(Options.Create(new CosmosDbSettings
        {
            Endpoint = "https://synthetic.documents.example/", AuthKeyOrResourceToken = "synthetic",
            DatabaseId = "db", PodcastsContainer = "podcasts", EpisodesContainer = "episodes",
            TvShowsContainer = "tvshows", TvShowEpisodesContainer = "tvshowepisodes", FilmsContainer = "films",
            NewsOrganisationsContainer = "newsorganisations", NewsReportsContainer = "newsreports",
            SubjectsContainer = "subjects", PeopleContainer = "people", ActivitiesContainer = "activities",
            DiscoveryContainer = "discovery", DiscoverySubmissionsContainer = "submissions",
            LookUpsContainer = "lookups", TitleCasingRulesContainer = "titlecasing",
            PushSubscriptionsContainer = "push"
        }));

        // AutoMocker builds Azure SDK clients through their *Settings constructors, which throw without an endpoint.
        // Mock.Of uses the protected parameterless constructor; the mocks are registered with AutoMocker so the
        // SUT still comes from CreateInstance and setups still go through _mocker.GetMock<T>().
        _mocker.Use(Mock.Of<SearchClient>());
        _mocker.Use(Mock.Get(Mock.Of<SearchIndexerClient>()));
        _mocker.Use(Mock.Get(Mock.Of<SearchIndexClient>()));
        var indexer = _mocker.GetMock<SearchIndexerClient>();
        // Every indexer and data source already exists (the state before a rebuild).
        indexer.Setup(x => x.GetIndexerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken _) =>
                Response.FromValue(new SearchIndexer(name, "ds", IndexName), Status(200)));
        indexer.Setup(x => x.GetDataSourceConnectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken _) => Response.FromValue(
                new SearchIndexerDataSourceConnection(name, SearchIndexerDataSourceType.CosmosDb, "cs",
                    new SearchIndexerDataContainer("c")), Status(200)));
        indexer.Setup(x => x.DeleteIndexerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(204));
        indexer.Setup(x => x.DeleteDataSourceConnectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(204));
        indexer.Setup(x => x.CreateOrUpdateIndexerAsync(It.IsAny<SearchIndexer>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Callback((SearchIndexer i, bool _, CancellationToken _) =>
            {
                _createdOrUpdatedIndexers.Add(i.Name);
                _indexerCallOrder.Add(("create-or-update", i.Name));
            })
            .ReturnsAsync((SearchIndexer i, bool _, CancellationToken _) =>
                Response.FromValue(i, Status(200)));
        indexer.Setup(x => x.CreateOrUpdateDataSourceConnectionAsync(It.IsAny<SearchIndexerDataSourceConnection>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback((SearchIndexerDataSourceConnection d, bool _, CancellationToken _) =>
                _createdOrUpdatedDataSources.Add(d.Name))
            .ReturnsAsync((SearchIndexerDataSourceConnection d, bool _, CancellationToken _) =>
                Response.FromValue(d, Status(200)));
        indexer.Setup(x => x.ResetIndexerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string name, CancellationToken _) => _indexerCallOrder.Add(("reset", name)))
            .ReturnsAsync(Status(204));

        var index = _mocker.GetMock<SearchIndexClient>();
        index.Setup(x => x.DeleteIndexAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Status(204));
        index.Setup(x => x.GetIndexAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException((int)HttpStatusCode.NotFound, "not found"));
        index.Setup(x => x.CreateOrUpdateIndexAsync(It.IsAny<SearchIndex>(), It.IsAny<bool>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((SearchIndex i, bool _, bool _, CancellationToken _) => Response.FromValue(i, Status(200)));
    }

    private static Response Status(int status) => new StatusResponse(status);

    /// <summary>Focused fake: the processor only reads <see cref="Response.Status"/>.</summary>
    private sealed class StatusResponse(int status) : Response
    {
        public override int Status => status;
        public override string ReasonPhrase => string.Empty;
        public override Stream? ContentStream { get; set; }
        public override string ClientRequestId { get; set; } = string.Empty;
        public override void Dispose() { }
        protected override bool ContainsHeader(string name) => false;
        protected override IEnumerable<Azure.Core.HttpHeader> EnumerateHeaders() => [];

        protected override bool TryGetHeader(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
        {
            value = null;
            return false;
        }

        protected override bool TryGetHeaderValues(string name, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IEnumerable<string>? values)
        {
            values = null;
            return false;
        }
    }

    [Fact(DisplayName =
        "When the index is torn down without --all-playables, then the TV and news indexers and data sources are deleted and created again and the retired Film pair is deleted and not recreated, because the recreated index needs TV and news back while films are pushed by FilmSearchPusher.")]
    public async Task teardown_without_all_playables_deletes_and_recreates_siblings()
    {
        // Arrange
        var request = new CreateSearchIndexRequest { IndexName = IndexName, TearDownIndex = true };
        var sut = _mocker.CreateInstance<CreateSearchIndexProcessor>();

        // Act
        await sut.Process(request);

        // Assert
        var indexer = _mocker.GetMock<SearchIndexerClient>();
        foreach (var name in SiblingIndexers)
        {
            indexer.Verify(x => x.DeleteIndexerAsync(name, It.IsAny<CancellationToken>()), Times.Once);
        }

        foreach (var name in SiblingDataSources)
        {
            indexer.Verify(x => x.DeleteDataSourceConnectionAsync(name, It.IsAny<CancellationToken>()), Times.Once);
        }

        _mocker.GetMock<SearchIndexClient>()
            .Verify(x => x.DeleteIndexAsync(IndexName, It.IsAny<CancellationToken>()), Times.Once);
        indexer.Verify(x => x.DeleteIndexerAsync(RetiredFilmIndexer, It.IsAny<CancellationToken>()), Times.Once);
        indexer.Verify(x => x.DeleteDataSourceConnectionAsync(RetiredFilmDataSource, It.IsAny<CancellationToken>()),
            Times.Once);
        _createdOrUpdatedIndexers.Should().BeEquivalentTo(SiblingIndexers);
        _createdOrUpdatedDataSources.Should().BeEquivalentTo(SiblingDataSources);
    }

    [Fact(DisplayName =
        "When --all-playables is run against an existing index without a teardown, then the TV and news indexers are created or updated, a leftover Film pair is deleted, and the index is kept, so a service with only the episode pair reaches the three-source layout without a rebuild.")]
    public async Task all_playables_adds_tv_and_news_without_teardown()
    {
        // Arrange
        _mocker.GetMock<SearchIndexClient>()
            .Setup(x => x.GetIndexAsync(IndexName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(new SearchIndex(IndexName), Status(200)));
        var request = new CreateSearchIndexRequest
        {
            IndexName = IndexName, DataSourceName = $"{IndexName}-ds", IndexerName = $"{IndexName}-indexer",
            AllPlayables = true
        };
        var sut = _mocker.CreateInstance<CreateSearchIndexProcessor>();

        // Act
        await sut.Process(request);

        // Assert
        _mocker.GetMock<SearchIndexClient>()
            .Verify(x => x.DeleteIndexAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        var indexer = _mocker.GetMock<SearchIndexerClient>();
        indexer.Verify(x => x.DeleteIndexerAsync(RetiredFilmIndexer, It.IsAny<CancellationToken>()), Times.Once);
        indexer.Verify(x => x.DeleteIndexerAsync($"{IndexName}-indexer", It.IsAny<CancellationToken>()), Times.Never);
        foreach (var name in SiblingIndexers)
        {
            indexer.Verify(x => x.DeleteIndexerAsync(name, It.IsAny<CancellationToken>()), Times.Never);
        }

        _createdOrUpdatedIndexers.Should().BeEquivalentTo(SiblingIndexers);
        _createdOrUpdatedDataSources.Should().BeEquivalentTo(SiblingDataSources);
    }

    [Fact(DisplayName =
        "When --all-playables runs and no Film pair is left over, then nothing named Film is deleted, because there is nothing to retire.")]
    public async Task all_playables_without_leftover_film_pair_deletes_nothing()
    {
        // Arrange
        var indexer = _mocker.GetMock<SearchIndexerClient>();
        indexer.Setup(x => x.GetIndexerAsync(RetiredFilmIndexer, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException((int)HttpStatusCode.NotFound, "not found"));
        indexer.Setup(x => x.GetDataSourceConnectionAsync(RetiredFilmDataSource, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException((int)HttpStatusCode.NotFound, "not found"));
        var request = new CreateSearchIndexRequest { IndexName = IndexName, AllPlayables = true };
        var sut = _mocker.CreateInstance<CreateSearchIndexProcessor>();

        // Act
        await sut.Process(request);

        // Assert
        indexer.Verify(x => x.DeleteIndexerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        indexer.Verify(x => x.DeleteDataSourceConnectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _createdOrUpdatedIndexers.Should().BeEquivalentTo(SiblingIndexers);
    }

    [Fact(DisplayName =
        "When the index is only created (no teardown, no --all-playables), then no sibling indexer is deleted, created or reset, because the existing index keeps its documents.")]
    public async Task create_only_leaves_siblings_alone()
    {
        // Arrange
        var request = new CreateSearchIndexRequest { IndexName = IndexName };
        var sut = _mocker.CreateInstance<CreateSearchIndexProcessor>();

        // Act
        await sut.Process(request);

        // Assert
        var indexer = _mocker.GetMock<SearchIndexerClient>();
        indexer.Verify(x => x.DeleteIndexerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        indexer.Verify(x => x.ResetIndexerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _createdOrUpdatedIndexers.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When --update-existing --reset-indexer --all-playables is run, then the episode indexer and every sibling indexer are reset, because existing TV and news documents also need the new fields.")]
    public async Task reset_with_all_playables_resets_every_sibling()
    {
        // Arrange
        _mocker.GetMock<SearchIndexClient>()
            .Setup(x => x.GetIndexAsync(IndexName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(new SearchIndex(IndexName), Status(200)));
        var request = new CreateSearchIndexRequest
        {
            IndexName = IndexName, DataSourceName = $"{IndexName}-ds", IndexerName = $"{IndexName}-indexer",
            UpdateExisting = true, ResetIndexer = true, AllPlayables = true
        };
        var sut = _mocker.CreateInstance<CreateSearchIndexProcessor>();

        // Act
        await sut.Process(request);

        // Assert
        var indexer = _mocker.GetMock<SearchIndexerClient>();
        indexer.Verify(x => x.ResetIndexerAsync($"{IndexName}-indexer", It.IsAny<CancellationToken>()), Times.Once);
        foreach (var name in SiblingIndexers)
        {
            indexer.Verify(x => x.ResetIndexerAsync(name, It.IsAny<CancellationToken>()), Times.Once);
        }

        indexer.Verify(x => x.DeleteIndexerAsync(
            It.Is<string>(name => name != RetiredFilmIndexer), It.IsAny<CancellationToken>()), Times.Never);
        foreach (var name in SiblingIndexers)
        {
            var ensured = _indexerCallOrder.IndexOf(("create-or-update", name));
            var reset = _indexerCallOrder.IndexOf(("reset", name));
            ensured.Should().BeGreaterThanOrEqualTo(0, "sibling '{0}' must be ensured", name);
            reset.Should().BeGreaterThan(ensured, "sibling '{0}' must exist before it is reset", name);
        }
    }

    private void GivenServiceQuota(int dataSourcesAndIndexers, long usage = 1)
    {
        var quota = SearchModelFactory.SearchResourceCounter(usage, dataSourcesAndIndexers);
        var counters = SearchModelFactory.SearchServiceCounters(
            SearchModelFactory.SearchResourceCounter(0, 0), SearchModelFactory.SearchResourceCounter(1, null),
            quota, quota, quota, SearchModelFactory.SearchResourceCounter(1, 52428800),
            SearchModelFactory.SearchResourceCounter(0, 3), SearchModelFactory.SearchResourceCounter(0, 3),
            SearchModelFactory.SearchResourceCounter(0, null));
        _mocker.GetMock<SearchIndexClient>()
            .Setup(x => x.GetServiceStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(SearchModelFactory.SearchServiceStatistics(counters, null!), Status(200)));
    }

    [Fact(DisplayName =
        "When a teardown runs on a Free-tier service (three data sources and three indexers), then it goes ahead and recreates only the TV and news siblings, because episodes, TV and news fit the Free-tier limit exactly.")]
    public async Task teardown_on_free_tier_recreates_tv_and_news()
    {
        // Arrange
        GivenServiceQuota(3);
        var request = new CreateSearchIndexRequest { IndexName = IndexName, TearDownIndex = true };
        var sut = _mocker.CreateInstance<CreateSearchIndexProcessor>();

        // Act
        await sut.Process(request);

        // Assert
        _mocker.GetMock<SearchIndexClient>()
            .Verify(x => x.DeleteIndexAsync(IndexName, It.IsAny<CancellationToken>()), Times.Once);
        _createdOrUpdatedIndexers.Should().BeEquivalentTo(SiblingIndexers);
        _createdOrUpdatedIndexers.Should().NotContain(RetiredFilmIndexer);
    }

    [Fact(DisplayName =
        "When a teardown would need more data sources or indexers than the service allows, then it stops before deleting anything, because the last sibling create would otherwise fail after the index was already gone.")]
    public async Task teardown_over_quota_stops_before_deleting()
    {
        // Arrange
        GivenServiceQuota(2);
        var request = new CreateSearchIndexRequest { IndexName = IndexName, TearDownIndex = true, RunIndexer = true };
        var sut = _mocker.CreateInstance<CreateSearchIndexProcessor>();

        // Act
        var act = () => sut.Process(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Nothing was changed*");
        _mocker.GetMock<SearchIndexClient>()
            .Verify(x => x.DeleteIndexAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<SearchIndexerClient>()
            .Verify(x => x.DeleteIndexerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _createdOrUpdatedIndexers.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a Free-tier service already holds the episode, TV and news pairs, then --all-playables goes ahead, because the run only updates the pairs it manages and still ends with three of each.")]
    public async Task all_playables_on_full_free_tier_with_only_managed_pairs_goes_ahead()
    {
        // Arrange
        var indexer = _mocker.GetMock<SearchIndexerClient>();
        indexer.Setup(x => x.GetIndexerAsync(RetiredFilmIndexer, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException((int)HttpStatusCode.NotFound, "not found"));
        indexer.Setup(x => x.GetDataSourceConnectionAsync(RetiredFilmDataSource, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException((int)HttpStatusCode.NotFound, "not found"));
        GivenServiceQuota(3, usage: 3);
        var request = new CreateSearchIndexRequest
        {
            IndexName = IndexName, DataSourceName = $"{IndexName}-ds", IndexerName = $"{IndexName}-indexer",
            AllPlayables = true
        };
        var sut = _mocker.CreateInstance<CreateSearchIndexProcessor>();

        // Act
        await sut.Process(request);

        // Assert
        _createdOrUpdatedIndexers.Should().BeEquivalentTo(SiblingIndexers);
    }

    [Fact(DisplayName =
        "When something other than the managed pairs is using a slot and the result would exceed the quota, then the run stops before changing anything, because the last sibling create would otherwise fail part-way.")]
    public async Task unmanaged_usage_over_quota_stops_before_changing_anything()
    {
        // Arrange
        var indexer = _mocker.GetMock<SearchIndexerClient>();
        indexer.Setup(x => x.GetIndexerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException((int)HttpStatusCode.NotFound, "not found"));
        indexer.Setup(x => x.GetDataSourceConnectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException((int)HttpStatusCode.NotFound, "not found"));
        GivenServiceQuota(3, usage: 2);
        var request = new CreateSearchIndexRequest { IndexName = IndexName, TearDownIndex = true };
        var sut = _mocker.CreateInstance<CreateSearchIndexProcessor>();

        // Act
        var act = () => sut.Process(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Nothing was changed*");
        _mocker.GetMock<SearchIndexClient>()
            .Verify(x => x.DeleteIndexAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _createdOrUpdatedIndexers.Should().BeEmpty();
    }
}
