using System.Net;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Repositories;

namespace RedditPodcastPoster.Persistence.Tests.BusinessRules;

public class TvShowTrailerRepositoryRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "TV shows holding a trailer id are found by a cross-partition query on trailers[].id, because a trailer episode must not be attached to a second TV show.")]
    public async Task trailer_id_lookup_queries_across_partitions()
    {
        // Arrange
        var trailerId = _fixture.CreateGuid();
        var holdingShowId = _fixture.CreateGuid();
        QueryDefinition? query = null;
        QueryRequestOptions? options = null;
        var iterator = new SinglePageIterator([holdingShowId.ToString()]);
        _mocker.GetMock<Container>()
            .Setup(container => container.GetItemQueryIterator<string>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string?>(),
                It.IsAny<QueryRequestOptions?>()))
            .Callback<QueryDefinition, string?, QueryRequestOptions?>((definition, _, requestOptions) =>
            {
                query = definition;
                options = requestOptions;
            })
            .Returns(iterator);
        var sut = _mocker.CreateInstance<TvShowRepository>();

        // Act
        var ids = await sut.GetTvShowIdsByTrailerId(trailerId);

        // Assert
        query.Should().NotBeNull();
        query!.QueryText.Should().Be(
            "SELECT VALUE c.id FROM c WHERE EXISTS(SELECT VALUE t FROM t IN c.trailers WHERE t.id = @trailerId)");
        query.GetQueryParameters().Should().ContainSingle()
            .Which.Should().Be(("@trailerId", trailerId.ToString()));
        options.Should().NotBeNull();
        options!.PartitionKey.Should().BeNull();
        ids.Should().Equal(holdingShowId);
    }

    [Fact(DisplayName =
        "A TV show replace whose stored ETag no longer matches returns false instead of throwing, so the caller re-reads and re-applies rather than overwriting another write (RPP #1412).")]
    public async Task replace_with_a_stale_etag_returns_false()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle());
        _mocker.GetMock<Container>()
            .Setup(container => container.ReplaceItemAsync(
                It.IsAny<TvShow>(),
                It.IsAny<string>(),
                It.IsAny<PartitionKey?>(),
                It.Is<ItemRequestOptions>(options => options.IfMatchEtag == "stale"),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException(_fixture.Create<string>(), HttpStatusCode.PreconditionFailed, 0, _fixture.Create<string>(), 0));
        var sut = _mocker.CreateInstance<TvShowRepository>();

        // Act
        var replaced = await sut.TryReplace(show, "stale");

        // Assert
        replaced.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A TV show create when a document already has the id returns false instead of throwing, so concurrent creates of one deterministic id converge on one document (RPP #1412).")]
    public async Task create_when_the_id_exists_returns_false()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle());
        _mocker.GetMock<Container>()
            .Setup(container => container.CreateItemAsync(
                It.IsAny<TvShow>(),
                It.IsAny<PartitionKey?>(),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException(_fixture.Create<string>(), HttpStatusCode.Conflict, 0, _fixture.Create<string>(), 0));
        var sut = _mocker.CreateInstance<TvShowRepository>();

        // Act
        var created = await sut.TryCreate(show);

        // Assert
        created.Should().BeFalse();
    }
    private sealed class SinglePageIterator(IReadOnlyList<string> ids) : FeedIterator<string>
    {
        private bool _pending = true;

        public override bool HasMoreResults => _pending;

        public override Task<FeedResponse<string>> ReadNextAsync(CancellationToken cancellationToken = default)
        {
            _pending = false;
            return Task.FromResult<FeedResponse<string>>(new StringPage(ids));
        }
    }

    private sealed class StringPage(IReadOnlyList<string> ids) : FeedResponse<string>
    {
        public override string ContinuationToken => string.Empty;

        public override int Count => ids.Count;

        public override string IndexMetrics => string.Empty;

        public override Headers Headers => new();

        public override IEnumerable<string> Resource => ids;

        public override HttpStatusCode StatusCode => HttpStatusCode.OK;

        public override CosmosDiagnostics Diagnostics => null!;

        public override IEnumerator<string> GetEnumerator() => ids.GetEnumerator();
    }
}
