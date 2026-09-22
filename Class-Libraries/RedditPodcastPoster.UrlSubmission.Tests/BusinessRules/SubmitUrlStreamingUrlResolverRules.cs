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
        "When an episode has a submit-eligible streaming services URL, TryGetUrl returns that URL in " +
        "submit-eligible key order so SubmitUrl --episode-id can refresh from the stored catalogue link.")]
    public void returns_streaming_service_url_in_search_encode_order()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var firstKey = StreamingServiceWire.SubmitEligibleKeys[0];
        var laterKey = StreamingServiceWire.SubmitEligibleKeys[^1];
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
        "When an episode has both a submit-retired Hulu URL and a live Peacock URL, TryGetUrl prefers Peacock " +
        "so SubmitUrl --episode-id does not resolve a scrape-retired catalogue link.")]
    public void prefers_submit_eligible_over_retired_hulu()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        var huluUrl = new Uri($"https://www.hulu.com/watch/{_fixture.CreateYouTubeId()}");
        var peacockUrl = new Uri(
            $"https://www.peacocktv.com/watch-online/tv/{_fixture.CreateYouTubeId()}/{_fixture.CreateAppleId()}{_fixture.CreateAppleId()}");
        EpisodeServicePresence.Upsert(
            episode,
            StreamingServiceWire.ToKey(StreamingService.Hulu),
            huluUrl,
            null);
        EpisodeServicePresence.Upsert(
            episode,
            StreamingServiceWire.ToKey(StreamingService.Peacock),
            peacockUrl,
            null);

        // Act
        var found = SubmitUrlStreamingUrlResolver.TryGetUrl(episode, out var url);

        // Assert
        found.Should().BeTrue();
        url.Should().Be(peacockUrl);
    }

    [Fact(DisplayName =
        "When an episode only has a submit-retired Hulu URL, TryGetUrl returns false " +
        "because retired keys are not scrape targets after package retire.")]
    public void ignores_hulu_only_episode()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast);
        EpisodeServicePresence.Upsert(
            episode,
            StreamingServiceWire.ToKey(StreamingService.Hulu),
            new Uri($"https://www.hulu.com/watch/{_fixture.CreateYouTubeId()}"),
            null);

        // Act
        var found = SubmitUrlStreamingUrlResolver.TryGetUrl(episode, out _);

        // Assert
        found.Should().BeFalse();
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
        EpisodeServicePresence.Upsert(emptyStreaming, StreamingServiceWire.SubmitEligibleKeys[0], null, null);

        // Act
        var withoutServicesFound = SubmitUrlStreamingUrlResolver.TryGetUrl(withoutServices, out _);
        var emptyStreamingFound = SubmitUrlStreamingUrlResolver.TryGetUrl(emptyStreaming, out _);

        // Assert
        withoutServicesFound.Should().BeFalse();
        emptyStreamingFound.Should().BeFalse();
    }
}
