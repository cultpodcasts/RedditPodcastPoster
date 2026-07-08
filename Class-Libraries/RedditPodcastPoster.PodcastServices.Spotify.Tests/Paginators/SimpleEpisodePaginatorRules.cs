using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Spotify.Paginators;
using RedditPodcastPoster.PodcastServices.Spotify.Tests.Support;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.Paginators;

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

    private SimpleEpisode CreateEpisode(string id, int daysAgo) =>
        new()
        {
            Id = id,
            Name = _fixture.CreateTitle(),
            ReleaseDate = DomainTestFixture.UtcDateDaysAgo(daysAgo).ToString("yyyy-MM-dd"),
            Type = ItemType.Episode
        };
}
