using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.Models.Services;

namespace Indexer.Tests;

public class SearchEpisodeServicesTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "Search svc encoding stores BBC Sounds as a compact play-id when the URL is the standard sounds/play shape, because the index must stay small while remaining loss-less.")]
    public void Compacts_bbc_sounds_play_url_to_id()
    {
        // Arrange
        var services = new Dictionary<string, ServiceLink>
        {
            [StreamingServiceWire.ToKey(StreamingService.BbcSounds)] = new()
            {
                Url = new Uri("https://www.bbc.co.uk/sounds/play/p0example")
            }
        };

        // Act
        var compact = SearchEpisodeServices.Compact(services);
        var expanded = SearchEpisodeServices.Expand(compact);

        // Assert
        compact.Should().Be("bbcSounds:p0example");
        expanded.Should().ContainSingle()
            .Which.Url.ToString().Should().Be("https://www.bbc.co.uk/sounds/play/p0example");
    }

    [Fact(DisplayName =
        "Search svc encoding stores Vimeo as a numeric id when the URL is vimeo.com/{id}, so Vimeo uses the same compact grammar as BBC and Archive.")]
    public void Compacts_vimeo_watch_url_to_id()
    {
        // Arrange
        var services = new Dictionary<string, ServiceLink>
        {
            [StreamingServiceWire.ToKey(StreamingService.Vimeo)] = new() { Url = new Uri("https://vimeo.com/123456789") }
        };

        // Act
        var compact = SearchEpisodeServices.Compact(services);
        var expanded = SearchEpisodeServices.Expand(compact);

        // Assert
        compact.Should().Be("vimeo:123456789");
        expanded.Should().ContainSingle()
            .Which.Url.ToString().Should().Be("https://vimeo.com/123456789");
    }

    [Fact(DisplayName =
        "Search svc encoding stores BcVideo as a video id when the URL is the canonical /video/{id} shape, so BcVideo uses the same compact grammar as Vimeo.")]
    public void Compacts_bc_video_watch_url_to_id()
    {
        // Arrange
        var host = "bitchute";
        var id = _fixture.CreateBcVideoId();
        var services = new Dictionary<string, ServiceLink>
        {
            [StreamingServiceWire.ToKey(StreamingService.BcVideo)] = new() { Url = new Uri($"https://www.{host}.com/video/{id}") }
        };

        // Act
        var compact = SearchEpisodeServices.Compact(services);
        var expanded = SearchEpisodeServices.Expand(compact);

        // Assert
        compact.Should().Be($"{host}:{id}");
        expanded.Should().ContainSingle()
            .Which.Url.ToString().Should().Be($"https://www.{host}.com/video/{id}");
    }

    [Fact(DisplayName =
        "Search svc encoding stores a BcVideo /embed/{id} URL as the same compact token as /video/{id}, and Expand rebuilds the canonical watch URL.")]
    public void Compacts_bc_video_embed_url_to_same_id()
    {
        // Arrange
        var host = "bitchute";
        var id = _fixture.CreateBcVideoId();
        var services = new Dictionary<string, ServiceLink>
        {
            [StreamingServiceWire.ToKey(StreamingService.BcVideo)] = new() { Url = new Uri($"https://www.{host}.com/embed/{id}") }
        };

        // Act
        var compact = SearchEpisodeServices.Compact(services);
        var expanded = SearchEpisodeServices.Expand(compact);

        // Assert
        compact.Should().Be($"{host}:{id}");
        expanded.Should().ContainSingle()
            .Which.Url.ToString().Should().Be($"https://www.{host}.com/video/{id}");
    }


    [Fact(DisplayName =
        "Search svc encoding stores Tubi as movies/{id} when the URL is a locale movie page, so Expand rebuilds the canonical /movies/{id} URL.")]
    public void Compacts_tubi_locale_movie_url_to_kind_and_id()
    {
        // Arrange
        var id = _fixture.CreateAppleId();
        var services = new Dictionary<string, ServiceLink>
        {
            [StreamingServiceWire.ToKey(StreamingService.Tubi)] = new() { Url = new Uri($"https://tubitv.com/en-au/movies/{id}/{_fixture.CreateYouTubeId()}") }
        };

        // Act
        var compact = SearchEpisodeServices.Compact(services);
        var expanded = SearchEpisodeServices.Expand(compact);

        // Assert
        compact.Should().Be($"tubi:movies/{id}");
        expanded.Should().ContainSingle()
            .Which.Url.ToString().Should().Be($"https://tubitv.com/movies/{id}");
    }


    [Fact(DisplayName =
        "Search svc encoding omits Spotify/YouTube/Apple because those URLs are rebuilt from index id fields, keeping quota for services that are not id-derivable.")]
    public void Omits_reconstructable_platform_ids()
    {
        // Arrange
        var services = new Dictionary<string, ServiceLink>
        {
            [ServiceKeys.Spotify] = new() { Url = new Uri("https://open.spotify.com/episode/opaqueid00000000000000") },
            [ServiceKeys.YouTube] = new() { Url = new Uri("https://www.youtube.com/watch?v=griffinsong42") },
            [StreamingServiceWire.ToKey(StreamingService.InternetArchive)] = new() { Url = new Uri("https://archive.org/details/harbour-vale-ep") }
        };

        // Act
        var compact = SearchEpisodeServices.Compact(services);

        // Assert
        compact.Should().Be("internetArchive:harbour-vale-ep");
    }

    [Fact(DisplayName =
        "Search svc encoding keeps a full Netflix title URL when it cannot be compacted without changing the original string, so documentary links are never lossy.")]
    public void Keeps_nonstandard_netflix_url_as_full_payload()
    {
        // Arrange
        var url = new Uri("https://www.netflix.com/watch/81040344?trackId=14262865");
        var services = new Dictionary<string, ServiceLink>
        {
            [StreamingServiceWire.ToKey(StreamingService.Netflix)] = new() { Url = url }
        };

        // Act
        var compact = SearchEpisodeServices.Compact(services);
        var expanded = SearchEpisodeServices.Expand(compact);

        // Assert
        compact.Should().StartWith("netflix:uhttps://");
        expanded.Should().ContainSingle().Which.Url.Should().Be(url);
    }
}
