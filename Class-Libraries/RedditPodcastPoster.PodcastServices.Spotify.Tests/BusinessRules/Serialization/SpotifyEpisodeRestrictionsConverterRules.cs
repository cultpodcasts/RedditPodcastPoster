using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.Spotify.Serialization;
using SpotifyAPI.Web;

namespace RedditPodcastPoster.PodcastServices.Spotify.Tests.BusinessRules.Serialization;

public class SpotifyEpisodeRestrictionsConverterRules
{
    private readonly JsonSerializerSettings _settings = new()
    {
        NullValueHandling = NullValueHandling.Ignore,
        ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy()
        },
        Converters = { new SpotifyEpisodeRestrictionsConverter() }
    };

    [Fact(DisplayName =
        "When SimpleEpisode JSON includes restrictions.reason, deserialize yields SimpleEpisodeWithRestrictions " +
        "because SpotifyAPI.Web 7.4.2 does not model episode Restrictions.")]
    public void Simple_episode_json_with_restrictions_deserializes_subclass()
    {
        // Arrange
        const string json = """
            {
              "id": "abc123",
              "name": "Paywalled",
              "is_playable": false,
              "restrictions": { "reason": "payment_required" },
              "type": "episode"
            }
            """;

        // Act
        var episode = JsonConvert.DeserializeObject<SimpleEpisode>(json, _settings);

        // Assert
        episode.Should().BeOfType<SimpleEpisodeWithRestrictions>();
        var withRestrictions = (SimpleEpisodeWithRestrictions)episode!;
        withRestrictions.IsPlayable.Should().BeFalse();
        withRestrictions.Restrictions.Should().ContainKey("reason")
            .WhoseValue.Should().Be("payment_required");
    }

    [Fact(DisplayName =
        "When FullEpisode JSON includes restrictions.reason, deserialize yields FullEpisodeWithRestrictions " +
        "because hydrate paths also need the reason for non-playable skip logs.")]
    public void Full_episode_json_with_restrictions_deserializes_subclass()
    {
        // Arrange
        const string json = """
            {
              "id": "xyz789",
              "name": "Paywalled Full",
              "is_playable": false,
              "restrictions": { "reason": "payment_required" },
              "type": "episode"
            }
            """;

        // Act
        var episode = JsonConvert.DeserializeObject<FullEpisode>(json, _settings);

        // Assert
        episode.Should().BeOfType<FullEpisodeWithRestrictions>();
        var withRestrictions = (FullEpisodeWithRestrictions)episode!;
        withRestrictions.Restrictions.Should().ContainKey("reason")
            .WhoseValue.Should().Be("payment_required");
    }
}
