using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Paginators;
using SpotifyAPI.Web;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Paginators;

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

    [Fact(DisplayName =
        "When ReleasedSince is set and episodes are not reverse-chronological, PaginateEpisodes must not call PaginateAll " +
        "because a date-scoped lookup must not pull the full catalogue (discovery timeouts).")]
    public async Task Released_since_skips_paginate_all_even_when_expensive()
    {
        // Arrange â€” older episode before newer breaks reverse-time-order detection
        var older = CreateEpisode("episode-older", daysAgo: 10);
        var newer = CreateEpisode("episode-newer", daysAgo: 1);
        var wrapper = new CapturingSpotifyClientWrapper
        {
            PaginateResult = new List<SimpleEpisode> { older, newer }
        };
        var sut = CreateSut(wrapper);
        var pagedEpisodes = new Paging<SimpleEpisode>
        {
            Items = new List<SimpleEpisode> { older, newer },
            Next = "https://api.spotify.com/v1/shows/show-1/episodes?offset=2"
        };
        var indexingContext = new IndexingContext(ReleasedSince: DateTime.UtcNow.Date.AddDays(-3));

        // Act
        var result = await sut.PaginateEpisodes(pagedEpisodes, indexingContext);

        // Assert
        wrapper.PaginateInvoked.Should().BeTrue();
        wrapper.PaginateAllInvoked.Should().BeFalse();
        result.ExpensiveQueryFound.Should().BeTrue();
        result.Episodes.Should().OnlyContain(x => x.Id == newer.Id);
    }

    [Fact(DisplayName =
        "When ReleasedSince is null and Next contains the singular /show/ path, PaginateEpisodes rewrites it to /shows/ before PaginateAll " +
        "because Spotify's show episodes next-link must use the plural shows resource.")]
    public async Task Null_released_since_rewrites_singular_show_next_before_paginate_all()
    {
        // Arrange
        var episode = CreateEpisode("episode-1", daysAgo: 1);
        var wrapper = new CapturingSpotifyClientWrapper
        {
            PaginateResult = new List<SimpleEpisode> { episode },
            PaginateAllResult = new List<SimpleEpisode> { episode }
        };
        var sut = CreateSut(wrapper);
        var pagedEpisodes = new Paging<SimpleEpisode>
        {
            Items = new List<SimpleEpisode> { episode },
            Next = "https://api.spotify.com/v1/show/show-1/episodes?offset=2"
        };

        // Act
        await sut.PaginateEpisodes(pagedEpisodes, new IndexingContext());

        // Assert
        wrapper.PaginateAllInvoked.Should().BeTrue();
        wrapper.CapturedPaginateAllNext.Should().Be("https://api.spotify.com/v1/shows/show-1/episodes?offset=2");
        pagedEpisodes.Next.Should().Be("https://api.spotify.com/v1/shows/show-1/episodes?offset=2");
    }

    [Fact(DisplayName =
        "When ReleasedSince carries a time-of-day, PaginateEpisodes includes an episode released at that UTC calendar day start " +
        "because date-scoped pagination compares against ReleasedSince.Date, not the mid-day clock time.")]
    public async Task Released_since_time_of_day_uses_date_truncate()
    {
        // Arrange
        var boundaryDay = DomainTestFixture.UtcDateDaysAgo(2);
        var onBoundary = CreateEpisode("episode-on-boundary", boundaryDay);
        var older = CreateEpisode("episode-older", DomainTestFixture.UtcDateDaysAgo(5));
        var wrapper = new CapturingSpotifyClientWrapper
        {
            PaginateResult = new List<SimpleEpisode> { onBoundary, older }
        };
        var sut = CreateSut(wrapper);
        var pagedEpisodes = new Paging<SimpleEpisode>
        {
            Items = new List<SimpleEpisode> { onBoundary, older },
            Next = null
        };
        var indexingContext = new IndexingContext(ReleasedSince: boundaryDay.AddHours(15));

        // Act
        var result = await sut.PaginateEpisodes(pagedEpisodes, indexingContext);

        // Assert
        result.Episodes.Select(x => x.Id).Should().Equal(onBoundary.Id);
        wrapper.PaginateAllInvoked.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When reverse-chronological pages already end before ReleasedSince, PaginateEpisodes does not keep calling Paginate for growth " +
        "because the growth loop must stop once the oldest seen release is older than the slot window.")]
    public async Task Reverse_chrono_growth_stops_when_oldest_before_released_since()
    {
        // Arrange â€” newest-first (reverse chrono); oldest already before ReleasedSince
        var newer = CreateEpisode("episode-newer", daysAgo: 1);
        var older = CreateEpisode("episode-older", daysAgo: 10);
        var wrapper = new CapturingSpotifyClientWrapper
        {
            PaginateResult = new List<SimpleEpisode> { newer, older }
        };
        var sut = CreateSut(wrapper);
        var pagedEpisodes = new Paging<SimpleEpisode>
        {
            Items = new List<SimpleEpisode> { newer, older },
            Next = "https://api.spotify.com/v1/shows/show-1/episodes?offset=2"
        };
        var indexingContext = new IndexingContext(ReleasedSince: DomainTestFixture.UtcDateDaysAgo(3));

        // Act
        var result = await sut.PaginateEpisodes(pagedEpisodes, indexingContext);

        // Assert
        wrapper.PaginateCallCount.Should().Be(1);
        result.ExpensiveQueryFound.Should().BeFalse();
        result.Episodes.Select(x => x.Id).Should().Equal(newer.Id);
    }

    private SpotifyQueryPaginator CreateSut(ISpotifyClientWrapper wrapper) =>
        new(
            wrapper,
            NullLogger<SpotifyQueryPaginator>.Instance,
            NullLogger<SimpleEpisodePaginator>.Instance);

    private SimpleEpisode CreateEpisode(string id, int daysAgo) =>
        CreateEpisode(id, DomainTestFixture.UtcDateDaysAgo(daysAgo));

    private SimpleEpisode CreateEpisode(string id, DateTime release) =>
        new()
        {
            Id = id,
            Name = _fixture.CreateTitle(),
            ReleaseDate = release.ToString("yyyy-MM-dd"),
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
            PaginateCallCount++;
            return Task.FromResult(typeof(T) == typeof(SimpleEpisode)
                ? (IList<T>?)(object)PaginateResult!
                : null);
        }

        public Task<IList<T>?> PaginateAll<T>(IPaginatable<T> firstPage, IndexingContext indexingContext)
        {
            PaginateAllInvoked = true;
            if (firstPage is Paging<SimpleEpisode> paging)
            {
                CapturedPaginateAllNext = paging.Next;
            }

            return Task.FromResult(typeof(T) == typeof(SimpleEpisode)
                ? (IList<T>?)(object)PaginateAllResult!
                : null);
        }

        public bool PaginateInvoked { get; private set; }
        public bool PaginateAllInvoked { get; private set; }
        public int PaginateCallCount { get; private set; }
        public string? CapturedPaginateAllNext { get; private set; }

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
