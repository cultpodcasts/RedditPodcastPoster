using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Spotify.Paginators;
using RedditPodcastPoster.PodcastServices.Spotify.Tests.Support;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Paginators;

/// <summary>
/// SimpleEpisodePaginator must tolerate null episode slots and all-null pages from the Spotify API.
/// </summary>
public class SimpleEpisodePaginatorRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When a page contains a null episode slot, SimpleEpisodePaginator skips it and yields only valid episodes " +
        "because null API entries must not propagate into indexing.")]
    public async Task Skips_null_slots_on_first_page()
    {
        // Arrange
        var validEpisode = CreateEpisode("episode-1", daysAgo: 1);
        var firstPage = new Paging<SimpleEpisode>
        {
            Items = [(SimpleEpisode)null!, validEpisode],
            Next = null
        };
        var sut = new SimpleEpisodePaginator(
            releasedSince: null,
            isInReverseOrder: false,
            NullLogger<SimpleEpisodePaginator>.Instance);
        var connector = new FakeSpotifyApiConnector(new Dictionary<string, object>());

        // Act
        var results = await sut.Paginate(firstPage, connector).ToListAsync();

        // Assert
        results.Should().ContainSingle().Which.Id.Should().Be(validEpisode.Id);
    }

    [Fact(DisplayName =
        "When a page is entirely null slots but has a next link, SimpleEpisodePaginator continues paging " +
        "because Spotify sometimes returns sparse pages before real episodes appear.")]
    public async Task Continues_paging_when_page_is_all_null_slots()
    {
        // Arrange
        const string nextUrl = "https://api.spotify.com/v1/shows/show/episodes?offset=1";
        var validEpisode = CreateEpisode("episode-1", daysAgo: 1);
        var firstPage = new Paging<SimpleEpisode>
        {
            Items = [(SimpleEpisode)null!],
            Next = nextUrl
        };
        var secondPage = new Paging<SimpleEpisode>
        {
            Items = [validEpisode],
            Next = null
        };
        var connector = new FakeSpotifyApiConnector(new Dictionary<string, object> { [nextUrl] = secondPage });
        var sut = new SimpleEpisodePaginator(
            releasedSince: null,
            isInReverseOrder: true,
            NullLogger<SimpleEpisodePaginator>.Instance);

        // Act
        var results = await sut.Paginate(firstPage, connector).ToListAsync();

        // Assert
        results.Should().ContainSingle().Which.Id.Should().Be(validEpisode.Id);
    }

    [Fact(DisplayName =
        "When releasedSince is set, SimpleEpisodePaginator excludes episodes older than the cutoff " +
        "even when null slots appear on the same page.")]
    public async Task Applies_released_since_filter_with_null_slots_present()
    {
        // Arrange
        var recentEpisode = CreateEpisode("recent", daysAgo: 1);
        var oldEpisode = CreateEpisode("old", daysAgo: 30);
        var releasedSince = DomainTestFixture.UtcDateDaysAgo(7);
        var firstPage = new Paging<SimpleEpisode>
        {
            Items = [(SimpleEpisode)null!, oldEpisode, recentEpisode],
            Next = null
        };
        var sut = new SimpleEpisodePaginator(
            releasedSince,
            isInReverseOrder: false,
            NullLogger<SimpleEpisodePaginator>.Instance);
        var connector = new FakeSpotifyApiConnector(new Dictionary<string, object>());

        // Act
        var results = await sut.Paginate(firstPage, connector).ToListAsync();

        // Assert
        results.Should().ContainSingle().Which.Id.Should().Be(recentEpisode.Id);
    }

    [Fact(DisplayName =
        "When catalogue order is not reverse-chronological, SimpleEpisodePaginator stops after MaxPages subsequent fetches " +
        "because unordered date-scoped walks must not pull an entire high-volume show catalogue.")]
    public async Task Hard_caps_subsequent_pages_when_not_reverse_chronological()
    {
        // Arrange — register MaxPages+1 subsequent pages; only MaxPages should be fetched
        var pagesByUrl = new Dictionary<string, object>();
        var expectedIds = new List<string> { "ep-0" };
        string? firstNext = null;
        for (var i = 1; i <= SimpleEpisodePaginator.MaxPages + 1; i++)
        {
            var url = $"https://api.spotify.com/v1/shows/show/episodes?offset={i}";
            firstNext ??= url;
            var nextUrl = i <= SimpleEpisodePaginator.MaxPages
                ? $"https://api.spotify.com/v1/shows/show/episodes?offset={i + 1}"
                : null;
            var episode = CreateEpisode($"ep-{i}", daysAgo: i + 1);
            pagesByUrl[url] = new Paging<SimpleEpisode> { Items = [episode], Next = nextUrl };
            if (i <= SimpleEpisodePaginator.MaxPages)
            {
                expectedIds.Add(episode.Id);
            }
        }

        var firstPage = new Paging<SimpleEpisode>
        {
            Items = [CreateEpisode("ep-0", daysAgo: 1)],
            Next = firstNext
        };
        var connector = new FakeSpotifyApiConnector(pagesByUrl);
        var sut = new SimpleEpisodePaginator(
            DomainTestFixture.UtcDateDaysAgo(30),
            isInReverseOrder: false,
            NullLogger<SimpleEpisodePaginator>.Instance);

        // Act
        var results = await sut.Paginate(firstPage, connector).ToListAsync();

        // Assert — first page + MaxPages subsequent fetches; page MaxPages+1 never fetched
        results.Select(x => x.Id).Should().Equal(expectedIds);
        results.Should().HaveCount(SimpleEpisodePaginator.MaxPages + 1);
        results.Should().NotContain(x => x.Id == $"ep-{SimpleEpisodePaginator.MaxPages + 1}");
    }

    [Fact(DisplayName =
        "When catalogue order is reverse-chronological, SimpleEpisodePaginator does not apply a page cap " +
        "and continues paging while episodes remain within the ReleasedSince window.")]
    public async Task Does_not_hard_cap_subsequent_pages_when_reverse_chronological()
    {
        // Arrange — reverse-chrono uses production ctor (no page cap); date window should fetch all pages
        var page1Url = "https://api.spotify.com/v1/shows/show/episodes?offset=1";
        var page2Url = "https://api.spotify.com/v1/shows/show/episodes?offset=2";
        var page3Url = "https://api.spotify.com/v1/shows/show/episodes?offset=3";
        var ep0 = CreateEpisode("ep-0", daysAgo: 1);
        var ep1 = CreateEpisode("ep-1", daysAgo: 2);
        var ep2 = CreateEpisode("ep-2", daysAgo: 3);
        var ep3 = CreateEpisode("ep-3", daysAgo: 4);
        var firstPage = new Paging<SimpleEpisode>
        {
            Items = [ep0],
            Next = page1Url
        };
        var connector = new FakeSpotifyApiConnector(new Dictionary<string, object>
        {
            [page1Url] = new Paging<SimpleEpisode> { Items = [ep1], Next = page2Url },
            [page2Url] = new Paging<SimpleEpisode> { Items = [ep2], Next = page3Url },
            [page3Url] = new Paging<SimpleEpisode> { Items = [ep3], Next = null }
        });
        var sut = new SimpleEpisodePaginator(
            DomainTestFixture.UtcDateDaysAgo(30),
            isInReverseOrder: true,
            NullLogger<SimpleEpisodePaginator>.Instance);

        // Act
        var results = await sut.Paginate(firstPage, connector).ToListAsync();

        // Assert — reverse-chrono has no page cap; all in-window pages fetched
        results.Select(x => x.Id).Should().Equal(ep0.Id, ep1.Id, ep2.Id, ep3.Id);
    }

    private SimpleEpisode CreateEpisode(string id, int daysAgo) =>
        new()
        {
            Id = id,
            Name = _fixture.CreateTitle(),
            ReleaseDate = DomainTestFixture.UtcDateDaysAgo(daysAgo).ToString("yyyy-MM-dd"),
            Type = ItemType.Episode
        };
}
