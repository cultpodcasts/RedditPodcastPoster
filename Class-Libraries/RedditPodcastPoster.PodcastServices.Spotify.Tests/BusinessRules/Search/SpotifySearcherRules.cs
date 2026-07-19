using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Search;
using RedditPodcastPoster.Text;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Search;

/// <summary>
/// Discovery Spotify search must floor ReleasedSince to UTC day, require whole-word query matches,
/// then hydrate filtered hits via GetSeveral — otherwise discovery emits false positives or skips URLs.
/// </summary>
public class SpotifySearcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When ReleasedSince carries a time-of-day, Search includes an episode released at the floored UTC day start " +
        "because discovery must not exclude same-calendar-day Spotify rows after a mid-day floor.")]
    public async Task Released_since_floors_to_utc_day_before_filter()
    {
        // Arrange
        const string query = "cultists";
        var releasedSince = DomainTestFixture.UtcDateDaysAgo(2).AddHours(15);
        var episodeId = _fixture.CreateSpotifyId();
        var searchHit = CreateSimpleEpisode(
            episodeId,
            name: $"Interview with {query} today",
            release: DomainTestFixture.UtcDateDaysAgo(2));
        var fullEpisode = CreateFullEpisode(episodeId, searchHit.Name, DomainTestFixture.UtcDateDaysAgo(2));
        string[]? getSeveralIds = null;
        var client = CreateClientReturning(
            [searchHit],
            fullEpisode,
            onGetSeveral: ids => getSeveralIds = ids);
        var sut = CreateSut(client.Object);
        var indexingContext = new IndexingContext(ReleasedSince: releasedSince);

        // Act
        var results = await sut.Search(query, indexingContext);

        // Assert
        results.Should().ContainSingle();
        results[0].Id.Should().Be(episodeId);
        getSeveralIds.Should().Equal(episodeId);
    }

    [Fact(DisplayName =
        "When an episode name contains the query as a whole word, Search includes it after hydrate " +
        "because discovery must surface true keyword hits without requiring description matches.")]
    public async Task Whole_word_name_match_is_included_and_hydrated()
    {
        // Arrange
        const string query = "cultists";
        var episodeId = _fixture.CreateSpotifyId();
        var searchHit = CreateSimpleEpisode(
            episodeId,
            name: $"The {query} return",
            release: DomainTestFixture.UtcDateDaysAgo(1));
        var fullEpisode = CreateFullEpisode(episodeId, searchHit.Name, DomainTestFixture.UtcDateDaysAgo(1));
        string[]? getSeveralIds = null;
        var client = CreateClientReturning(
            [searchHit],
            fullEpisode,
            onGetSeveral: ids => getSeveralIds = ids);
        var sut = CreateSut(client.Object);

        // Act
        var results = await sut.Search(query, new IndexingContext(ReleasedSince: DomainTestFixture.UtcDateDaysAgo(3)));

        // Assert
        results.Should().ContainSingle();
        results[0].Id.Should().Be(episodeId);
        results[0].DiscoverServices.Should().Equal(DiscoverService.Spotify);
        results[0].Urls.Spotify.Should().Be(new Uri($"https://open.spotify.com/episode/{episodeId}"));
        getSeveralIds.Should().Equal(episodeId);
    }

    [Fact(DisplayName =
        "When an episode name contains the query only as a substring without word boundaries, Search excludes it " +
        "because discovery must not emit false positives from partial token matches.")]
    public async Task Substring_without_word_boundary_is_excluded()
    {
        // Arrange
        const string query = "cult";
        var episodeId = _fixture.CreateSpotifyId();
        var searchHit = CreateSimpleEpisode(
            episodeId,
            name: "The culture war continues",
            release: DomainTestFixture.UtcDateDaysAgo(1));
        var client = CreateClientReturning([searchHit], fullEpisode: null);
        var sut = CreateSut(client.Object);

        // Act
        var results = await sut.Search(query, new IndexingContext(ReleasedSince: DomainTestFixture.UtcDateDaysAgo(3)));

        // Assert
        results.Should().BeEmpty();
        client.Verify(
            x => x.GetSeveral(
                It.IsAny<EpisodesRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When only the episode description contains the query as a whole word, Search includes and hydrates it " +
        "because discovery matches name or description with the same word-boundary rule.")]
    public async Task Whole_word_description_match_is_included()
    {
        // Arrange
        const string query = "cultists";
        var episodeId = _fixture.CreateSpotifyId();
        var searchHit = CreateSimpleEpisode(
            episodeId,
            name: _fixture.CreateTitle(),
            description: $"Discussion of {query} and recovery",
            release: DomainTestFixture.UtcDateDaysAgo(1));
        var fullEpisode = CreateFullEpisode(episodeId, searchHit.Name, DomainTestFixture.UtcDateDaysAgo(1));
        var client = CreateClientReturning([searchHit], fullEpisode);
        var sut = CreateSut(client.Object);

        // Act
        var results = await sut.Search(query, new IndexingContext(ReleasedSince: DomainTestFixture.UtcDateDaysAgo(3)));

        // Assert
        results.Should().ContainSingle();
        results[0].Id.Should().Be(episodeId);
    }

    [Fact(DisplayName =
        "When FindEpisodes returns null, Search returns an empty list and does not call GetSeveral " +
        "because there are no search hits to hydrate.")]
    public async Task Null_search_results_return_empty_without_hydrate()
    {
        // Arrange
        var client = new Mock<ISpotifyClientWrapper>(MockBehavior.Strict);
        client
            .Setup(x => x.FindEpisodes(
                It.IsAny<SearchRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Paging<SimpleEpisode, SearchResponse>?)null);
        var sut = CreateSut(client.Object);

        // Act
        var results = await sut.Search("cultists", new IndexingContext(ReleasedSince: DomainTestFixture.UtcDateDaysAgo(3)));

        // Assert
        results.Should().BeEmpty();
        client.Verify(
            x => x.GetSeveral(
                It.IsAny<EpisodesRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When PaginateAll returns no episodes after filtering, Search returns empty and skips GetSeveral " +
        "because hydrate must only run for matching discovery candidates.")]
    public async Task Empty_paginate_results_skip_get_several()
    {
        // Arrange
        var client = CreateClientReturning([], fullEpisode: null);
        var sut = CreateSut(client.Object);

        // Act
        var results = await sut.Search("cultists", new IndexingContext(ReleasedSince: DomainTestFixture.UtcDateDaysAgo(3)));

        // Assert
        results.Should().BeEmpty();
        client.Verify(
            x => x.GetSeveral(
                It.IsAny<EpisodesRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private Mock<ISpotifyClientWrapper> CreateClientReturning(
        IList<SimpleEpisode> paginateResults,
        FullEpisode? fullEpisode,
        Action<string[]>? onGetSeveral = null)
    {
        var client = new Mock<ISpotifyClientWrapper>();
        client
            .Setup(x => x.FindEpisodes(
                It.IsAny<SearchRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Paging<SimpleEpisode, SearchResponse> { Items = paginateResults.ToList() });
        client
            .Setup(x => x.PaginateAll(
                It.IsAny<IPaginatable<SimpleEpisode, SearchResponse>>(),
                It.IsAny<Func<SearchResponse, IPaginatable<SimpleEpisode, SearchResponse>>>(),
                It.IsAny<IndexingContext>()))
            .ReturnsAsync(paginateResults);
        client
            .Setup(x => x.GetSeveral(
                It.IsAny<EpisodesRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((EpisodesRequest request, IndexingContext _, CancellationToken _) =>
            {
                onGetSeveral?.Invoke(request.Ids.ToArray());
                if (fullEpisode == null)
                {
                    return null;
                }

                return new EpisodesResponse { Episodes = [fullEpisode] };
            });
        return client;
    }

    [Fact(DisplayName =
        "When a search hit has IsPlayable=false, Search excludes it before hydrate " +
        "because paywalled Spotify episodes must not enter discovery results.")]
    public async Task Non_playable_search_hit_is_excluded()
    {
        // Arrange
        const string query = "cultists";
        var episodeId = _fixture.CreateSpotifyId();
        var searchHit = CreateSimpleEpisode(
            episodeId,
            name: $"Interview with {query} today",
            release: DomainTestFixture.UtcDateDaysAgo(1),
            isPlayable: false);
        var client = CreateClientReturning([searchHit], fullEpisode: null);
        var sut = CreateSut(client.Object);

        // Act
        var results = await sut.Search(query, new IndexingContext(ReleasedSince: DomainTestFixture.UtcDateDaysAgo(3)));

        // Assert
        results.Should().BeEmpty();
        client.Verify(
            x => x.GetSeveral(
                It.IsAny<EpisodesRequest>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private SimpleEpisode CreateSimpleEpisode(
        string id,
        string name,
        DateTime release,
        string? description = null,
        bool isPlayable = true) =>
        new()
        {
            Id = id,
            Name = name,
            Description = description ?? _fixture.CreateTitle(),
            DurationMs = (int)_fixture.CreateDuration().TotalMilliseconds,
            ReleaseDate = release.ToString("yyyy-MM-dd"),
            Type = ItemType.Episode,
            IsPlayable = isPlayable
        };

    private FullEpisode CreateFullEpisode(string episodeId, string title, DateTime release)
    {
        var showId = _fixture.CreateSpotifyId();
        return new FullEpisode
        {
            Id = episodeId,
            Name = title,
            HtmlDescription = $"<p>{_fixture.CreateTitle()}</p>",
            DurationMs = (int)_fixture.CreateDuration().TotalMilliseconds,
            ReleaseDate = release.ToString("yyyy-MM-dd"),
            Explicit = false,
            IsPlayable = true,
            ExternalUrls = new Dictionary<string, string>
            {
                ["spotify"] = _fixture.DefaultSpotifyUrl(episodeId).ToString()
            },
            Images = [],
            Show = new SimpleShow
            {
                Id = showId,
                Name = _fixture.CreateTitle(),
                Description = _fixture.CreateTitle(),
                // 'publisher' removed from Spotify show objects (Feb 2026); still exercised for pass-through.
#pragma warning disable CS0618
                Publisher = _fixture.CreateTitle()
#pragma warning restore CS0618
            }
        };
    }

    private static SpotifySearcher CreateSut(ISpotifyClientWrapper client) =>
        new(
            client,
            new HtmlSanitiser(NullLogger<HtmlSanitiser>.Instance),
            NullLogger<SpotifySearcher>.Instance);
}
