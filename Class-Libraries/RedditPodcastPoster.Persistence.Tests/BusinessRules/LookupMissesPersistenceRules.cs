using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Providers;

namespace RedditPodcastPoster.Persistence.Tests.BusinessRules;

/// <summary>
/// <c>lookupMisses</c> is omitted when empty/null so historic podcast documents read and write unchanged (#1558).
/// </summary>
public class LookupMissesPersistenceRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly JsonSerializerOptions _options = new JsonSerializerOptionsProvider().GetJsonSerializerOptions();

    [Fact(DisplayName =
        "A podcast with no misses writes no lookupMisses property; a document without the property deserialises.")]
    public void empty_misses_are_omitted_and_absent_deserialises()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.LookupMisses = null;

        // Act
        var json = JsonSerializer.SerializeToNode(podcast, _options)!.AsObject();
        var id = _fixture.CreateGuid();
        var withoutProperty =
            $"{{\"id\":\"{id}\",\"name\":\"specimen\",\"fileKey\":\"specimen\",\"type\":\"podcast\"}}";
        var restored = JsonSerializer.Deserialize<Podcast>(withoutProperty, _options);

        // Assert
        json.ContainsKey("lookupMisses").Should().BeFalse();
        restored.Should().NotBeNull();
        restored!.LookupMisses.Should().BeNull();
    }

    [Fact(DisplayName = "Lookup misses round-trip on a podcast document.")]
    public void misses_round_trip()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var miss = new EpisodeLookupMiss
        {
            EpisodeId = _fixture.CreateGuid(),
            Platform = "Spotify",
            NewestCatalogueId = _fixture.CreateSpotifyId(),
            ShowTotal = 42,
            LastFullAttemptUtc = DomainTestFixture.UtcDaysAgo(0)
        };
        podcast.LookupMisses = [miss];

        // Act
        var json = JsonSerializer.Serialize(podcast, _options);
        var back = JsonSerializer.Deserialize<Podcast>(json, _options)!;

        // Assert
        JsonNode.Parse(json)!.AsObject().ContainsKey("lookupMisses").Should().BeTrue();
        back.LookupMisses.Should().ContainSingle(m =>
            m.EpisodeId == miss.EpisodeId &&
            m.Platform == miss.Platform &&
            m.NewestCatalogueId == miss.NewestCatalogueId &&
            m.ShowTotal == miss.ShowTotal &&
            m.LastFullAttemptUtc == miss.LastFullAttemptUtc);
    }
}
