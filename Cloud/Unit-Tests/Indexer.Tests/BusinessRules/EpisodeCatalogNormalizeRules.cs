using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;

namespace Indexer.Tests.BusinessRules;

public class EpisodeCatalogNormalizeRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When Upsert writes a catalog service, NormalizeCatalog drops a retired other key and empty ids so those do not persist on the write path.")]
    public void upsert_normalizes_retired_other_and_empty_ids()
    {
        // Arrange
        var leftoverArt = new Uri($"https://cdn.example.test/{_fixture.Create<string>()}.jpg");
        var catalogUrl = _fixture.DefaultSpotifyUrl(_fixture.CreateSpotifyId());
        var episode = _fixture.CreateEpisode(e =>
        {
            e.Ids = new EpisodeIds();
            e.Services = new Dictionary<string, EpisodeServiceLink>
            {
                ["other"] = new() { Image = leftoverArt }
            };
        });

        // Act
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Spotify, catalogUrl, null);

        // Assert
        episode.Services.Should().NotBeNull();
        episode.Services.Should().NotContainKey("other");
        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify).Should().Be(catalogUrl);
        episode.Ids.Should().BeNull();
    }

    [Fact(DisplayName =
        "When the only catalog listen URL is Netflix, TryGetPreferredSocialPost picks that URL so a social post can log and share a Netflix destination.")]
    public void preferred_social_post_can_be_netflix()
    {
        // Arrange
        var netflixUrl = new Uri($"https://www.netflix.com/title/{Math.Abs(_fixture.Create<int>())}");
        var episode = _fixture.CreateEpisode(e =>
        {
            EpisodeServicePresence.Upsert(e, StreamingServiceWire.ToKey(StreamingService.Netflix), netflixUrl, null);
        });

        // Act
        var found = EpisodeServicePresence.TryGetPreferredSocialPost(
            episode, out var url, out var key, out var service);
        var catalogLog = EpisodeServicePresence.FormatCatalogUrlsForLog(episode);

        // Assert
        found.Should().BeTrue();
        url.Should().Be(netflixUrl);
        key.Should().Be(StreamingServiceWire.ToKey(StreamingService.Netflix));
        service.Should().Be(Service.Other);
        catalogLog.Should().Contain($"{StreamingServiceWire.ToKey(StreamingService.Netflix)}={netflixUrl}");
    }

    [Fact(DisplayName =
        "When an episode has both BBC iPlayer and BBC Sounds listen URLs, TryGetPreferredSocialPost with ImageCoalesceOrder picks the iPlayer URL, because iPlayer precedes Sounds in social-share order.")]
    public void preferred_social_post_prefers_iplayer_before_sounds()
    {
        // Arrange
        var soundsUrl = new Uri($"https://www.bbc.co.uk/sounds/play/{_fixture.CreateYouTubeId()}");
        var iplayerUrl = new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}");
        var episode = _fixture.CreateEpisode(e =>
        {
            EpisodeServicePresence.Upsert(e, StreamingServiceWire.ToKey(StreamingService.BbcSounds), soundsUrl, null);
            EpisodeServicePresence.Upsert(e, StreamingServiceWire.ToKey(StreamingService.BbcIplayer), iplayerUrl, null);
        });

        // Act
        var found = EpisodeServicePresence.TryGetPreferredSocialPost(
            episode,
            StreamingServiceCatalog.ImageCoalesceOrder,
            out var url,
            out var key,
            out var service);

        // Assert
        found.Should().BeTrue();
        url.Should().Be(iplayerUrl);
        key.Should().Be(StreamingServiceWire.ToKey(StreamingService.BbcIplayer));
        service.Should().Be(Service.Other);
    }

    [Fact(DisplayName =
        "When fixture Urls.BBC is an iPlayer episode URI, ApplyBbc stores StreamingServiceWire.ToKey(StreamingService.BbcIplayer), not Sounds, because the composed catalog resolves /iplayer/ before the Sounds fallback.")]
    public void fixture_bbc_iplayer_url_stores_bbc_iplayer_key()
    {
        // Arrange
        var iplayerUrl = new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}");
        var episode = _fixture.CreateEpisode();

        // Act
        episode.Urls.BBC = iplayerUrl;

        // Assert
        EpisodeServicePresence.TryGetUrl(episode, StreamingServiceWire.ToKey(StreamingService.BbcIplayer)).Should().Be(iplayerUrl);
        EpisodeServicePresence.HasUrl(episode, StreamingServiceWire.ToKey(StreamingService.BbcSounds)).Should().BeFalse();
    }
}
