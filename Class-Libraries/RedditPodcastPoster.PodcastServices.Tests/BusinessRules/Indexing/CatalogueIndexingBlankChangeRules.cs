using System.Text.Json;
using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.PodcastServices.Updaters;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

public class CatalogueIndexingBlankChangeRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "An index pass over a parent saved with \"\" ids and regexes reports no changed indexing members, because the " +
        "transient podcast's \"\" values are captured back as unset (null), so the Indexer writes nothing for them.")]
    public void legacy_blank_values_do_not_produce_an_indexer_patch()
    {
        // Arrange
        var parent = CatalogueParentKindMapper.ToNewsOrganisation(_fixture.CreatePodcast());
        var indexing = JsonSerializer.Deserialize<CatalogueIndexingSource>(
            """
            {"hasBundledEpisodes":false,"indexAllEpisodes":true,"spotifyId":"","youTubeChannelId":"UCsyntheticChannel0000001",
             "youTubePlaylistId":"","titleRegex":"","descriptionRegex":"","episodeMatchRegex":"",
             "episodeIncludeTitleRegex":""}
            """)!;
        var before = CatalogueIndexingChanges.Snapshot(indexing);

        // Act
        indexing.CaptureFrom(indexing.ToTransientPodcast(parent));

        // Assert
        CatalogueIndexingChanges.ChangedSince(before, indexing).Should().BeEmpty();
    }
}
