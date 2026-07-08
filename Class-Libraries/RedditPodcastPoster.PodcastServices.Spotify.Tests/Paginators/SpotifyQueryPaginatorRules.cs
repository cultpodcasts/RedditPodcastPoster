using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Paginators;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.Paginators;

/// <summary>
/// SpotifyQueryPaginator defensively filters null episode references returned despite SDK non-null contracts.
/// </summary>
public class SpotifyQueryPaginatorRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When pagedEpisodes Items is null, PaginateEpisodes returns an empty result " +
        "because a missing first page must not throw during show lookup.")]
    public async Task Null_items_returns_empty_result()
    {
        // Arrange
        var sut = CreateSut(Mock.Of<ISpotifyClientWrapper>());
        var pagedEpisodes = new Paging<SimpleEpisode> { Items = null! };

        // Act
        var result = await sut.PaginateEpisodes(pagedEpisodes, new IndexingContext());

        // Assert
        result.Episodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When PaginateAll returns null episode references mixed with valid episodes, PaginateEpisodes filters them out " +
        "because Spotify occasionally deserializes sparse episode arrays with null slots.")]
    public async Task Filters_null_episode_references_from_pagination_results()
    {
        // Arrange
        var validEpisode = CreateEpisode("episode-1", daysAgo: 1);
        var olderEpisode = CreateEpisode("episode-2", daysAgo: 10);
        var wrapper = new CapturingSpotifyClientWrapper
        {
            PaginateResult = new List<SimpleEpisode> { olderEpisode, validEpisode },
            PaginateAllResult = new List<SimpleEpisode?> { validEpisode, null, olderEpisode }!
        };
        var sut = CreateSut(wrapper);
        var pagedEpisodes = new Paging<SimpleEpisode>
        {
            Items = new List<SimpleEpisode> { olderEpisode, validEpisode },
            Next = null
        };

        // Act
        var result = await sut.PaginateEpisodes(pagedEpisodes, new IndexingContext());

        // Assert
        wrapper.PaginateInvoked.Should().BeTrue();
        wrapper.PaginateAllInvoked.Should().BeTrue();
        result.Episodes.Should().HaveCount(2);
        result.Episodes.Should().OnlyContain(x => x != null);
        result.Episodes.Select(x => x.Id).Should().BeEquivalentTo(validEpisode.Id, olderEpisode.Id);
    }

    [Fact(DisplayName =
        "When SkipSpotifyUrlResolving is set, PaginateEpisodes returns empty without calling the client " +
        "because rate-limit recovery must short-circuit expensive Spotify paging.")]
    public async Task Skip_spotify_url_resolving_returns_empty_without_client_call()
    {
        // Arrange
        var wrapper = new Mock<ISpotifyClientWrapper>();
        var sut = CreateSut(wrapper.Object);
        var indexingContext = new IndexingContext { SkipSpotifyUrlResolving = true };

        // Act
        var result = await sut.PaginateEpisodes(
            new Paging<SimpleEpisode> { Items = [CreateEpisode("episode-1", daysAgo: 1)] },
            indexingContext);

        // Assert
        result.Episodes.Should().BeEmpty();
        wrapper.Verify(
            x => x.Paginate(
                It.IsAny<IPaginatable<SimpleEpisode>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<IPaginator?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private SpotifyQueryPaginator CreateSut(ISpotifyClientWrapper wrapper) =>
        new(
            wrapper,
            NullLogger<SpotifyQueryPaginator>.Instance,
            NullLogger<SimpleEpisodePaginator>.Instance);

    private SimpleEpisode CreateEpisode(string id, int daysAgo) =>
        new()
        {
            Id = id,
            Name = _fixture.CreateTitle(),
            ReleaseDate = DomainTestFixture.UtcDateDaysAgo(daysAgo).ToString("yyyy-MM-dd"),
            Type = ItemType.Episode
        };

    private sealed class CapturingSpotifyClientWrapper : ISpotifyClientWrapper
    {
        public IList<SimpleEpisode>? PaginateResult { get; init; }
        public IList<SimpleEpisode>? PaginateAllResult { get; init; }

        public Task<IList<T>?> Paginate<T>(
            IPaginatable<T> firstPage,
            IndexingContext indexingContext,
            IPaginator? paginator = null,
            CancellationToken cancel = default)
        {
            PaginateInvoked = true;
            return Task.FromResult(typeof(T) == typeof(SimpleEpisode)
                ? (IList<T>?)(object)PaginateResult!
                : null);
        }

        public Task<IList<T>?> PaginateAll<T>(IPaginatable<T> firstPage, IndexingContext indexingContext)
        {
            PaginateAllInvoked = true;
            return Task.FromResult(typeof(T) == typeof(SimpleEpisode)
                ? (IList<T>?)(object)PaginateAllResult!
                : null);
        }

        public bool PaginateInvoked { get; private set; }
        public bool PaginateAllInvoked { get; private set; }

        public Task<IList<T>?> PaginateAll<T, T1>(
            IPaginatable<T, T1> firstPage,
            Func<T1, IPaginatable<T, T1>> mapper,
            IndexingContext indexingContext) =>
            throw new NotImplementedException();

        public Task<Paging<SimpleEpisode>?> GetShowEpisodes(
            string showId,
            ShowEpisodesRequest request,
            IndexingContext indexingContext,
            CancellationToken cancel = default) =>
            throw new NotImplementedException();

        public Task<List<SimpleShow>> GetSimpleShows(
            SearchRequest request,
            IndexingContext indexingContext,
            CancellationToken cancel = default) =>
            throw new NotImplementedException();

        public Task<SearchResponse?> GetSearchResponse(
            SearchRequest request,
            IndexingContext indexingContext,
            CancellationToken cancel = default) =>
            throw new NotImplementedException();

        public Task<FullShow?> GetFullShow(
            string showId,
            ShowRequest request,
            IndexingContext indexingContext,
            CancellationToken cancel = default) =>
            throw new NotImplementedException();

        public Task<FullEpisode?> GetFullEpisode(
            string episodeId,
            EpisodeRequest request,
            IndexingContext indexingContext,
            CancellationToken cancel = default) =>
            throw new NotImplementedException();

        public Task<EpisodesResponse?> GetSeveral(
            EpisodesRequest request,
            IndexingContext indexingContext,
            CancellationToken cancel = default) =>
            throw new NotImplementedException();

        public Task<Paging<SimpleEpisode, SearchResponse>?> FindEpisodes(
            SearchRequest request,
            IndexingContext indexingContext,
            CancellationToken cancel = default) =>
            throw new NotImplementedException();
    }
}
