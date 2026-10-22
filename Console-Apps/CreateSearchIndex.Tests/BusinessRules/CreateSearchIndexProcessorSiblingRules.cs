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
        $"{IndexName}-film-indexer",
        $"{IndexName}-newsreport-indexer"
    ];

    private static readonly string[] SiblingDataSources =
    [
        $"{IndexName}-tvshowepisode-ds",
        $"{IndexName}-film-ds",
        $"{IndexName}-newsreport-ds"
    ];

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
        "When the index is torn down without --all-playables, then the TV, film and news indexers and data sources are deleted and created again, because the recreated index would otherwise never get those documents back.")]
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
        _createdOrUpdatedIndexers.Should().BeEquivalentTo(SiblingIndexers);
        _createdOrUpdatedDataSources.Should().BeEquivalentTo(SiblingDataSources);
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
        "When --update-existing --reset-indexer --all-playables is run, then the episode indexer and every sibling indexer are reset, because existing TV, film and news documents also need the new fields.")]
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

        indexer.Verify(x => x.DeleteIndexerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        foreach (var name in SiblingIndexers)
        {
            var ensured = _indexerCallOrder.IndexOf(("create-or-update", name));
            var reset = _indexerCallOrder.IndexOf(("reset", name));
            ensured.Should().BeGreaterThanOrEqualTo(0, "sibling '{0}' must be ensured", name);
            reset.Should().BeGreaterThan(ensured, "sibling '{0}' must exist before it is reset", name);
        }
    }

    [Fact(DisplayName =
        "When transient siblings run, then each of TV, film and news is created, run and deleted before the next starts, so no more than one sibling pair exists at a time on a Free-tier service.")]
    public async Task transient_siblings_create_run_and_delete_one_at_a_time()
    {
        // Arrange
        var request = new CreateSearchIndexRequest
        {
            IndexName = IndexName, TearDownIndex = true, RunIndexer = true, TransientPlayables = true
        };
        var events = new List<string>();
        var live = new HashSet<string>();
        var maxLive = 0;
        var indexer = _mocker.GetMock<SearchIndexerClient>();
        indexer.Setup(x => x.GetIndexerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken _) => live.Contains(name)
                ? Response.FromValue(new SearchIndexer(name, "ds", IndexName), Status(200))
                : throw new RequestFailedException((int)HttpStatusCode.NotFound, "not found"));
        indexer.Setup(x => x.GetDataSourceConnectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken _) => live.Contains(name)
                ? Response.FromValue(new SearchIndexerDataSourceConnection(name, SearchIndexerDataSourceType.CosmosDb,
                    "cs", new SearchIndexerDataContainer("c")), Status(200))
                : throw new RequestFailedException((int)HttpStatusCode.NotFound, "not found"));
        indexer.Setup(x => x.CreateOrUpdateIndexerAsync(It.IsAny<SearchIndexer>(), It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Callback((SearchIndexer i, bool _, CancellationToken _) =>
            {
                live.Add(i.Name);
                maxLive = Math.Max(maxLive, live.Count(n => n.EndsWith("-indexer")));
                events.Add($"create {i.Name}");
            })
            .ReturnsAsync((SearchIndexer i, bool _, CancellationToken _) => Response.FromValue(i, Status(200)));
        indexer.Setup(x => x.CreateOrUpdateDataSourceConnectionAsync(It.IsAny<SearchIndexerDataSourceConnection>(),
                It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback((SearchIndexerDataSourceConnection d, bool _, CancellationToken _) => live.Add(d.Name))
            .ReturnsAsync((SearchIndexerDataSourceConnection d, bool _, CancellationToken _) =>
                Response.FromValue(d, Status(200)));
        indexer.Setup(x => x.DeleteIndexerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string name, CancellationToken _) =>
            {
                live.Remove(name);
                events.Add($"delete {name}");
            })
            .ReturnsAsync(Status(204));
        indexer.Setup(x => x.DeleteDataSourceConnectionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string name, CancellationToken _) => live.Remove(name))
            .ReturnsAsync(Status(204));
        var sut = _mocker.CreateInstance<CreateSearchIndexProcessor>();

        // Act
        await sut.RunTransientSiblings(request, name =>
        {
            events.Add($"run {name}");
            return Task.CompletedTask;
        });

        // Assert
        events.Should().Equal(SiblingIndexers.SelectMany(name =>
            new[] { $"create {name}", $"run {name}", $"delete {name}" }));
        maxLive.Should().Be(1);
        live.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a transient sibling run fails, then its indexer and data source are still deleted, so a failed rebuild does not leave the Free-tier quota full.")]
    public async Task transient_sibling_is_deleted_when_its_run_fails()
    {
        // Arrange
        var request = new CreateSearchIndexRequest
        {
            IndexName = IndexName, TearDownIndex = true, RunIndexer = true, TransientPlayables = true
        };
        var sut = _mocker.CreateInstance<CreateSearchIndexProcessor>();

        // Act
        var act = () => sut.RunTransientSiblings(request, _ => throw new InvalidOperationException("synthetic run failure"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("synthetic run failure");
        var indexer = _mocker.GetMock<SearchIndexerClient>();
        indexer.Verify(x => x.DeleteIndexerAsync(SiblingIndexers[0], It.IsAny<CancellationToken>()), Times.Once);
        indexer.Verify(x => x.DeleteDataSourceConnectionAsync(SiblingDataSources[0], It.IsAny<CancellationToken>()),
            Times.Once);
        _createdOrUpdatedIndexers.Should().Equal(SiblingIndexers[0]);
    }

    [Fact(DisplayName =
        "When a teardown asks for --transient-playables without --run-indexer, then nothing is deleted, because the plan is refused before any teardown.")]
    public async Task transient_without_run_indexer_deletes_nothing()
    {
        // Arrange
        var request = new CreateSearchIndexRequest { IndexName = IndexName, TearDownIndex = true, TransientPlayables = true };
        var sut = _mocker.CreateInstance<CreateSearchIndexProcessor>();

        // Act
        var act = () => sut.Process(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _mocker.GetMock<SearchIndexClient>()
            .Verify(x => x.DeleteIndexAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<SearchIndexerClient>()
            .Verify(x => x.DeleteIndexerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When a teardown would recreate scheduled siblings on a service that allows only three data sources and three indexers, then it stops before deleting anything and points at --transient-playables, because the fourth create would fail after the index was already gone.")]
    public async Task teardown_on_free_tier_without_transient_playables_stops_before_deleting()
    {
        // Arrange
        var free = SearchModelFactory.SearchResourceCounter(1, 3);
        var counters = SearchModelFactory.SearchServiceCounters(
            SearchModelFactory.SearchResourceCounter(0, 0), SearchModelFactory.SearchResourceCounter(1, null),
            free, free, free, SearchModelFactory.SearchResourceCounter(1, 52428800),
            SearchModelFactory.SearchResourceCounter(0, 3), SearchModelFactory.SearchResourceCounter(0, 3),
            SearchModelFactory.SearchResourceCounter(0, null));
        _mocker.GetMock<SearchIndexClient>()
            .Setup(x => x.GetServiceStatisticsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(SearchModelFactory.SearchServiceStatistics(counters, null!), Status(200)));
        var request = new CreateSearchIndexRequest { IndexName = IndexName, TearDownIndex = true, RunIndexer = true };
        var sut = _mocker.CreateInstance<CreateSearchIndexProcessor>();

        // Act
        var act = () => sut.Process(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*--transient-playables*");
        _mocker.GetMock<SearchIndexClient>()
            .Verify(x => x.DeleteIndexAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<SearchIndexerClient>()
            .Verify(x => x.DeleteIndexerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _createdOrUpdatedIndexers.Should().BeEmpty();
    }
}
