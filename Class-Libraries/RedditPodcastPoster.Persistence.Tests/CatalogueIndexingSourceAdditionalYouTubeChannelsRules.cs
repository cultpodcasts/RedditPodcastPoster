using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Providers;

namespace RedditPodcastPoster.Persistence.Tests;

/// <summary>
/// #1370: a news organisation's <c>indexing.additionalYouTubeChannelIds</c> holds its YouTube channels beside the main
/// <c>youTubeChannelId</c>, each owned as a whole channel.
/// </summary>
public class CatalogueIndexingSourceAdditionalYouTubeChannelsRules
{
    private readonly DomainTestFixture _fixture = new();

    private static JsonSerializerOptions CosmosOptions => new JsonSerializerOptionsProvider().GetJsonSerializerOptions();

    [Fact(DisplayName =
        "Additional YouTube channel ids are stored trimmed, without blanks or repeats, because a channel is held once.")]
    public void additional_channels_are_normalised()
    {
        // Arrange
        var first = _fixture.CreateYouTubeChannelId();
        var second = _fixture.CreateYouTubeChannelId();
        var source = new CatalogueIndexingSource();

        // Act
        source.AdditionalYouTubeChannelIds = [$" {first} ", "", "  ", second, first];

        // Assert
        source.AdditionalYouTubeChannelIds.Should().Equal(first, second);
    }

    [Fact(DisplayName =
        "An empty additional channel list is null and is not written, because a parent with no additional channel stores nothing for it.")]
    public void empty_additional_channels_are_not_stored()
    {
        // Arrange
        var source = new CatalogueIndexingSource
        {
            YouTubeChannelId = _fixture.CreateYouTubeChannelId(),
            AdditionalYouTubeChannelIds = ["   "]
        };

        // Act
        var stored = (JsonObject) JsonSerializer.SerializeToNode(source, CosmosOptions)!;

        // Assert
        source.AdditionalYouTubeChannelIds.Should().BeNull();
        stored.Select(pair => pair.Key).Should().NotContain("additionalYouTubeChannelIds");
    }

    [Fact(DisplayName =
        "Additional YouTube channel ids round-trip through the Cosmos serializer as indexing.additionalYouTubeChannelIds, because the indexer, URL submission and discovery read them from the stored news organisation.")]
    public void additional_channels_round_trip()
    {
        // Arrange
        var archive = _fixture.CreateYouTubeChannelId();
        var organisation = new NewsOrganisation(_fixture.CreateTitle())
        {
            Indexing = new CatalogueIndexingSource
            {
                YouTubeChannelId = _fixture.CreateYouTubeChannelId(),
                AdditionalYouTubeChannelIds = [archive]
            }
        };

        // Act
        var json = JsonSerializer.Serialize(organisation, CosmosOptions);
        var read = JsonSerializer.Deserialize<NewsOrganisation>(json, CosmosOptions)!;

        // Assert
        JsonNode.Parse(json)!["indexing"]!["additionalYouTubeChannelIds"]!.AsArray()
            .Select(x => x!.GetValue<string>()).Should().Equal(archive);
        read.Indexing!.AdditionalYouTubeChannelIds.Should().Equal(archive);
    }

    [Fact(DisplayName =
        "A parent holds its main channel and each additional channel, an additional channel is claimed whole even when the main channel is playlist-scoped, and the main channel keeps its playlist, because only the main channel can be playlist-scoped.")]
    public void additional_channels_are_held_channel_wide()
    {
        // Arrange
        var main = _fixture.CreateYouTubeChannelId();
        var archive = _fixture.CreateYouTubeChannelId();
        var playlist = _fixture.CreateYouTubePlaylistId();
        var source = new CatalogueIndexingSource
        {
            YouTubeChannelId = main,
            YouTubePlaylistId = playlist,
            AdditionalYouTubeChannelIds = [archive]
        };

        // Act
        var all = source.AllYouTubeChannelIds();

        // Assert
        all.Should().Equal(main, archive);
        source.HoldsYouTubeChannel(archive).Should().BeTrue();
        source.HoldsYouTubeChannel(_fixture.CreateYouTubeChannelId()).Should().BeFalse();
        source.YouTubePlaylistIdOn(main).Should().Be(playlist);
        source.YouTubePlaylistIdOn(archive).Should().BeNull();
        source.ClaimsYouTubeChannelWide(main).Should().BeFalse();
        source.ClaimsYouTubeChannelWide(archive).Should().BeTrue();
    }

    [Fact(DisplayName =
        "The transient podcast for an additional channel indexes that whole channel only, with no Spotify or Apple id and none of the main channel's playlist or indexer state, because the extra pass must neither re-run the audio discovery nor move the main channel's bookmarks.")]
    public void additional_channel_transient_is_youtube_channel_only()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
            p.YouTubePlaylistQueryIsExpensive = true;
            p.YouTubeChannelSearchForbidden = true;
            p.SpotifyId = _fixture.CreateSpotifyId();
            p.AppleId = _fixture.CreateAppleId();
            p.IndexAllEpisodes = true;
        });
        var source = CatalogueIndexingSource.FromPodcast(podcast);
        var parent = CatalogueParentKindMapper.ToNewsOrganisation(podcast);
        var archive = _fixture.CreateYouTubeChannelId();

        // Act
        var transient = source.ToTransientPodcastForAdditionalYouTubeChannel(parent, archive);

        // Assert
        transient.Id.Should().Be(parent.Id);
        transient.YouTubeChannelId.Should().Be(archive);
        transient.YouTubePlaylistId.Should().BeEmpty();
        transient.YouTubePlaylistQueryIsExpensive.Should().BeNull();
        transient.YouTubeChannelSearchForbidden.Should().BeNull();
        transient.YouTubeIndexingHints.Should().BeNull();
        transient.SpotifyId.Should().BeEmpty();
        transient.AppleId.Should().BeNull();
        transient.IndexAllEpisodes.Should().BeTrue();
        source.YouTubeChannelId.Should().Be(podcast.YouTubeChannelId);
    }
}
