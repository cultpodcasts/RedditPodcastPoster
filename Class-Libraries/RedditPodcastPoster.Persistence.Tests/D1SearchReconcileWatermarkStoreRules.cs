using System.Net;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Search;
using RedditPodcastPoster.Persistence.Lookups;
using RedditPodcastPoster.Persistence.Providers;

namespace RedditPodcastPoster.Persistence.Tests;

public class D1SearchReconcileWatermarkStoreRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    private static CosmosException CosmosStatus(HttpStatusCode status) => new("synthetic", status, 0, string.Empty, 0);

    [Fact(DisplayName =
        "When no D1 search reconcile watermark has been saved, then reading it returns null, because a missing " +
        "watermark means the next reconcile scans the whole catalogue.")]
    public async Task missing_watermark_reads_as_null()
    {
        // Arrange
        _mocker.GetMock<Container>()
            .Setup(x => x.ReadItemAsync<D1SearchReconcileWatermark>(It.IsAny<string>(), It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(CosmosStatus(HttpStatusCode.NotFound));
        var sut = _mocker.CreateInstance<D1SearchReconcileWatermarkStore>();

        // Act
        var watermark = await sut.GetAsync();

        // Assert
        watermark.Should().BeNull();
    }

    [Fact(DisplayName =
        "When the watermark is saved, then it is upserted into LookUps under its fixed singleton id as the partition " +
        "key, because LookUps singletons are partitioned by their own id.")]
    public async Task save_upserts_singleton_by_fixed_id()
    {
        // Arrange
        var watermark = new D1SearchReconcileWatermark { HighWaterMark = _fixture.Create<long>() };
        var sut = _mocker.CreateInstance<D1SearchReconcileWatermarkStore>();

        // Act
        await sut.SaveAsync(watermark);

        // Assert
        _mocker.GetMock<Container>().Verify(x => x.UpsertItemAsync(
            watermark,
            new PartitionKey(D1SearchReconcileWatermark._Id.ToString()),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "When a watermark with a different id is saved, then the save is refused, because there is only one D1 " +
        "search reconcile watermark.")]
    public async Task save_refuses_other_ids()
    {
        // Arrange
        var watermark = new D1SearchReconcileWatermark { Id = _fixture.CreateGuid() };
        var sut = _mocker.CreateInstance<D1SearchReconcileWatermarkStore>();

        // Act
        var act = () => sut.SaveAsync(watermark);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        _mocker.GetMock<Container>().Verify(x => x.UpsertItemAsync(It.IsAny<D1SearchReconcileWatermark>(),
            It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "When episodes and podcasts are queried by Timestamp with the repository's System.Text.Json Cosmos " +
        "serializer, then the Cosmos SQL filters on _ts, because incremental reconcile reads changed playables " +
        "and changed podcasts by Cosmos _ts.")]
    public void timestamp_filter_translates_to_ts()
    {
        // Arrange
        var since = _fixture.Create<long>();
        using var client = new CosmosClient(
            "https://synthetic.documents.example:443/",
            Convert.ToBase64String(_fixture.CreateGuid().ToByteArray()),
            new CosmosClientOptions
            {
                UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptionsProvider().GetJsonSerializerOptions()
            });
        var episodes = client.GetContainer("db", "episodes");
        var podcasts = client.GetContainer("db", "podcasts");

        // Act
        var episodeSql = episodes.GetItemLinqQueryable<Episode>()
            .Where(episode => episode.Timestamp >= since)
            .ToQueryDefinition()
            .QueryText;
        var podcastSql = podcasts.GetItemLinqQueryable<Podcast>()
            .Where(podcast => podcast.Timestamp >= since)
            .ToQueryDefinition()
            .QueryText;

        // Assert
        episodeSql.Should().Contain("[\"_ts\"] >=").And.Contain(since.ToString());
        podcastSql.Should().Contain("[\"_ts\"] >=").And.Contain(since.ToString());
    }
}
