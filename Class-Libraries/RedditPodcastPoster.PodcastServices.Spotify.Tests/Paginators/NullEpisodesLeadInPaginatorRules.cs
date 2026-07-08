using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Spotify.Paginators;
using RedditPodcastPoster.PodcastServices.Spotify.Tests.Support;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.Paginators;

/// <summary>
/// Spotify show-episode pages can contain null slots where an episode object failed to deserialize.
/// </summary>
public class NullEpisodesLeadInPaginatorRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When the first page contains null episode slots, NullEpisodesLeadInPaginator skips them and yields valid episodes " +
        "because null API entries must not abort pagination.")]
    public async Task Skips_null_slots_on_first_page()
    {
        // Arrange
        var validEpisode = CreateEpisode("episode-1", daysAgo: 1);
        var firstPage = new Paging<SimpleEpisode>
        {
            Items = CreateItemsWithNullSlot(validEpisode),
            Next = null
        };
        var sut = new NullEpisodesLeadInPaginator(maxConsecutiveNullEpisodes: 3, limit: 10);
        var connector = new FakeSpotifyApiConnector(new Dictionary<string, object>());

        // Act
        var results = await sut.Paginate(firstPage, connector).ToListAsync();

        // Assert
        results.Should().ContainSingle().Which.Id.Should().Be(validEpisode.Id);
    }

    [Fact(DisplayName =
        "When consecutive null slots appear across pages, NullEpisodesLeadInPaginator stops after the configured threshold " +
        "because lead-in null runs should not page forever.")]
    public async Task Stops_after_max_consecutive_null_episodes()
    {
        // Arrange
        const string nextUrl = "https://api.spotify.com/v1/shows/show/episodes?offset=1";
        var validEpisode = CreateEpisode("episode-1", daysAgo: 1);
        var firstPage = new Paging<SimpleEpisode>
        {
            Items = CreateItemsWithNullSlot(null),
            Next = nextUrl
        };
        var secondPage = new Paging<SimpleEpisode>
        {
            Items = CreateItemsWithNullSlot(null),
            Next = "https://api.spotify.com/v1/shows/show/episodes?offset=2"
        };
        var thirdPage = new Paging<SimpleEpisode>
        {
            Items = CreateItemsWithNullSlot(validEpisode),
            Next = null
        };
        var connector = new FakeSpotifyApiConnector(new Dictionary<string, object>
        {
            [nextUrl] = secondPage,
            [secondPage.Next!] = thirdPage
        });
        var sut = new NullEpisodesLeadInPaginator(maxConsecutiveNullEpisodes: 2, limit: 10);

        // Act
        var results = await sut.Paginate(firstPage, connector).ToListAsync();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When null slots are followed by valid episodes within the limit, NullEpisodesLeadInPaginator yields the valid episodes " +
        "because sparse pages should still surface usable catalogue rows.")]
    public async Task Yields_valid_episodes_from_later_page_after_null_slots()
    {
        // Arrange
        const string nextUrl = "https://api.spotify.com/v1/shows/show/episodes?offset=1";
        var validEpisode = CreateEpisode("episode-1", daysAgo: 1);
        var firstPage = new Paging<SimpleEpisode>
        {
            Items = CreateItemsWithNullSlot(null),
            Next = nextUrl
        };
        var secondPage = new Paging<SimpleEpisode>
        {
            Items = [validEpisode],
            Next = null
        };
        var connector = new FakeSpotifyApiConnector(new Dictionary<string, object> { [nextUrl] = secondPage });
        var sut = new NullEpisodesLeadInPaginator(maxConsecutiveNullEpisodes: 3, limit: 10);

        // Act
        var results = await sut.Paginate(firstPage, connector).ToListAsync();

        // Assert
        results.Should().ContainSingle().Which.Id.Should().Be(validEpisode.Id);
    }

    private SimpleEpisode CreateEpisode(string id, int daysAgo) =>
        new()
        {
            Id = id,
            Name = _fixture.CreateTitle(),
            ReleaseDate = DomainTestFixture.UtcDateDaysAgo(daysAgo).ToString("yyyy-MM-dd"),
            Type = ItemType.Episode
        };

    private static List<SimpleEpisode> CreateItemsWithNullSlot(SimpleEpisode? episode) =>
        episode == null ? [(SimpleEpisode)null!] : [(SimpleEpisode)null!, episode];
}
