using FluentAssertions;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;

namespace Indexer.Tests;

public class PodcastEpisodeStreamingSearchMappingTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "ToEpisodeSearchRecord maps an ITVX-only episode services.itvx url into svc and services.itvx image " +
        "into image, because streaming catalogue keys are search destinations not only Spotify/Apple/YouTube/BBC/IA.")]
    public void maps_itvx_url_and_image_into_svc_and_image()
    {
        // Arrange
        var slug = _fixture.CreateGuid().ToString("N")[..12];
        var programmeId = _fixture.CreateGuid().ToString("N")[..12];
        var itvxUrl = new Uri($"https://www.itv.com/watch/{slug}/{programmeId}/{programmeId}");
        var itvxImage = new Uri(
            $"https://ovp.itv.com/v2/images/special/{slug}/itv_hub/01_Hero_DesktopCTV/16x9?distributionPartner=itv_hub&fallback=standard&w=2236&q=80&blur=0&bg=false");
        var episode = _fixture.CreateEpisode(e =>
        {
            EpisodeServicePresence.Upsert(e, ServiceKeys.Itvx, itvxUrl, itvxImage);
        });
        var podcast = _fixture.CreatePodcast();

        // Act
        var record = new PodcastEpisode(podcast, episode).ToEpisodeSearchRecord();

        // Assert
        record.Svc.Should().Be(SearchEpisodeServices.Compact(episode.Services));
        record.Svc.Should().StartWith($"{ServiceKeys.Itvx}:u");
        record.Svc.Should().Contain(itvxUrl.ToString());
        SearchEpisodeServices.Expand(record.Svc).Should().ContainSingle()
            .Which.Should().Be((ServiceKeys.Itvx, itvxUrl));
        record.Image.Should().Be(itvxImage.ToString());
        record.SpotifyId.Should().BeNull();
        record.YoutubeId.Should().BeNull();
        record.AppleId.Should().BeNull();
        record.BBC.Should().BeEmpty();
        record.InternetArchive.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "SearchEpisodeServices.Compact encodes every SearchEncodedKeys service that has a URL, including ITVX, " +
        "so push-path indexing cannot drop streaming destinations that are present on Episode.Services.")]
    public void compact_includes_itvx_among_search_encoded_keys()
    {
        // Arrange
        var itvxUrl = new Uri($"https://www.itv.com/watch/{_fixture.CreateGuid():N}/a/b");
        var services = new Dictionary<string, EpisodeServiceLink>
        {
            [ServiceKeys.Itvx] = new() { Url = itvxUrl }
        };

        // Act
        var compact = SearchEpisodeServices.Compact(services);

        // Assert
        compact.Should().Contain($"{ServiceKeys.Itvx}:u");
        SearchEpisodeServices.Expand(compact).Should().ContainSingle()
            .Which.Url.Should().Be(itvxUrl);
    }
}
