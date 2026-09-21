using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.UrlSubmission;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules;

public class SubmitUrlStreamingUrlResolverRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When an episode has a streaming services URL, TryGetUrl returns that URL in search-encode key order " +
        "so SubmitUrl --episode-id can refresh from the stored catalogue link.")]
    public void returns_streaming_service_url_in_search_encode_order()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var firstKey = StreamingServiceWire.AllKeys[0];
        var laterKey = StreamingServiceWire.AllKeys[^1];
        var firstUrl = new Uri($"https://example.test/{_fixture.CreateYouTubeId()}");
        var laterUrl = new Uri($"https://example.test/{_fixture.CreateYouTubeId()}-later");
        var episode = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(episode, laterKey, laterUrl, null);
        EpisodeServicePresence.Upsert(episode, firstKey, firstUrl, null);
        EpisodeServicePresence.Upsert(
            episode,
            ServiceKeys.Spotify,
            new Uri($"https://open.spotify.com/episode/{_fixture.CreateSpotifyId()}"),
            null);

        // Act
        var found = SubmitUrlStreamingUrlResolver.TryGetUrl(episode, out var url);

        // Assert
        found.Should().BeTrue();
        url.Should().Be(firstUrl);
    }

    [Fact(DisplayName =
        "When an episode only has Spotify, Apple, or YouTube service URLs, TryGetUrl returns false " +
        "because those platforms are not streaming scrape targets for episode-id refresh.")]
    public void ignores_podcast_platform_service_urls()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            episode,
            ServiceKeys.Spotify,
            new Uri($"https://open.spotify.com/episode/{_fixture.CreateSpotifyId()}"),
            null);
        EpisodeServicePresence.Upsert(
            episode,
            ServiceKeys.YouTube,
            new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}"),
            null);
        EpisodeServicePresence.Upsert(
            episode,
            ServiceKeys.Apple,
            new Uri($"https://podcasts.apple.com/podcast/id{_fixture.CreateAppleId()}"),
            null);

        // Act
        var found = SubmitUrlStreamingUrlResolver.TryGetUrl(episode, out _);

        // Assert
        found.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When an episode has no services map or only empty streaming links, TryGetUrl returns false " +
        "because there is no catalogue URL to re-extract.")]
    public void returns_false_when_services_missing_or_empty()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var withoutServices = _fixture.CreateStoredEpisode(podcast, e => e.Services = null);
        var emptyStreaming = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(emptyStreaming, StreamingServiceWire.AllKeys[0], null, null);

        // Act
        var withoutServicesFound = SubmitUrlStreamingUrlResolver.TryGetUrl(withoutServices, out _);
        var emptyStreamingFound = SubmitUrlStreamingUrlResolver.TryGetUrl(emptyStreaming, out _);

        // Assert
        withoutServicesFound.Should().BeFalse();
        emptyStreamingFound.Should().BeFalse();
    }
}
