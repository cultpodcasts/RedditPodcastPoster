using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Providers;

namespace RedditPodcastPoster.Persistence.Tests;

/// <summary>
/// #1370: a news organisation's <c>indexing.additionalYouTubeChannels</c> holds its YouTube channels beside the main
/// <c>youTubeChannelId</c>, each owned as a whole channel or through its own optional playlist.
/// </summary>
public class CatalogueIndexingSourceAdditionalYouTubeChannelsRules
{
    private readonly DomainTestFixture _fixture = new();

    private static JsonSerializerOptions CosmosOptions => new JsonSerializerOptionsProvider().GetJsonSerializerOptions();

    [Fact(DisplayName =
        "Additional YouTube channels are stored trimmed, without blank channel ids or repeated channels (the first entry wins), because a channel is held once.")]
    public void additional_channels_are_normalised()
    {
        // Arrange
        var first = _fixture.CreateYouTubeChannelId();
        var second = _fixture.CreateYouTubeChannelId();
        var playlist = _fixture.CreateYouTubePlaylistId();
        var source = new CatalogueIndexingSource();

        // Act
        source.AdditionalYouTubeChannels =
        [
            AdditionalYouTubeChannel.ForPlaylist($" {first} ", $" {playlist} "),
            AdditionalYouTubeChannel.ChannelWide(""),
            AdditionalYouTubeChannel.ChannelWide("  "),
            AdditionalYouTubeChannel.ForPlaylist(second, "  "),
            AdditionalYouTubeChannel.ChannelWide(first)
        ];

        // Assert
        source.AdditionalYouTubeChannelIdList().Should().Equal(first, second);
        source.AdditionalYouTubeChannels![0].YouTubePlaylistId.Should().Be(playlist);
        source.AdditionalYouTubeChannels[1].YouTubePlaylistId.Should().BeNull();
    }

    [Fact(DisplayName =
        "An empty additional channel list is null and is not written, because a parent with no additional channel stores nothing for it.")]
    public void empty_additional_channels_are_not_stored()
    {
        // Arrange
        var source = new CatalogueIndexingSource
        {
            YouTubeChannelId = _fixture.CreateYouTubeChannelId(),
            AdditionalYouTubeChannels = [AdditionalYouTubeChannel.ChannelWide("   ")]
        };

        // Act
        var stored = (JsonObject) JsonSerializer.SerializeToNode(source, CosmosOptions)!;

        // Assert
        source.AdditionalYouTubeChannels.Should().BeNull();
        stored.Select(pair => pair.Key).Should().NotContain("additionalYouTubeChannels");
    }

