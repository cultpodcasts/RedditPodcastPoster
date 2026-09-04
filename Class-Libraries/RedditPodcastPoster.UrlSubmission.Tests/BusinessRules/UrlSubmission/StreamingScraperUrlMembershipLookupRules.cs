using FluentAssertions;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Tests.Support;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class StreamingScraperUrlMembershipLookupRules
{
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private readonly InMemoryPodcastRepository _podcasts = new();

    public StreamingScraperUrlMembershipLookupRules()
    {
        _mocker.Use<IEpisodeRepository>(_episodes);
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<INonPodcastServiceAdapterResolver>(LiveStreamingScraperAdapterResolverSupport.Create());
    }

    [LiveStreamingTheory(DisplayName =
        "When an unknown BBC Sounds canonical URL is classified live, URL membership lookup returns streaming with null podcastName " +
        "because membership does not scrape; prepare owns HTML fetch.")]
    [MemberData(nameof(BbcSoundsCanonicalCases))]
    public async Task bbc_sounds_live_lookup_returns_service_without_podcast_name(StreamingScraperCanonicalCase canonical)
    {
        // Arrange
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(canonical.Url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse($"case {canonical.CaseId} should not match stored membership");
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.PodcastName.Should().BeNull();
        result.PodcastId.Should().BeNull();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [LiveStreamingTheory(DisplayName =
        "When an unknown BBC iPlayer canonical URL is classified live, URL membership lookup returns streaming with null podcastName " +
        "because membership does not scrape; prepare owns HTML fetch.")]
    [MemberData(nameof(BbcIplayerCanonicalCases))]
    public async Task bbc_iplayer_live_lookup_returns_service_without_podcast_name(StreamingScraperCanonicalCase canonical)
    {
        // Arrange
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(canonical.Url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse($"case {canonical.CaseId} should not match stored membership");
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.PodcastName.Should().BeNull();
        result.PodcastId.Should().BeNull();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [LiveStreamingTheory(DisplayName =
        "When an unknown Netflix canonical URL is classified live, URL membership lookup returns streaming with null podcastName " +
        "because membership does not scrape; prepare owns HTML fetch.")]
    [MemberData(nameof(NetflixCanonicalCases))]
    public async Task netflix_live_lookup_returns_service_without_podcast_name(StreamingScraperCanonicalCase canonical)
    {
        // Arrange
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(canonical.Url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse($"case {canonical.CaseId} should not match stored membership");
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.PodcastName.Should().BeNull();
        result.PodcastId.Should().BeNull();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [LiveStreamingTheory(DisplayName =
        "When an unknown Prime Video canonical URL is classified live, URL membership lookup returns streaming with null podcastName " +
        "because membership does not scrape; prepare owns HTML fetch.")]
    [MemberData(nameof(AmazonPrimeCanonicalCases))]
    public async Task amazon_prime_live_lookup_returns_service_without_podcast_name(StreamingScraperCanonicalCase canonical)
    {
        // Arrange
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(canonical.Url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse($"case {canonical.CaseId} should not match stored membership");
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.PodcastName.Should().BeNull();
        result.PodcastId.Should().BeNull();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [LiveStreamingTheory(DisplayName =
        "When an unknown Vimeo canonical URL is classified live, URL membership lookup returns streaming with null podcastName " +
        "because membership does not scrape; prepare owns HTML fetch.")]
    [MemberData(nameof(VimeoCanonicalCases))]
    public async Task vimeo_live_lookup_returns_service_without_podcast_name(StreamingScraperCanonicalCase canonical)
    {
        // Arrange
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(canonical.Url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse($"case {canonical.CaseId} should not match stored membership");
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.PodcastName.Should().BeNull();
        result.PodcastId.Should().BeNull();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [LiveStreamingTheory(DisplayName =
        "When an unknown next-wave streaming canonical URL is classified live, URL membership lookup returns streaming with null podcastName " +
        "because membership does not scrape; prepare owns HTML fetch.")]
    [MemberData(nameof(NextWaveCanonicalCases))]
    public async Task next_wave_live_lookup_returns_service_without_podcast_name(StreamingScraperCanonicalCase canonical)
    {
        // Arrange
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(canonical.Url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse($"case {canonical.CaseId} should not match stored membership");
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.PodcastName.Should().BeNull();
        result.PodcastId.Should().BeNull();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    public static TheoryData<StreamingScraperCanonicalCase> BbcSoundsCanonicalCases() =>
        StreamingScraperCanonicalCases.BbcSoundsCases();

    public static TheoryData<StreamingScraperCanonicalCase> BbcIplayerCanonicalCases() =>
        StreamingScraperCanonicalCases.BbcIplayerCases();

    public static TheoryData<StreamingScraperCanonicalCase> NetflixCanonicalCases() =>
        StreamingScraperCanonicalCases.NetflixCases();

    public static TheoryData<StreamingScraperCanonicalCase> AmazonPrimeCanonicalCases() =>
        StreamingScraperCanonicalCases.AmazonPrimeCases();

    public static TheoryData<StreamingScraperCanonicalCase> VimeoCanonicalCases() =>
        StreamingScraperCanonicalCases.VimeoCases();

    public static TheoryData<StreamingScraperCanonicalCase> NextWaveCanonicalCases()
    {
        var data = new TheoryData<StreamingScraperCanonicalCase>();
        foreach (var canonical in StreamingScraperCanonicalCases.All.Where(c =>
                     c.Provider is StreamingScraperProvider.Itvx
                         or StreamingScraperProvider.Channel4
                         or StreamingScraperProvider.Fawesome
                         or StreamingScraperProvider.ParamountPlus
                         or StreamingScraperProvider.HboMax
                         or StreamingScraperProvider.PlaySuisse
                         or StreamingScraperProvider.TvnzPlus
                         or StreamingScraperProvider.DisneyPlus
                         or StreamingScraperProvider.DiscoveryPlus))
        {
            data.Add(canonical);
        }

        return data;
    }
}
