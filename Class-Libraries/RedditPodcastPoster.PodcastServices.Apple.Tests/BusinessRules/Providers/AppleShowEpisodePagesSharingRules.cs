using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Apple.Models;
using RedditPodcastPoster.PodcastServices.Apple.Providers;

namespace RedditPodcastPoster.PodcastServices.Apple.Tests.BusinessRules.Providers;

/// <summary>
/// Apple amp-api show pages are shared by every lookup in one scope (one apply/submit batch, one index pass), from the
/// real case: submission 4a7d8838 looked up two episodes of The Compendium (159 episodes, 16 pages of 10).
/// </summary>
public class AppleShowEpisodePagesSharingRules
{
    private const long Compendium = 1520000000;
    /// <summary>amp-api default page size is 10 (no limit parameter is sent): 159 episodes -> 16 pages.</summary>
    private sealed class CountingService : IApplePodcastService
    {
        public int PageCalls { get; private set; }

        public Task<IEnumerable<AppleEpisode>?> GetEpisodes(ApplePodcastId podcastId, IndexingContext indexingContext,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AppleEpisode?> GetEpisode(long episodeId, IndexingContext indexingContext,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<AppleEpisodePage?> GetEpisodePage(ApplePodcastId podcastId, string? next,
            CancellationToken cancellationToken = default)
        {
            PageCalls++;
            var index = next == null ? 0 : int.Parse(next);
            var episodes = Enumerable.Range(index * 10, Math.Min(10, 159 - index * 10))
                .Select(i => new AppleEpisode(i, $"Episode {i}", DomainTestFixture.UtcDateDaysAgo(7 * i),
                    TimeSpan.FromMinutes(50), new Uri($"https://podcasts.apple.com/e/{i}"), string.Empty, false))
                .ToList();
            return Task.FromResult<AppleEpisodePage?>(
                new AppleEpisodePage(episodes, (index + 1) * 10 < 159 ? (index + 1).ToString() : null));
        }
    }

    private readonly CountingService _service = new();

    [Fact(DisplayName =
        "INVARIANT: Two lookups of episodes from the same show in one scope read each show page once; the second " +
        "lookup's full read is served from the shared pages.")]
    public async Task Two_items_from_one_show_read_pages_once()
    {
        // Arrange
        var sut = new AppleShowEpisodePages(_service);

        // Act
        for (var lookup = 0; lookup < 2; lookup++)
        {
            for (var page = 0; page < 16; page++)
            {
                (await sut.GetNewestPage(Compendium, page)).Should().NotBeNull();
            }
        }

        // Assert
        _service.PageCalls.Should().Be(16);
    }
}
