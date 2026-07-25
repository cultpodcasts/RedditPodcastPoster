using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Spotify.Paginators;
using RedditPodcastPoster.PodcastServices.Spotify.Tests.Support;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Paginators;

public class AscendingEpisodePaginatorRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "An ascending Spotify catalogue jumps directly to its final page and walks backwards to ReleasedSince " +
        "because recent episodes are at the end and paging forward from offset zero wastes quota.")]
    public async Task Jumps_to_final_page_then_walks_back_to_cutoff()
    {
        // Arrange — 103 episodes means the final 50-item page begins at offset 100.
        const string firstHref =
            "https://api.spotify.com/v1/shows/show/episodes?market=GB&limit=50&offset=0";
        const string finalUrl =
            "https://api.spotify.com/v1/shows/show/episodes?market=GB&limit=50&offset=100";
        const string previousUrl =
            "https://api.spotify.com/v1/shows/show/episodes?market=GB&limit=50&offset=50";

        var recentOne = CreateEpisode("recent-1", daysAgo: 1);
        var recentTwo = CreateEpisode("recent-2", daysAgo: 2);
        var old = CreateEpisode("old", daysAgo: 30);
        var firstPage = new Paging<SimpleEpisode>
        {
            Href = firstHref,
            Items = [CreateEpisode("oldest", daysAgo: 3000)],
            Limit = 50,
            Offset = 0,
            Total = 103,
            Next = "https://api.spotify.com/v1/shows/show/episodes?market=GB&limit=50&offset=50"
        };
        var connector = new FakeSpotifyApiConnector(new Dictionary<string, object>
        {
            [finalUrl] = new Paging<SimpleEpisode>
            {
                Href = finalUrl,
                Items = [recentTwo, recentOne],
                Limit = 50,
                Offset = 100,
                Total = 103,
                Previous = previousUrl
            },
            [previousUrl] = new Paging<SimpleEpisode>
            {
                Href = previousUrl,
                Items = [old],
                Limit = 50,
                Offset = 50,
                Total = 103,
                Previous = firstHref
            }
        });
        var sut = CreateSut(DomainTestFixture.UtcDateDaysAgo(3));

        // Act
        var results = await sut.Paginate(firstPage, connector).ToListAsync();

        // Assert
        results.Select(x => x.Id).Should().BeEquivalentTo(recentOne.Id, recentTwo.Id);
        connector.RequestedUrls.Should().Equal(finalUrl, previousUrl);
        connector.RequestedUrls.Should().NotContain(firstHref);
    }

    [Fact(DisplayName =
        "An exact multiple catalogue size jumps to total minus limit " +
        "because offset equal to total would be an empty page.")]
    public async Task Exact_multiple_uses_last_non_empty_offset()
    {
        const string firstHref =
            "https://api.spotify.com/v1/shows/show/episodes?limit=50&offset=0";
        const string finalUrl =
            "https://api.spotify.com/v1/shows/show/episodes?limit=50&offset=50";
        var recent = CreateEpisode("recent", daysAgo: 1);
        var firstPage = new Paging<SimpleEpisode>
        {
            Href = firstHref,
            Items = [CreateEpisode("oldest", daysAgo: 3000)],
            Limit = 50,
            Offset = 0,
            Total = 100
        };
        var connector = new FakeSpotifyApiConnector(new Dictionary<string, object>
        {
            [finalUrl] = new Paging<SimpleEpisode>
            {
                Href = finalUrl,
                Items = [recent, CreateEpisode("old", daysAgo: 30)],
                Limit = 50,
                Offset = 50,
                Total = 100,
                Previous = firstHref
            }
        });
        var sut = CreateSut(DomainTestFixture.UtcDateDaysAgo(3));

        var results = await sut.Paginate(firstPage, connector).ToListAsync();

        results.Select(x => x.Id).Should().Equal(recent.Id);
        connector.RequestedUrls.Should().Equal(finalUrl);
    }

    private static AscendingEpisodePaginator CreateSut(DateTime releasedSince) =>
        new(
            releasedSince,
            NullLogger<AscendingEpisodePaginator>.Instance,
            new SimpleEpisodePaginator(
                releasedSince,
                isInReverseOrder: false,
                NullLogger<SimpleEpisodePaginator>.Instance));

    private SimpleEpisode CreateEpisode(string id, int daysAgo) =>
        new()
        {
            Id = id,
            Name = _fixture.CreateTitle(),
            ReleaseDate = DomainTestFixture.UtcDateDaysAgo(daysAgo).ToString("yyyy-MM-dd"),
            Type = ItemType.Episode
        };
}
