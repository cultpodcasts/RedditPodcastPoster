using FluentAssertions;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class StreamingServiceCatalogRules
{
    [Fact(DisplayName =
        "The composed streaming catalog lists bbcIplayer before bbcSounds in ImageCoalesceOrder, " +
        "because iPlayer cover art is preferred when both BBC slots are populated.")]
    public void image_coalesce_prefers_iplayer_before_sounds()
    {
        // Arrange
        var order = StreamingServiceCatalog.ImageCoalesceOrder;

        // Act
        var iplayer = Array.IndexOf(order, StreamingServiceKeys.BbcIplayer);
        var sounds = Array.IndexOf(order, StreamingServiceKeys.BbcSounds);

        // Assert
        iplayer.Should().BeGreaterThanOrEqualTo(0);
        sounds.Should().BeGreaterThan(iplayer);
    }

    [Fact(DisplayName =
        "SearchEncodedKeys lists bbcSounds before bbcIplayer, because the streaming-submit contract encode order is Sounds then iPlayer.")]
    public void search_encoded_keys_list_sounds_before_iplayer()
    {
        // Arrange
        var keys = StreamingServiceCatalog.SearchEncodedKeys;

        // Act
        var sounds = Array.IndexOf(keys, StreamingServiceKeys.BbcSounds);
        var iplayer = Array.IndexOf(keys, StreamingServiceKeys.BbcIplayer);

        // Assert
        sounds.Should().BeGreaterThanOrEqualTo(0);
        iplayer.Should().BeGreaterThan(sounds);
    }

    [Fact(DisplayName =
        "The composed catalog TryGet resolves YouTube, because index-id platforms stay in the catalog even though streaming plugins register separately.")]
    public void try_get_includes_youtube_podcast_platform()
    {
        // Arrange
        // Act
        var found = StreamingServiceCatalog.TryGet(ServiceKeys.YouTube, out var descriptor);

        // Assert
        found.Should().BeTrue();
        descriptor.Key.Should().Be(ServiceKeys.YouTube);
    }
}
