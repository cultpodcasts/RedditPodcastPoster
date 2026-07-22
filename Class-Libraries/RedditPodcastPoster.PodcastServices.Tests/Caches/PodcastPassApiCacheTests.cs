using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.PodcastServices.Abstractions.Caches;
using RedditPodcastPoster.PodcastServices.Updaters;

namespace RedditPodcastPoster.PodcastServices.Tests.Caches;

public class PodcastPassApiCacheTests
{
    [Fact(DisplayName = "Clear invokes ClearPassCache on every registered source.")]
    public void Clear_invokes_ClearPassCache_on_all_sources()
    {
        var source1 = new Mock<IPodcastPassApiCacheSource>();
        var source2 = new Mock<IPodcastPassApiCacheSource>();
        var cache = new PodcastPassApiCache(
            [source1.Object, source2.Object],
            NullLogger<PodcastPassApiCache>.Instance);

        cache.Clear();

        source1.Verify(x => x.ClearPassCache(), Times.Once);
        source2.Verify(x => x.ClearPassCache(), Times.Once);
    }

    [Fact(DisplayName = "Clear with no sources does not throw.")]
    public void Clear_with_no_sources_does_not_throw()
    {
        var cache = new PodcastPassApiCache(
            [],
            NullLogger<PodcastPassApiCache>.Instance);

        var act = () => cache.Clear();

        act.Should().NotThrow();
    }
}
