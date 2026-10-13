using FluentAssertions;
using Moq;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Client;
using RedditPodcastPoster.PodcastServices.Spotify.Providers;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Providers;

/// <summary>
/// Pass cache behind the shared catalogue policy's recent-show-feed stage (docs/catalogue-pagination.md):
/// one bounded GetShowEpisodes page per show per pass, never a catalogue walk.
/// </summary>
public class SpotifyRecentShowEpisodesCacheRules
{
    private const string ShowId = "3fy5dEPagR2T8MPeS7ieAM";
    private readonly Mock<ISpotifyClientWrapper> _client = new();

    [Fact(DisplayName =
        "Within one podcast pass the recent show page is fetched once per show, market and page size, with Limit set to the page size.")]
    public async Task Fetches_once_per_show_per_pass()
    {
        // Arrange
        Stub(new Paging<SimpleEpisode> { Items = [new SimpleEpisode { Id = "a" }] });
        var sut = new SpotifyRecentShowEpisodesCache(_client.Object);

        // Act
        var first = await sut.GetRecentShowEpisodes(ShowId, "GB", 50, new IndexingContext());
        var second = await sut.GetRecentShowEpisodes(ShowId, "GB", 50, new IndexingContext());

        // Assert
        first.Should().ContainSingle();
        second.Should().BeSameAs(first);
        _client.Verify(x => x.GetShowEpisodes(ShowId, It.Is<ShowEpisodesRequest>(r => r.Limit == 50 && r.Market == "GB"),
            It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName =
        "A failed GetShowEpisodes call (null) returns no candidates and is not cached, so the next lookup in the pass retries.")]
    public async Task Null_results_are_not_cached()
    {
        // Arrange
        Stub(null);
        var sut = new SpotifyRecentShowEpisodesCache(_client.Object);

        // Act
        var first = await sut.GetRecentShowEpisodes(ShowId, "GB", 50, new IndexingContext());
        await sut.GetRecentShowEpisodes(ShowId, "GB", 50, new IndexingContext());

        // Assert
        first.Should().BeEmpty();
        VerifyFetches(Times.Exactly(2));
    }

    [Fact(DisplayName =
        "While SkipSpotifyUrlResolving (the Spotify circuit breaker) is set, the cache returns no candidates without calling Spotify.")]
    public async Task Skip_flag_makes_no_call()
    {
        // Arrange
        var sut = new SpotifyRecentShowEpisodesCache(_client.Object);

        // Act
        var result = await sut.GetRecentShowEpisodes(ShowId, "GB", 50, new IndexingContext { SkipSpotifyUrlResolving = true });

        // Assert
        result.Should().BeEmpty();
        VerifyFetches(Times.Never());
    }

    [Fact(DisplayName =
        "ClearPassCache (called between podcast passes) discards cached pages, so the next pass fetches the show page again.")]
    public async Task Clear_pass_cache_forces_refetch()
    {
        // Arrange
        Stub(new Paging<SimpleEpisode> { Items = [new SimpleEpisode { Id = "a" }] });
        var sut = new SpotifyRecentShowEpisodesCache(_client.Object);
        await sut.GetRecentShowEpisodes(ShowId, "GB", 50, new IndexingContext());

        // Act
        sut.ClearPassCache();
        await sut.GetRecentShowEpisodes(ShowId, "GB", 50, new IndexingContext());

        // Assert
        VerifyFetches(Times.Exactly(2));
    }

    private void Stub(Paging<SimpleEpisode>? page) =>
        _client.Setup(x => x.GetShowEpisodes(ShowId, It.IsAny<ShowEpisodesRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

    private void VerifyFetches(Times times) =>
        _client.Verify(x => x.GetShowEpisodes(ShowId, It.IsAny<ShowEpisodesRequest>(), It.IsAny<IndexingContext>(), It.IsAny<CancellationToken>()), times);
}
