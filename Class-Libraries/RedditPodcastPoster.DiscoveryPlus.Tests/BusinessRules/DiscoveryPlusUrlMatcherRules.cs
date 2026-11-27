using FluentAssertions;
using RedditPodcastPoster.DiscoveryPlus.Matching;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.DiscoveryPlus.Tests.BusinessRules;

public class DiscoveryPlusUrlMatcherRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "A discovery+ show slug URL is a submit URL, so submit can ingest a series catalogue page.")]
    public void show_slug_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.discoveryplus.com/show/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = DiscoveryPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }
    [Fact(DisplayName =
        "A locale-prefixed discovery+ show URL is a submit URL, because regional storefronts use the same catalogue path.")]
    public void locale_prefixed_show_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.discoveryplus.com/gb/show/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = DiscoveryPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A discovery+ movie UUID URL is a submit URL, so film catalogue pages can be dragged into submit.")]
    public void movie_uuid_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.discoveryplus.com/movie/{_fixture.CreateGuid()}");

        // Act
        var matches = DiscoveryPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A locale-prefixed discovery+ movie URL is a submit URL, because regional film pages use the same /movie/ path.")]
    public void locale_prefixed_movie_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.discoveryplus.com/gb/movie/{_fixture.CreateGuid()}");

        // Act
        var matches = DiscoveryPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A discovery+ video UUID URL is a submit URL, so episode watch pages can be ingested.")]
    public void video_uuid_is_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.discoveryplus.com/video/{_fixture.CreateGuid()}");

        // Act
        var matches = DiscoveryPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeTrue();
    }

    [Fact(DisplayName =
        "The discovery+ homepage is not a submit URL, because it is marketing rather than a catalogue title.")]
    public void home_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.discoveryplus.com/");

        // Act
        var matches = DiscoveryPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }
    [Fact(DisplayName =
        "A lookalike host that merely contains the letters discoveryplus.com is not a submit URL, because host matching is suffix-safe.")]
    public void lookalike_host_is_not_submit_url()
    {
        // Arrange
        var url = new Uri($"https://www.discoveryplus.com.example.test/show/{_fixture.CreateYouTubeId()}");

        // Act
        var matches = DiscoveryPlusUrlMatcher.IsSubmitUrl(url);

        // Assert
        matches.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A discovery+ /show/{uuid} path with no season/episode segments is a show hub, " +
        "so submit can reject it instead of minting a fake episode (#1587).")]
    public void show_uuid_alone_is_show_hub()
    {
        // Arrange
        var showId = _fixture.CreateGuid();
        var url = new Uri($"https://www.discoveryplus.com/show/{showId}");

        // Act
        var hub = DiscoveryPlusUrlMatcher.IsShowHub(url);
        var episode = DiscoveryPlusUrlMatcher.IsEpisodePath(url);
        var gotId = DiscoveryPlusUrlMatcher.TryGetShowId(url, out var parsed);

        // Assert
        hub.Should().BeTrue();
        episode.Should().BeFalse();
        gotId.Should().BeTrue();
        parsed.Should().Be(showId.ToString());
    }

    [Fact(DisplayName =
        "A discovery+ episode path under /show/{uuid}/sN/e… shares the show UUID with the hub, " +
        "so later episode submits can attach to the same TvShow (#1587).")]
    public void episode_path_shares_show_id_with_hub()
    {
        // Arrange
        var showId = _fixture.CreateGuid();
        var episodeId = _fixture.CreateGuid();
        var hub = new Uri($"https://www.discoveryplus.com/show/{showId}");
        var episode = new Uri(
            $"https://www.discoveryplus.com/show/{showId}/s1/e1-rise-of-top-g/{episodeId}");

        // Act
        DiscoveryPlusUrlMatcher.TryGetShowId(hub, out var hubId).Should().BeTrue();
        DiscoveryPlusUrlMatcher.TryGetShowId(episode, out var episodeShowId).Should().BeTrue();

        // Assert
        DiscoveryPlusUrlMatcher.IsShowHub(hub).Should().BeTrue();
        DiscoveryPlusUrlMatcher.IsEpisodePath(episode).Should().BeTrue();
        DiscoveryPlusUrlMatcher.IsShowHub(episode).Should().BeFalse();
        episodeShowId.Should().Be(hubId);
        DiscoveryPlusUrlMatcher.SameShow(episode, hubId).Should().BeTrue();
    }

    [Fact(DisplayName =
        "A discovery+ /show/{uuid}/… path without a season (sN) or episode (e…) segment is not an episode path, " +
        "so a non-watch deep link is treated as a hub rather than episode ingest.")]
    public void show_path_without_season_or_episode_segment_is_not_episode()
    {
        // Arrange
        var showId = _fixture.CreateGuid();
        var trailing = _fixture.CreateYouTubeId();
        var url = new Uri($"https://www.discoveryplus.com/show/{showId}/{trailing}");

        // Act
        var episode = DiscoveryPlusUrlMatcher.IsEpisodePath(url);
        var hub = DiscoveryPlusUrlMatcher.IsShowHub(url);

        // Assert
        episode.Should().BeFalse();
        hub.Should().BeTrue();
    }
}