    [Fact(DisplayName =
        "Additional YouTube channels round-trip through the Cosmos serializer as indexing.additionalYouTubeChannels with each channel's playlist and indexer flags, and a whole channel writes only its id, because the indexer, URL submission and discovery read them from the stored news organisation.")]
    public void additional_channels_round_trip()
    {
        // Arrange
        var archive = _fixture.CreateYouTubeChannelId();
        var clips = _fixture.CreateYouTubeChannelId();
        var clipsPlaylist = _fixture.CreateYouTubePlaylistId();
        var scoped = AdditionalYouTubeChannel.ForPlaylist(clips, clipsPlaylist);
        scoped.YouTubePlaylistQueryIsExpensive = true;
        scoped.YouTubeChannelSearchForbidden = true;
        var organisation = new NewsOrganisation(_fixture.CreateTitle())
        {
            Indexing = new CatalogueIndexingSource
            {
                YouTubeChannelId = _fixture.CreateYouTubeChannelId(),
                AdditionalYouTubeChannels = [AdditionalYouTubeChannel.ChannelWide(archive), scoped]
            }
        };

        // Act
        var json = JsonSerializer.Serialize(organisation, CosmosOptions);
        var read = JsonSerializer.Deserialize<NewsOrganisation>(json, CosmosOptions)!;

        // Assert
        var stored = JsonNode.Parse(json)!["indexing"]!["additionalYouTubeChannels"]!.AsArray();
        stored[0]!.AsObject().Select(pair => pair.Key).Should().Equal("youTubeChannelId");
        stored[0]!["youTubeChannelId"]!.GetValue<string>().Should().Be(archive);
        stored[1]!["youTubePlaylistId"]!.GetValue<string>().Should().Be(clipsPlaylist);
        stored[1]!["youTubePlaylistQueryIsExpensive"]!.GetValue<bool>().Should().BeTrue();
        stored[1]!["youTubeChannelSearchForbidden"]!.GetValue<bool>().Should().BeTrue();
        read.Indexing!.AdditionalYouTubeChannelIdList().Should().Equal(archive, clips);
        read.Indexing.AdditionalYouTubeChannelFor(clips)!.YouTubePlaylistId.Should().Be(clipsPlaylist);
        read.Indexing.AdditionalYouTubeChannelFor(clips)!.YouTubePlaylistQueryIsExpensive.Should().BeTrue();
        read.Indexing.AdditionalYouTubeChannelFor(clips)!.YouTubeChannelSearchForbidden.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A parent holds its main channel and each additional channel; each channel is scoped only by its own playlist (the main playlist never scopes an additional channel, and an additional channel's playlist never scopes the main one), because one organisation may claim one channel whole and only a playlist of another.")]
    public void each_channel_is_scoped_by_its_own_playlist()
    {
        // Arrange
        var main = _fixture.CreateYouTubeChannelId();
        var archive = _fixture.CreateYouTubeChannelId();
        var clips = _fixture.CreateYouTubeChannelId();
        var mainPlaylist = _fixture.CreateYouTubePlaylistId();
        var clipsPlaylist = _fixture.CreateYouTubePlaylistId();
        var source = new CatalogueIndexingSource
        {
            YouTubeChannelId = main,
            YouTubePlaylistId = mainPlaylist,
            AdditionalYouTubeChannels =
            [
                AdditionalYouTubeChannel.ChannelWide(archive),
                AdditionalYouTubeChannel.ForPlaylist(clips, clipsPlaylist)
            ]
        };

        // Act
        var all = source.AllYouTubeChannelIds();

        // Assert
        all.Should().Equal(main, archive, clips);
        source.HoldsYouTubeChannel(archive).Should().BeTrue();
        source.HoldsYouTubeChannel(clips).Should().BeTrue();
        source.HoldsYouTubeChannel(_fixture.CreateYouTubeChannelId()).Should().BeFalse();
        source.YouTubePlaylistIdOn(main).Should().Be(mainPlaylist);
        source.YouTubePlaylistIdOn(archive).Should().BeNull();
        source.YouTubePlaylistIdOn(clips).Should().Be(clipsPlaylist);
        source.YouTubePlaylistIdOn(_fixture.CreateYouTubeChannelId()).Should().BeNull();
        source.ClaimsYouTubeChannelWide(main).Should().BeFalse();
        source.ClaimsYouTubeChannelWide(archive).Should().BeTrue();
        source.ClaimsYouTubeChannelWide(clips).Should().BeFalse();
    }

    [Fact(DisplayName =
        "The transient podcast for a whole additional channel indexes that channel only, with no Spotify or Apple id and none of the main channel's playlist or indexer state, because the extra pass must neither re-run the audio discovery nor move the main channel's bookmarks.")]
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
        var archive = _fixture.CreateYouTubeChannelId();
        source.AdditionalYouTubeChannels = [AdditionalYouTubeChannel.ChannelWide(archive)];
        var parent = CatalogueParentKindMapper.ToNewsOrganisation(podcast);

        // Act
        var transient = source.ToTransientPodcastForAdditionalYouTubeChannel(parent, archive);

        // Assert
        transient.Id.Should().Be(parent.Id);
        transient.YouTubeChannelId.Should().Be(archive);
        transient.YouTubePlaylistId.Should().BeEmpty();
        transient.YouTubePlaylistOwnerChannelId.Should().BeNull();
        transient.YouTubePlaylistQueryIsExpensive.Should().BeNull();
        transient.YouTubeChannelSearchForbidden.Should().BeNull();
        transient.YouTubeIndexingHints.Should().BeNull();
        transient.SpotifyId.Should().BeEmpty();
        transient.AppleId.Should().BeNull();
        transient.IndexAllEpisodes.Should().BeTrue();
        source.YouTubeChannelId.Should().Be(podcast.YouTubeChannelId);
    }

    [Fact(DisplayName =
        "The transient podcast for a playlist-scoped additional channel reads that channel's own playlist (owned by that channel) with that channel's own expensive-query and search-forbidden flags, never the main channel's, because each channel's YouTube cost is measured and remembered separately.")]
    public void additional_channel_transient_carries_its_own_playlist_and_flags()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
            p.YouTubePlaylistId = _fixture.CreateYouTubePlaylistId();
            p.YouTubePlaylistQueryIsExpensive = false;
            p.YouTubeChannelSearchForbidden = false;
            p.IndexAllEpisodes = true;
        });
        var source = CatalogueIndexingSource.FromPodcast(podcast);
        var clips = _fixture.CreateYouTubeChannelId();
        var clipsPlaylist = _fixture.CreateYouTubePlaylistId();
        var channel = AdditionalYouTubeChannel.ForPlaylist(clips, clipsPlaylist);
        channel.YouTubePlaylistQueryIsExpensive = true;
        channel.YouTubeChannelSearchForbidden = true;
        source.AdditionalYouTubeChannels = [channel];
        var parent = CatalogueParentKindMapper.ToNewsOrganisation(podcast);

        // Act
        var transient = source.ToTransientPodcastForAdditionalYouTubeChannel(parent, clips);

        // Assert
        transient.YouTubeChannelId.Should().Be(clips);
        transient.YouTubePlaylistId.Should().Be(clipsPlaylist);
        transient.YouTubePlaylistOwnerChannelId.Should().Be(clips);
        transient.YouTubePlaylistQueryIsExpensive.Should().BeTrue();
        transient.YouTubeChannelSearchForbidden.Should().BeTrue();
        transient.YouTubePlaylistIdHistory.Should().BeNull();
        source.YouTubePlaylistId.Should().Be(podcast.YouTubePlaylistId);
        source.YouTubePlaylistQueryIsExpensive.Should().BeFalse();
    }
}
