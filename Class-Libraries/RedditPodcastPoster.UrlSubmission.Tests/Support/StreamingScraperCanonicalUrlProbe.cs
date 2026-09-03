using FluentAssertions;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Tests.Support;

namespace RedditPodcastPoster.UrlSubmission.Tests.Support;

/// <summary>
/// Developer aid: run with <c>dotnet test --filter ProbeCanonicalStreamingUrls</c> to print live podcastName values.
/// </summary>
public sealed class StreamingScraperCanonicalUrlProbe
{
    [Fact(DisplayName = "Probe canonical streaming URLs and print live podcastName outcomes.")]
    public async Task ProbeCanonicalStreamingUrls()
    {
        // Arrange
        var resolver = LiveStreamingScraperAdapterResolverSupport.Create();

        // Act
        foreach (var canonical in StreamingScraperCanonicalCases.All)
        {
            var adapter = resolver.ForExtract(canonical.Url);
            adapter.Should().NotBeNull($"no adapter for {canonical.Url}");
            var meta = await adapter!.ExtractMetaData(canonical.Url);
            var podcastName = NonPodcastShowNameResolver.TrySeriesName(
                meta.ShowName,
                meta.Publisher,
                adapter.Service);
            Console.WriteLine(
                $"{canonical.Provider,-14} {canonical.CaseId,-28} expected={canonical.ExpectedPodcastName ?? "null",-35} actual={podcastName ?? "null",-35} title={meta.Title}");
        }

        // Assert
        StreamingScraperCanonicalCases.All.Should().NotBeEmpty();
    }
}
