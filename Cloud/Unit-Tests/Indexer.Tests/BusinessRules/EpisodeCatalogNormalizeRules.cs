using System.Text.Json;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Serialization;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class EpisodeCatalogNormalizeRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When Cosmos System.Text.Json deserializes or serializes an episode, OnDeserialized and OnSerializing run NormalizeCatalog so retired other keys, empty ids, and empty services maps do not persist, because leftover hydrate is gone and write-path Upsert is not the only load/save path.")]
    public void deserialize_and_serialize_hooks_still_normalize_catalog()
    {
        // Arrange
        var leftoverArt = new Uri($"https://cdn.example.test/{_fixture.Create<string>()}.jpg");
        var json =
            $$"""
            {
              "title": "Untitled",
              "ids": {},
              "services": { "other": { "image": "{{leftoverArt}}" } }
            }
            """;

        // Act
        var episode = JsonSerializer.Deserialize<Episode>(json, EpisodeDocumentJsonOptions.Instance)!;
        var written = JsonSerializer.Serialize(episode, EpisodeDocumentJsonOptions.Instance);

        // Assert
        episode.Services.Should().BeNull();
        episode.Ids.Should().BeNull();
        written.Should().NotContain("\"other\"");
        written.Should().NotContain("\"ids\"");
        written.Should().NotContain("\"services\"");
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
