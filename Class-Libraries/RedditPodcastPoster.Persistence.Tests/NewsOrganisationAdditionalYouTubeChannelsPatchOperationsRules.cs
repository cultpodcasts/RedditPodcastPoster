using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Repositories;

namespace RedditPodcastPoster.Persistence.Tests;

/// <summary>
/// #1370: the indexer writes back a news organisation's per-channel YouTube flags as one guarded set of its own
/// <c>/additionalYouTubeChannels</c> (not under <c>/indexing</c>, which TV shows share).
/// </summary>
public class NewsOrganisationAdditionalYouTubeChannelsPatchOperationsRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Indexer patch: a news organisation's additional channels (per-channel indexer flags) are set at /additionalYouTubeChannels in one operation guarded on the stored channel and playlist ids, because the list also holds curator-owned ids and a curator edit saved mid-pass must not be overwritten.")]
    public void additional_channels_are_set_alone_and_guarded_on_their_ids()
    {
        // Arrange
        var archive = _fixture.CreateYouTubeChannelId();
        var clips = _fixture.CreateYouTubeChannelId();
        var clipsPlaylist = _fixture.CreateYouTubePlaylistId();
        var scoped = AdditionalYouTubeChannel.ForPlaylist(clips, clipsPlaylist);
        scoped.YouTubeChannelSearchForbidden = true;

        // Act
        var set = NewsOrganisationAdditionalYouTubeChannelsPatchOperations.Build(
            [AdditionalYouTubeChannel.ChannelWide(archive), scoped], new JsonSerializerOptions());
        var filter = NewsOrganisationAdditionalYouTubeChannelsPatchOperations.UnchangedFilter(set.Value!.AsArray());

        // Assert
        set.OperationType.Should().Be(PatchOperationType.Set);
        set.Path.Should().Be("/additionalYouTubeChannels");
        set.Value!.AsArray()[1]!["youTubeChannelSearchForbidden"]!.GetValue<bool>().Should().BeTrue();
        filter.Should().Be(
            "FROM c WHERE IS_ARRAY(c[\"additionalYouTubeChannels\"]) " +
            "AND ARRAY_LENGTH(c[\"additionalYouTubeChannels\"]) = 2 " +
            $"AND c[\"additionalYouTubeChannels\"][0].youTubeChannelId = '{archive}' " +
            "AND (NOT IS_DEFINED(c[\"additionalYouTubeChannels\"][0].youTubePlaylistId) " +
            "OR IS_NULL(c[\"additionalYouTubeChannels\"][0].youTubePlaylistId)) " +
            $"AND c[\"additionalYouTubeChannels\"][1].youTubeChannelId = '{clips}' " +
            $"AND c[\"additionalYouTubeChannels\"][1].youTubePlaylistId = '{clipsPlaylist}'");
    }

    [Fact(DisplayName =
        "Indexer patch: the additional-channel guard quotes ids, so a stray quote in a stored id cannot break or widen the Cosmos filter.")]
    public void additional_channels_guard_quotes_ids()
    {
        // Arrange
        var channels = new JsonArray(new JsonObject { ["youTubeChannelId"] = "UC'x" });

        // Act
        var filter = NewsOrganisationAdditionalYouTubeChannelsPatchOperations.UnchangedFilter(channels);

        // Assert
        filter.Should().Contain("youTubeChannelId = 'UC\\'x'");
    }
}
