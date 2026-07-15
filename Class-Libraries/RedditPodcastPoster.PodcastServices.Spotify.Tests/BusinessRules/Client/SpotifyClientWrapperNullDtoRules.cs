using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.DependencyInjection;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Client;

/// <summary>
/// Spotify search responses can contain null show entries despite SDK list typing.
/// </summary>
public class SpotifyClientWrapperNullDtoRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When search results contain null SimpleShow entries, GetSimpleShows filters them out " +
        "because null API entries must not break podcast discovery.")]
    public async Task Get_simple_shows_filters_null_show_entries()
    {
        // Arrange
        var validShow = new SimpleShow
        {
            Id = _fixture.CreateSpotifyId(),
            Name = _fixture.CreateTitle()
        };
        var searchResponse = new SearchResponse
        {
            Shows = new Paging<SimpleShow, SearchResponse>
            {
                Items = [null, validShow]
            }
        };
        var spotifyClient = new Mock<ISpotifyClient>();
        spotifyClient
            .Setup(x => x.Search.Item(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResponse);
        var provider = new Mock<IAsyncInstance<ISpotifyClient>>();
        provider.Setup(x => x.GetAsync()).ReturnsAsync(spotifyClient.Object);
        var sut = new SpotifyClientWrapper(provider.Object, NullLogger<SpotifyClientWrapper>.Instance);

        // Act
        var results = await sut.GetSimpleShows(
            new SearchRequest(SearchRequest.Types.Show, validShow.Name),
            new IndexingContext());

        // Assert
        results.Should().ContainSingle().Which.Id.Should().Be(validShow.Id);
    }

    [Fact(DisplayName =
        "When search response Shows Items is null, GetSimpleShows returns an empty list " +
        "because missing show collections must be treated as no matches.")]
    public async Task Get_simple_shows_returns_empty_when_items_null()
    {
        // Arrange
        var searchResponse = new SearchResponse
        {
            Shows = new Paging<SimpleShow, SearchResponse> { Items = null! }
        };
        var spotifyClient = new Mock<ISpotifyClient>();
        spotifyClient
            .Setup(x => x.Search.Item(It.IsAny<SearchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResponse);
        var provider = new Mock<IAsyncInstance<ISpotifyClient>>();
        provider.Setup(x => x.GetAsync()).ReturnsAsync(spotifyClient.Object);
        var sut = new SpotifyClientWrapper(provider.Object, NullLogger<SpotifyClientWrapper>.Instance);

        // Act
        var results = await sut.GetSimpleShows(
            new SearchRequest(SearchRequest.Types.Show, "example"),
            new IndexingContext());

        // Assert
        results.Should().BeEmpty();
    }
}
