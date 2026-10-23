using System.Net;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Persistence.Repositories;

namespace RedditPodcastPoster.Persistence.Tests.BusinessRules;

public class FilmTrailerIdLookupRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "Films holding a trailer id are found by a cross-partition query on trailers[].id, because a trailer episode must not be attached to a second film.")]
    public async Task trailer_id_lookup_queries_across_partitions()
    {
        // Arrange
        var trailerId = _fixture.CreateGuid();
        var holdingFilmId = _fixture.CreateGuid();
        QueryDefinition? query = null;
        QueryRequestOptions? options = null;
        var iterator = new SinglePageIterator([holdingFilmId.ToString()]);
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
        var sut = _mocker.CreateInstance<FilmRepository>();

        // Act
        var ids = await sut.GetFilmIdsByTrailerId(trailerId);

        // Assert
        query.Should().NotBeNull();
        query!.QueryText.Should().Be(
            "SELECT VALUE c.id FROM c WHERE EXISTS(SELECT VALUE t FROM t IN c.trailers WHERE t.id = @trailerId)");
        query.GetQueryParameters().Should().ContainSingle()
            .Which.Should().Be(("@trailerId", trailerId.ToString()));
        options.Should().NotBeNull();
        options!.PartitionKey.Should().BeNull();
        ids.Should().Equal(holdingFilmId);
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
