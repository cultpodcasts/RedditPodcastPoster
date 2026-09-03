using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Tests.Support;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class StreamingScraperBrowsePageHarvestRules
{
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private readonly InMemoryPodcastRepository _podcasts = new();

    public StreamingScraperBrowsePageHarvestRules()
    {
        _mocker.Use<IEpisodeRepository>(_episodes);
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<INonPodcastServiceAdapterResolver>(LiveStreamingScraperAdapterResolverSupport.Create());
    }

    [LiveStreamingTheory(DisplayName =
        "When a streaming homepage or section page is scraped live, it yields at least the expected number of " +
        "submit-eligible deep links, and sampled lookups stay streaming-shaped without writing episodes.")]
    [MemberData(nameof(BrowsePages))]
    public async Task browse_page_harvests_submit_urls_and_sample_lookups_succeed(
        StreamingScraperBrowsePage browse)
    {
        // Arrange
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var harvested = await StreamingScraperBrowseLinkHarvester.HarvestSubmitUrlsAsync(
            browse,
            CancellationToken.None);

        // Assert — harvest floor
        harvested.Count.Should().BeGreaterThanOrEqualTo(
            browse.MinSubmitLinks,
            because: $"browse page {browse.CaseId} ({browse.BrowseUrl}) should expose submit-eligible cards; got {harvested.Count}");

        foreach (var url in harvested.Take(browse.SampleLookups))
        {
            StreamingScraperBrowseLinkHarvester.IsSubmitUrl(browse.Provider, url).Should().BeTrue();

            var result = await sut.Lookup(url, CancellationToken.None);

            result.Known.Should().BeFalse($"harvested {url} should not match stored membership");
            result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
            result.PodcastId.Should().BeNull();
            result.PodcastName.Should().NotBe("Netflix");
            result.PodcastName.Should().NotBe("Amazon Prime Video");
            result.PodcastName.Should().NotBe("BBC");
            _episodes.SavedEpisodes.Should().BeEmpty();
        }
    }

    public static TheoryData<StreamingScraperBrowsePage> BrowsePages() =>
        StreamingScraperBrowsePages.All();
}
