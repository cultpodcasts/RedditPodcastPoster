using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class StreamingServiceCatalogRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "The composed streaming catalog lists bbcIplayer before bbcSounds in ImageCoalesceOrder, " +
        "because iPlayer cover art is preferred when both BBC slots are populated.")]
    public void image_coalesce_prefers_iplayer_before_sounds()
    {
        // Arrange
        var order = StreamingServiceCatalog.ImageCoalesceOrder;

        // Act
        var iplayer = Array.IndexOf(order, StreamingServiceWire.ToKey(StreamingService.BbcIplayer));
        var sounds = Array.IndexOf(order, StreamingServiceWire.ToKey(StreamingService.BbcSounds));

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
        var sounds = Array.IndexOf(keys, StreamingServiceWire.ToKey(StreamingService.BbcSounds));
        var iplayer = Array.IndexOf(keys, StreamingServiceWire.ToKey(StreamingService.BbcIplayer));

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

    [Fact(DisplayName =
        "ImageCoalesceOrder after IndexIdImageOrder matches ImageCoalesceKeys (full enum incl. submit-retired), " +
        "while SearchEncodedKeys is SubmitEligibleKeys only; iPlayer still precedes Sounds for cover-art preference.")]
    public void image_coalesce_includes_retired_keys_search_encoded_is_submit_eligible()
    {
        // Arrange
        var coalesceStreaming = StreamingServiceCatalog.ImageCoalesceOrder
            .Skip(ServiceCatalog.IndexIdImageOrder.Length)
            .ToArray();
        var encoded = StreamingServiceCatalog.SearchEncodedKeys;
        var huluKey = StreamingServiceWire.ToKey(StreamingService.Hulu);

        // Act
        var iplayer = Array.IndexOf(StreamingServiceCatalog.ImageCoalesceOrder, StreamingServiceWire.ToKey(StreamingService.BbcIplayer));
        var sounds = Array.IndexOf(StreamingServiceCatalog.ImageCoalesceOrder, StreamingServiceWire.ToKey(StreamingService.BbcSounds));

        // Assert
        coalesceStreaming.Should().Equal(StreamingServiceWire.ImageCoalesceKeys);
        coalesceStreaming.Should().Contain(huluKey);
        encoded.Should().Equal(StreamingServiceWire.SubmitEligibleKeys);
        encoded.Should().NotContain(huluKey);
        iplayer.Should().BeGreaterThanOrEqualTo(0);
        sounds.Should().BeGreaterThan(iplayer);
    }

    [Fact(DisplayName =
        "StreamingServiceCatalog.Use rejects an empty registration list, because replacing a loaded catalog with none would fail-open SearchEncodedKeys and compact.")]
    public void use_rejects_empty_registrations()
    {
        // Arrange
        IReadOnlyList<IStreamingServiceRegistration> empty = [];

        // Act
        var act = () => StreamingServiceCatalog.Use(empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("registrations")
            .WithMessage("*EnsureLoaded*");
        StreamingServiceCatalog.SearchEncodedKeys.Should().NotBeEmpty();
    }

    [Fact(DisplayName =
        "After the catalog is loaded, SearchEncodedKeys is non-empty and a specimen Tubi movie URL compact round-trips, because search encode must not skip the ordered plugin list.")]
    public void loaded_catalog_search_encoded_keys_compact_tubi_round_trips()
    {
        // Arrange
        var id = _fixture.CreateAppleId();
        var url = new Uri($"https://tubitv.com/movies/{id}");

        // Act
        var keys = StreamingServiceCatalog.SearchEncodedKeys;
        var compact = StreamingServiceCatalog.TryCompactUrl(StreamingServiceWire.ToKey(StreamingService.Tubi), url);
        var expanded = StreamingServiceCatalog.TryExpandCompactUrl(StreamingServiceWire.ToKey(StreamingService.Tubi), compact!);

        // Assert
        keys.Should().NotBeEmpty();
        keys.Should().Contain(StreamingServiceWire.ToKey(StreamingService.Tubi));
        compact.Should().Be($"movies/{id}");
        expanded.Should().Be(new Uri($"https://tubitv.com/movies/{id}"));
    }

    [Fact(DisplayName =
        "TryResolveKey maps an iPlayer episode URL to bbcIplayer, because an unloaded or Sounds-first fallback would store Sounds for iPlayer pastes.")]
    public void try_resolve_key_maps_iplayer_url_to_bbc_iplayer()
    {
        // Arrange
        var url = new Uri($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}");

        // Act
        var key = StreamingServiceCatalog.TryResolveKey(url);

        // Assert
        key.Should().Be(StreamingServiceWire.ToKey(StreamingService.BbcIplayer));
    }
}
