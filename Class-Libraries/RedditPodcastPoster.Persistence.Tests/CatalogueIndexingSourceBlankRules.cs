using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Providers;

namespace RedditPodcastPoster.Persistence.Tests;

/// <summary>
/// The string ids and regexes on a news organisation's / TV show's <c>indexing</c> block are null when unset, never
/// <c>""</c>: blanks are normalised on set and on read, and the Cosmos serializer omits nulls.
/// </summary>
public class CatalogueIndexingSourceBlankRules
{
    private static readonly string[] BlankableFields =
    [
        "spotifyId", "youTubeChannelId", "youTubePlaylistId", "titleRegex", "descriptionRegex", "episodeMatchRegex",
        "episodeIncludeTitleRegex"
    ];

    private readonly DomainTestFixture _fixture = new();

    private static JsonSerializerOptions CosmosOptions => new JsonSerializerOptionsProvider().GetJsonSerializerOptions();

    [Fact(DisplayName =
        "A podcast with the default \"\" ids and regexes becomes an indexing block that stores none of them, because " +
        "the kind transfer and the catalogue migration must not write empty strings onto the parent.")]
    public void podcast_defaults_are_not_stored()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();

        // Act
        var stored = (JsonObject) JsonSerializer.SerializeToNode(CatalogueIndexingSource.FromPodcast(podcast), CosmosOptions)!;

        // Assert
        stored.Select(pair => pair.Key).Should().NotContain(BlankableFields);
    }

    [Theory(DisplayName =
        "Empty or whitespace set on an id or regex reads back as null, because a blank means unset.")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    [InlineData(null)]
    public void blank_values_are_null(string? blank)
    {
        // Arrange
        var source = new CatalogueIndexingSource();

        // Act
        source.SpotifyId = blank;
        source.YouTubeChannelId = blank;
        source.YouTubePlaylistId = blank;
        source.TitleRegex = blank;
        source.DescriptionRegex = blank;
        source.EpisodeMatchRegex = blank;
        source.EpisodeIncludeTitleRegex = blank;

        // Assert
        new[]
        {
            source.SpotifyId, source.YouTubeChannelId, source.YouTubePlaylistId, source.TitleRegex,
            source.DescriptionRegex, source.EpisodeMatchRegex, source.EpisodeIncludeTitleRegex
        }.Should().AllSatisfy(value => value.Should().BeNull());
        source.IsAutoIndexed.Should().BeFalse();
    }

    [Fact(DisplayName =
        "A document saved with \"\" ids and regexes reads them as null and writes them back omitted, while set values " +
        "are kept exactly (not trimmed), so existing documents are cleaned on their next full save.")]
    public void legacy_empty_strings_are_dropped_and_values_kept()
    {
        // Arrange
        const string json =
            """
            {"hasBundledEpisodes":false,"indexAllEpisodes":false,"spotifyId":"","youTubeChannelId":"UCsyntheticChannel0000001",
             "youTubePlaylistId":"","titleRegex":"","descriptionRegex":" ","episodeMatchRegex":"",
             "episodeIncludeTitleRegex":"^Synthetic "}
            """;

        // Act
        var source = JsonSerializer.Deserialize<CatalogueIndexingSource>(json, CosmosOptions)!;
        var stored = (JsonObject) JsonSerializer.SerializeToNode(source, CosmosOptions)!;

        // Assert
        stored.Select(pair => pair.Key).Should().BeEquivalentTo(
            "hasBundledEpisodes", "indexAllEpisodes", "youTubeChannelId", "episodeIncludeTitleRegex");
        stored["youTubeChannelId"]!.GetValue<string>().Should().Be("UCsyntheticChannel0000001");
        stored["episodeIncludeTitleRegex"]!.GetValue<string>().Should().Be("^Synthetic ");
    }

    [Fact(DisplayName =
        "Unset ids and regexes become \"\" on the transient podcast, the podcast's own unset value, so the indexer " +
        "and submit pipeline treat them exactly as an unset podcast field.")]
    public void transient_podcast_gets_podcast_unset_values()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var parent = CatalogueParentKindMapper.ToNewsOrganisation(podcast);

        // Act
        var transient = new CatalogueIndexingSource().ToTransientPodcast(parent);

        // Assert
        new[]
        {
            transient.SpotifyId, transient.YouTubeChannelId, transient.YouTubePlaylistId, transient.TitleRegex,
            transient.DescriptionRegex, transient.EpisodeMatchRegex, transient.EpisodeIncludeTitleRegex
        }.Should().AllSatisfy(value => value.Should().BeEmpty());
    }
}
