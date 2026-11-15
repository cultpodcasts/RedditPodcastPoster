using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Providers;

namespace RedditPodcastPoster.Persistence.Tests;

/// <summary>
/// #1370: a news organisation's own <c>additionalYouTubeChannels</c> holds its YouTube channels beside the main
/// <c>indexing.youTubeChannelId</c>, each owned as a whole channel or through its own optional playlist. The field is on
/// the news organisation only: the shared <see cref="CatalogueIndexingSource"/> and <see cref="TvShow"/> have no such
/// field, and a stray one on a stored document is neither read nor written back.
/// </summary>
public class NewsOrganisationAdditionalYouTubeChannelsModelRules
{
    private readonly DomainTestFixture _fixture = new();

    private static JsonSerializerOptions CosmosOptions => new JsonSerializerOptionsProvider().GetJsonSerializerOptions();

    private static IEnumerable<string> WireNames(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always })
            .Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? p.Name);

    [Fact(DisplayName =
        "Only the news organisation has additionalYouTubeChannels: the shared indexing settings and the TV show have no such field, because a TV show has one YouTube channel and must not carry a field it never uses.")]
    public void only_the_news_organisation_has_additional_channels()
    {
        // Act
        var organisation = WireNames(typeof(NewsOrganisation)).ToList();
        var show = WireNames(typeof(TvShow)).ToList();
        var indexing = WireNames(typeof(CatalogueIndexingSource)).ToList();

        // Assert
        organisation.Should().Contain("additionalYouTubeChannels");
        show.Should().NotContain(x => x.Contains("additional", StringComparison.OrdinalIgnoreCase));
        indexing.Should().NotContain(x => x.Contains("additional", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName =
        "Additional YouTube channels are stored trimmed, without blank channel ids or repeated channels (the first entry wins), because a channel is held once.")]
    public void additional_channels_are_normalised()
    {
        // Arrange
        var first = _fixture.CreateYouTubeChannelId();
        var second = _fixture.CreateYouTubeChannelId();
        var playlist = _fixture.CreateYouTubePlaylistId();
        var organisation = new NewsOrganisation(_fixture.CreateTitle());

        // Act
        organisation.AdditionalYouTubeChannels =
        [
            AdditionalYouTubeChannel.ForPlaylist($" {first} ", $" {playlist} "),
            AdditionalYouTubeChannel.ChannelWide(""),
            AdditionalYouTubeChannel.ChannelWide("  "),
            AdditionalYouTubeChannel.ForPlaylist(second, "  "),
            AdditionalYouTubeChannel.ChannelWide(first)
        ];

        // Assert
        organisation.AdditionalYouTubeChannelIdList().Should().Equal(first, second);
        organisation.AdditionalYouTubeChannels![0].YouTubePlaylistId.Should().Be(playlist);
        organisation.AdditionalYouTubeChannels[1].YouTubePlaylistId.Should().BeNull();
    }

    [Fact(DisplayName =
        "An empty additional channel list is null and is not written, because an organisation with one channel stores nothing for it and a document without the field stays valid.")]
    public void empty_additional_channels_are_not_stored()
    {
        // Arrange
        var organisation = new NewsOrganisation(_fixture.CreateTitle())
        {
            Indexing = new CatalogueIndexingSource { YouTubeChannelId = _fixture.CreateYouTubeChannelId() },
            AdditionalYouTubeChannels = [AdditionalYouTubeChannel.ChannelWide("   ")]
        };

        // Act
        var stored = (JsonObject) JsonSerializer.SerializeToNode(organisation, CosmosOptions)!;
        var read = JsonSerializer.Deserialize<NewsOrganisation>(stored.ToJsonString(), CosmosOptions)!;

        // Assert
        organisation.AdditionalYouTubeChannels.Should().BeNull();
        stored.Select(pair => pair.Key).Should().NotContain("additionalYouTubeChannels");
        read.AdditionalYouTubeChannels.Should().BeNull();
        read.Indexing!.YouTubeChannelId.Should().Be(organisation.Indexing!.YouTubeChannelId);
    }

    [Fact(DisplayName =
        "Additional YouTube channels round-trip through the Cosmos serializer as the news organisation's own additionalYouTubeChannels (beside indexing, not inside it) with each channel's playlist and indexer flags, and a whole channel writes only its id, because the indexer, URL submission and discovery read them from the stored news organisation.")]
    public void additional_channels_round_trip()
    {
        // Arrange
        var main = _fixture.CreateYouTubeChannelId();
        var archive = _fixture.CreateYouTubeChannelId();
        var clips = _fixture.CreateYouTubeChannelId();
        var clipsPlaylist = _fixture.CreateYouTubePlaylistId();
        var scoped = AdditionalYouTubeChannel.ForPlaylist(clips, clipsPlaylist);
        scoped.YouTubePlaylistQueryIsExpensive = true;
        scoped.YouTubeChannelSearchForbidden = true;
        var organisation = new NewsOrganisation(_fixture.CreateTitle())
        {
            Indexing = new CatalogueIndexingSource { YouTubeChannelId = main },
            AdditionalYouTubeChannels = [AdditionalYouTubeChannel.ChannelWide(archive), scoped]
        };

        // Act
        var json = JsonSerializer.Serialize(organisation, CosmosOptions);
        var read = JsonSerializer.Deserialize<NewsOrganisation>(json, CosmosOptions)!;

        // Assert
        var document = JsonNode.Parse(json)!.AsObject();
        document["indexing"]!.AsObject().Select(pair => pair.Key).Should().NotContain("additionalYouTubeChannels");
        document["indexing"]!["youTubeChannelId"]!.GetValue<string>().Should().Be(main);
        var stored = document["additionalYouTubeChannels"]!.AsArray();
        stored[0]!.AsObject().Select(pair => pair.Key).Should().Equal("youTubeChannelId");
        stored[0]!["youTubeChannelId"]!.GetValue<string>().Should().Be(archive);
        stored[1]!["youTubePlaylistId"]!.GetValue<string>().Should().Be(clipsPlaylist);
        stored[1]!["youTubePlaylistQueryIsExpensive"]!.GetValue<bool>().Should().BeTrue();
        stored[1]!["youTubeChannelSearchForbidden"]!.GetValue<bool>().Should().BeTrue();
        read.AdditionalYouTubeChannelIdList().Should().Equal(archive, clips);
        read.AdditionalYouTubeChannelFor(clips)!.YouTubePlaylistId.Should().Be(clipsPlaylist);
        read.AdditionalYouTubeChannelFor(clips)!.YouTubePlaylistQueryIsExpensive.Should().BeTrue();
        read.AdditionalYouTubeChannelFor(clips)!.YouTubeChannelSearchForbidden.Should().BeTrue();
    }

    [Fact(DisplayName =
        "A stray additionalYouTubeChannels on a stored TV show document (top level or inside indexing) is not read, not honoured as a channel the show holds, and not written back, because a TV show has no additional channels in its object model.")]
    public void stray_additional_channels_on_a_tv_show_are_ignored()
    {
        // Arrange
        var main = _fixture.CreateYouTubeChannelId();
        var stray = _fixture.CreateYouTubeChannelId();
        var show = new TvShow(_fixture.CreateTitle())
        {
            Indexing = new CatalogueIndexingSource { YouTubeChannelId = main }
        };
        var document = JsonSerializer.SerializeToNode(show, CosmosOptions)!.AsObject();
        document["additionalYouTubeChannels"] = new JsonArray(new JsonObject { ["youTubeChannelId"] = stray });
        document["indexing"]!["additionalYouTubeChannels"] =
            new JsonArray(new JsonObject { ["youTubeChannelId"] = stray });

        // Act
        var read = JsonSerializer.Deserialize<TvShow>(document.ToJsonString(), CosmosOptions)!;
        var channels = CatalogueParentYouTubeChannels.Of(read);
        var written = JsonSerializer.SerializeToNode(read, CosmosOptions)!.AsObject();

        // Assert
        channels.All().Should().Equal(main);
        channels.Holds(stray).Should().BeFalse();
        written.Select(pair => pair.Key).Should().NotContain("additionalYouTubeChannels");
        written["indexing"]!.AsObject().Select(pair => pair.Key).Should().NotContain("additionalYouTubeChannels");
    }

    [Fact(DisplayName =
        "A news organisation's channels are its main channel and each additional channel; each channel is scoped only by its own playlist (the main playlist never scopes an additional channel, and an additional channel's playlist never scopes the main one), because one organisation may claim one channel whole and only a playlist of another.")]
    public void each_channel_is_scoped_by_its_own_playlist()
    {
        // Arrange
        var main = _fixture.CreateYouTubeChannelId();
        var archive = _fixture.CreateYouTubeChannelId();
        var clips = _fixture.CreateYouTubeChannelId();
        var mainPlaylist = _fixture.CreateYouTubePlaylistId();
        var clipsPlaylist = _fixture.CreateYouTubePlaylistId();
        var organisation = new NewsOrganisation(_fixture.CreateTitle())
        {
            Indexing = new CatalogueIndexingSource { YouTubeChannelId = main, YouTubePlaylistId = mainPlaylist },
            AdditionalYouTubeChannels =
            [
                AdditionalYouTubeChannel.ChannelWide(archive),
                AdditionalYouTubeChannel.ForPlaylist(clips, clipsPlaylist)
            ]
        };

        // Act
        var channels = CatalogueParentYouTubeChannels.Of(organisation);

        // Assert
        channels.All().Should().Equal(main, archive, clips);
        channels.Holds(archive).Should().BeTrue();
        channels.Holds(clips).Should().BeTrue();
        channels.Holds(_fixture.CreateYouTubeChannelId()).Should().BeFalse();
        channels.PlaylistIdOn(main).Should().Be(mainPlaylist);
        channels.PlaylistIdOn(archive).Should().BeNull();
        channels.PlaylistIdOn(clips).Should().Be(clipsPlaylist);
        channels.PlaylistIdOn(_fixture.CreateYouTubeChannelId()).Should().BeNull();
        channels.ClaimsChannelWide(main).Should().BeFalse();
        channels.ClaimsChannelWide(archive).Should().BeTrue();
        channels.ClaimsChannelWide(clips).Should().BeFalse();
    }

    [Fact(DisplayName =
        "A TV show's channels are its main channel only, scoped by its main playlist, because only a news organisation can hold additional YouTube channels.")]
    public void a_tv_show_holds_its_main_channel_only()
    {
        // Arrange
        var main = _fixture.CreateYouTubeChannelId();
        var show = new TvShow(_fixture.CreateTitle())
        {
            Indexing = new CatalogueIndexingSource { YouTubeChannelId = main }
        };

        // Act
        var channels = CatalogueParentYouTubeChannels.Of(show);

        // Assert
        channels.All().Should().Equal(main);
        channels.ClaimsChannelWide(main).Should().BeTrue();
        channels.Holds(_fixture.CreateYouTubeChannelId()).Should().BeFalse();
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
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);
        organisation.Indexing ??= CatalogueIndexingSource.FromPodcast(podcast);
        var archive = _fixture.CreateYouTubeChannelId();
        organisation.AdditionalYouTubeChannels = [AdditionalYouTubeChannel.ChannelWide(archive)];

        // Act
        var transient = organisation.ToTransientPodcastForAdditionalYouTubeChannel(archive);

        // Assert
        transient.Id.Should().Be(organisation.Id);
        transient.YouTubeChannelId.Should().Be(archive);
        transient.YouTubePlaylistId.Should().BeEmpty();
        transient.YouTubePlaylistOwnerChannelId.Should().BeNull();
        transient.YouTubePlaylistQueryIsExpensive.Should().BeNull();
        transient.YouTubeChannelSearchForbidden.Should().BeNull();
        transient.YouTubeIndexingHints.Should().BeNull();
        transient.SpotifyId.Should().BeEmpty();
        transient.AppleId.Should().BeNull();
        transient.IndexAllEpisodes.Should().BeTrue();
        organisation.Indexing.YouTubeChannelId.Should().Be(podcast.YouTubeChannelId);
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
        var organisation = CatalogueParentKindMapper.ToNewsOrganisation(podcast);
        organisation.Indexing ??= CatalogueIndexingSource.FromPodcast(podcast);
        var clips = _fixture.CreateYouTubeChannelId();
        var clipsPlaylist = _fixture.CreateYouTubePlaylistId();
        var channel = AdditionalYouTubeChannel.ForPlaylist(clips, clipsPlaylist);
        channel.YouTubePlaylistQueryIsExpensive = true;
        channel.YouTubeChannelSearchForbidden = true;
        organisation.AdditionalYouTubeChannels = [channel];

        // Act
        var transient = organisation.ToTransientPodcastForAdditionalYouTubeChannel(clips);

        // Assert
        transient.YouTubeChannelId.Should().Be(clips);
        transient.YouTubePlaylistId.Should().Be(clipsPlaylist);
        transient.YouTubePlaylistOwnerChannelId.Should().Be(clips);
        transient.YouTubePlaylistQueryIsExpensive.Should().BeTrue();
        transient.YouTubeChannelSearchForbidden.Should().BeTrue();
        transient.YouTubePlaylistIdHistory.Should().BeNull();
        organisation.Indexing.YouTubePlaylistId.Should().Be(podcast.YouTubePlaylistId);
        organisation.Indexing.YouTubePlaylistQueryIsExpensive.Should().BeFalse();
    }
}
