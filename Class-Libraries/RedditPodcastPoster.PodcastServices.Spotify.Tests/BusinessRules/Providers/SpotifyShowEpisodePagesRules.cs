using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Providers;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Providers;

/// <summary>
/// Newest-first Spotify show pages of 50, pass-cached, each request behind the 10 s guard; oldest-first ("expensive")
/// catalogues are read from the end using Paging.Total so page 0 is always the newest.
/// </summary>
public class SpotifyShowEpisodePagesRules
{
    private static readonly DateTime Newest = DomainTestFixture.UtcDateDaysAgo(1);
    private readonly AutoMocker _mocker = new();

    private Mock<ISpotifyClientWrapper> _client => _mocker.GetMock<ISpotifyClientWrapper>();

    private SpotifyShowEpisodePages Sut() => new(_client.Object, CatalogueTestServices.CreateGuard());

    private static List<SimpleEpisode> Daily(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new SimpleEpisode { Id = $"e{i}", Name = $"Episode {i}", ReleaseDate = Newest.AddDays(-i).ToString("yyyy-MM-dd") })
            .ToList();

    private void Serve(IReadOnlyList<SimpleEpisode> apiOrder) =>
        _client.Setup(x => x.GetShowEpisodes(It.IsAny<string>(), It.IsAny<ShowEpisodesRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, ShowEpisodesRequest r, IndexingContext _, CancellationToken _) => new Paging<SimpleEpisode>
            {
                Items = apiOrder.Skip(r.Offset ?? 0).Take(r.Limit ?? 20).ToList(),
                Total = apiOrder.Count
            });

    private int Calls => _client.Invocations.Count;

    [Fact(DisplayName = "A newest-first show is offset-paged: page 1 is items 50-99, HasMore follows Total, and Total is reported.")]
    public async Task Newest_first_offset_paging()
    {
        // Arrange
        Serve(Daily(120));

        // Act
        var page1 = await Sut().GetNewestPage("show", "GB", 1, new IndexingContext());

        // Assert
        page1!.Items.Select(x => x.Id).Should().StartWith("e50").And.HaveCount(50);
        page1.HasMore.Should().BeTrue();
        page1.TotalEpisodes.Should().Be(120);
        page1.IsNewestFirst.Should().BeTrue();
    }

    [Fact(DisplayName = "An oldest-first show is read from the end: page 0 is the newest 50, reversed to newest first.")]
    public async Task Oldest_first_read_from_end()
    {
        // Arrange
        var newestFirst = Daily(120);
        var apiOrder = Enumerable.Reverse(newestFirst).ToList();
        Serve(apiOrder);
        var sut = Sut();

        // Act
        var page0 = await sut.GetNewestPage("show", "GB", 0, new IndexingContext());
        var page2 = await sut.GetNewestPage("show", "GB", 2, new IndexingContext());

        // Assert
        page0!.Items.Select(x => x.Id).Should().Equal(newestFirst.Take(50).Select(x => x.Id));
        page0.HasMore.Should().BeTrue();
        page2!.Items.Select(x => x.Id).Should().Equal(newestFirst.Skip(100).Select(x => x.Id));
        page2.HasMore.Should().BeFalse();
    }

    [Fact(DisplayName = "A page is fetched once per podcast pass and fetched again after the pass cache is cleared.")]
    public async Task Pages_are_pass_cached()
    {
        // Arrange
        Serve(Daily(120));
        var sut = Sut();

        // Act
        await sut.GetNewestPage("show", "GB", 0, new IndexingContext());
        await sut.GetNewestPage("show", "GB", 0, new IndexingContext());
        var beforeClear = Calls;
        sut.ClearPassCache();
        await sut.GetNewestPage("show", "GB", 0, new IndexingContext());

        // Assert
        beforeClear.Should().Be(1);
        Calls.Should().Be(2);
    }

    [Fact(DisplayName = "A timed-out request returns null and is not cached, so a later lookup can retry it.")]
    public async Task Failed_page_is_not_cached()
    {
        // Arrange
        _client.SetupSequence(x => x.GetShowEpisodes(It.IsAny<string>(), It.IsAny<ShowEpisodesRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException())
            .ReturnsAsync(new Paging<SimpleEpisode> { Items = Daily(10), Total = 10 });
        var sut = Sut();

        // Act
        var first = await sut.GetNewestPage("show", "GB", 0, new IndexingContext());
        var second = await sut.GetNewestPage("show", "GB", 0, new IndexingContext());

        // Assert
        first.Should().BeNull();
        second!.Items.Should().HaveCount(10);
    }

    [Fact(DisplayName = "While SkipSpotifyUrlResolving is set, no page is fetched.")]
    public async Task Skip_spotify_fetches_nothing()
    {
        // Arrange
        var context = new IndexingContext { SkipSpotifyUrlResolving = true };

        // Act
        var page = await Sut().GetNewestPage("show", "GB", 0, context);

        // Assert
        page.Should().BeNull();
        Calls.Should().Be(0);
    }
}
