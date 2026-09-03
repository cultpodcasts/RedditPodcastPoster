using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;

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
            EpisodeServicePresence.Upsert(e, ServiceKeys.Netflix, netflixUrl, null);
        });

        // Act
        var found = EpisodeServicePresence.TryGetPreferredSocialPost(
            episode, out var url, out var key, out var service);
        var catalogLog = EpisodeServicePresence.FormatCatalogUrlsForLog(episode);

        // Assert
        found.Should().BeTrue();
        url.Should().Be(netflixUrl);
        key.Should().Be(ServiceKeys.Netflix);
        service.Should().Be(Service.Other);
        catalogLog.Should().Contain($"{ServiceKeys.Netflix}={netflixUrl}");
    }
}
