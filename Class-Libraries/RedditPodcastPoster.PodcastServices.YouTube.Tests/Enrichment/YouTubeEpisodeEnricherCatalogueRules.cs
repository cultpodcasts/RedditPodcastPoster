using System.Text.RegularExpressions;
using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.Adapters;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Enrichment;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using RedditPodcastPoster.Text;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Enrichment;

/// <summary>
/// YouTube episode enricher catalogue and fill-missing link rules.
/// </summary>
public class YouTubeEpisodeEnricherCatalogueRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "When a stored episode has a YouTube ID but no URL, the enricher backfills the watch URL " +
        "and marks the enrichment context as updated.")]
    public async Task enrich_backfills_youtube_url_when_id_present_and_url_missing()
    {
        // Arrange
        var youTubeId = _fixture.CreateYouTubeId();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.YouTubeId = youTubeId;
                e.Urls = new ServiceUrls();
            })
            .Create();
        var enrichmentContext = new EnrichmentContext();
        var sut = CreateEnricher();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        enrichmentContext.YouTubeUrlUpdated.Should().BeTrue();
        episode.Urls.YouTube.Should().Be(SearchResultExtensions.ToYouTubeUrl(youTubeId));
    }

    [Fact(DisplayName =
        "When a stored episode has a YouTube URL that does not contain an extractable video id, " +
        "the enricher leaves YouTube identity unchanged.")]
    public async Task enrich_leaves_episode_unchanged_when_url_has_no_extractable_video_id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.YouTubeId = string.Empty;
                e.Urls = new ServiceUrls { YouTube = new Uri("https://www.youtube.com/channel/UCexample") };
            })
            .Create();
        var enrichmentContext = new EnrichmentContext();
        var resolver = new Mock<IYouTubeItemResolver>();
        var sut = CreateEnricher(youTubeItemResolver: resolver.Object);

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        episode.YouTubeId.Should().BeNullOrWhiteSpace();
        enrichmentContext.YouTubeIdUpdated.Should().BeFalse();
        resolver.Verify(
            x => x.FindEpisode(It.IsAny<EnrichmentRequest>(), It.IsAny<IndexingContext>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When a matching catalogue item is found and the episode already has description and thumbnail, " +
        "the enricher does not load supplemental YouTube video details.")]
    public async Task enrich_skips_video_details_when_description_and_thumbnail_present()
    {
        // Arrange
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput();
        var podcast = _fixture.CreatePodcast();
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Title = youTubeInput.Title;
                e.Length = youTubeInput.Duration;
                e.Release = youTubeInput.Release;
                e.Description = _fixture.Create<string>();
                e.YouTubeId = string.Empty;
                e.Urls = new ServiceUrls();
                e.Images = new EpisodeImages { YouTube = _fixture.Create<Uri>() };
            })
            .Create();
        var resolver = new Mock<IYouTubeItemResolver>();
        resolver
            .Setup(x => x.FindEpisode(It.IsAny<EnrichmentRequest>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(CreateSearchResultResponse(
                youTubeInput.YouTubeId,
                youTubeInput.Title,
                youTubeInput.Release));
        var videoService = new Mock<IYouTubeVideoService>();
        var sut = CreateEnricher(
            youTubeItemResolver: resolver.Object,
            youTubeVideoService: videoService.Object);
        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        enrichmentContext.YouTubeUrlUpdated.Should().BeTrue();
        videoService.Verify(
            x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                true,
                It.IsAny<bool>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When a stored episode has a YouTube URL but no ID, the enricher extracts the video ID " +
        "and marks the enrichment context YouTubeId flag.")]
    public async Task enrich_sets_youtube_id_when_url_present_and_id_missing()
    {
        // Arrange
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.YouTubeId = string.Empty;
                e.Urls = new ServiceUrls { YouTube = youTubeInput.YouTubeUrl };
            })
            .Create();
        var enrichmentContext = new EnrichmentContext();
        var sut = CreateEnricher();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        episode.YouTubeId.Should().Be(youTubeInput.YouTubeId);
        enrichmentContext.YouTubeIdUpdated.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When a matching YouTube catalogue item is found and the episode has no description, " +
        "the enricher loads video details and fills the description via the applicator.")]
    public async Task enrich_fills_description_from_video_details_when_missing()
    {
        // Arrange
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b.WithDescription(string.Empty));
        var rawDescription = _fixture.Create<string>();
        var podcast = _fixture.CreatePodcast();
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Title = youTubeInput.Title;
                e.Length = youTubeInput.Duration;
                e.Release = youTubeInput.Release;
                e.Description = string.Empty;
                e.YouTubeId = string.Empty;
                e.Urls = new ServiceUrls();
                e.Images = new EpisodeImages();
            })
            .Create();
        var enrichmentContext = new EnrichmentContext();
        var resolver = new Mock<IYouTubeItemResolver>();
        resolver
            .Setup(x => x.FindEpisode(It.IsAny<EnrichmentRequest>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(CreateSearchResultResponse(youTubeInput.YouTubeId, youTubeInput.Title, youTubeInput.Release));
        var videoService = new Mock<IYouTubeVideoService>();
        videoService
            .Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                true,
                It.IsAny<bool>()))
            .ReturnsAsync([
                new Google.Apis.YouTube.v3.Data.Video
                {
                    Id = youTubeInput.YouTubeId,
                    Snippet = new VideoSnippet { Description = rawDescription }
                }
            ]);
        var sut = CreateEnricher(
            youTubeItemResolver: resolver.Object,
            youTubeVideoService: videoService.Object);

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        episode.Description.Should().Be(rawDescription);
        enrichmentContext.YouTubeUrlUpdated.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When the podcast defines a description regex, the enricher sanitises the YouTube video description " +
        "before applying it to the episode.")]
    public async Task enrich_sanitises_description_when_podcast_has_description_regex()
    {
        // Arrange
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b.WithDescription(string.Empty));
        var rawDescription = "Patreon exclusive content follows";
        var sanitizedDescription = "Public episode summary";
        var podcast = _fixture.CreatePodcast();
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        podcast.DescriptionRegex = "Patreon.*";
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Title = youTubeInput.Title;
                e.Length = youTubeInput.Duration;
                e.Release = youTubeInput.Release;
                e.Description = string.Empty;
                e.YouTubeId = string.Empty;
                e.Urls = new ServiceUrls();
            })
            .Create();
        var textSanitiser = new Mock<ITextSanitiser>();
        textSanitiser
            .Setup(x => x.SanitiseDescription(
                rawDescription,
                It.IsAny<Regex>()))
            .Returns(sanitizedDescription);
        var resolver = new Mock<IYouTubeItemResolver>();
        resolver
            .Setup(x => x.FindEpisode(It.IsAny<EnrichmentRequest>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(CreateSearchResultResponse(youTubeInput.YouTubeId, youTubeInput.Title, youTubeInput.Release));
        var videoService = new Mock<IYouTubeVideoService>();
        videoService
            .Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                true,
                It.IsAny<bool>()))
            .ReturnsAsync([
                new Google.Apis.YouTube.v3.Data.Video
                {
                    Id = youTubeInput.YouTubeId,
                    Snippet = new VideoSnippet { Description = rawDescription }
                }
            ]);
        var sut = CreateEnricher(
            youTubeItemResolver: resolver.Object,
            youTubeVideoService: videoService.Object,
            textSanitiser: textSanitiser.Object);

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            new EnrichmentContext());

        // Assert
        episode.Description.Should().Be(sanitizedDescription);
        textSanitiser.Verify(
            x => x.SanitiseDescription(rawDescription, It.IsAny<Regex>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When Apple identity is missing and the stored release is midnight UTC, a matching YouTube catalogue item " +
        "backfills publish time-of-day on the same calendar date.")]
    public async Task enrich_backfills_release_time_when_apple_missing_and_stored_release_is_midnight()
    {
        // Arrange
        var midnightRelease = DomainTestFixture.UtcDateDaysAgo(4);
        var youTubeRelease = DomainTestFixture.SameCalendarDateWithTime(
            midnightRelease,
            _fixture.CreateNonMidnightTimeOfDay());
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithRelease(youTubeRelease)
            .WithDescription(string.Empty));
        var podcast = _fixture.CreatePodcast();
        podcast.AppleId = null;
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Title = youTubeInput.Title;
                e.Length = youTubeInput.Duration;
                e.Release = midnightRelease;
                e.AppleId = null;
                e.Description = _fixture.Create<string>();
                e.YouTubeId = string.Empty;
                e.Urls = new ServiceUrls();
                e.Images = new EpisodeImages { YouTube = _fixture.Create<Uri>() };
            })
            .Create();
        var enrichmentContext = new EnrichmentContext();
        var resolver = new Mock<IYouTubeItemResolver>();
        resolver
            .Setup(x => x.FindEpisode(It.IsAny<EnrichmentRequest>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(CreateSearchResultResponse(
                youTubeInput.YouTubeId,
                youTubeInput.Title,
                youTubeRelease));
        var sut = CreateEnricher(youTubeItemResolver: resolver.Object);

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        episode.Release.Should().Be(youTubeRelease);
        enrichmentContext.ReleaseUpdated.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When the resolver returns a playlist item match without a search result, the enricher applies " +
        "the catalogue candidate via the playlist-item path.")]
    public async Task enrich_applies_catalogue_match_via_playlist_item()
    {
        // Arrange
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b.WithDescription(string.Empty));
        var podcast = _fixture.CreatePodcast();
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Title = youTubeInput.Title;
                e.Length = youTubeInput.Duration;
                e.Release = youTubeInput.Release;
                e.Description = _fixture.Create<string>();
                e.YouTubeId = string.Empty;
                e.Urls = new ServiceUrls();
                e.Images = new EpisodeImages { YouTube = _fixture.Create<Uri>() };
            })
            .Create();
        var enrichmentContext = new EnrichmentContext();
        var resolver = new Mock<IYouTubeItemResolver>();
        resolver
            .Setup(x => x.FindEpisode(It.IsAny<EnrichmentRequest>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(CreatePlaylistItemResponse(
                youTubeInput.YouTubeId,
                youTubeInput.Title,
                youTubeInput.Release));
        var sut = CreateEnricher(youTubeItemResolver: resolver.Object);

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        episode.YouTubeId.Should().Be(youTubeInput.YouTubeId);
        episode.Urls.YouTube.Should().NotBeNull();
        enrichmentContext.YouTubeUrlUpdated.Should().BeTrue();
        enrichmentContext.YouTubeIdUpdated.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When YouTube catalogue returns a video id already owned by another stored episode, " +
        "YouTube enrichment leaves the current episode unchanged.")]
    public async Task enrich_skips_youtube_id_already_owned_by_another_episode()
    {
        // Arrange
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput();
        var podcast = _fixture.CreatePodcast();
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        var sharedRelease = youTubeInput.Release;
        var sharedLength = youTubeInput.Duration;
        var sharedTitle = youTubeInput.Title;
        var current = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithTitle(sharedTitle)
            .WithRelease(sharedRelease)
            .WithLength(sharedLength)
            .Customize(e =>
            {
                e.YouTubeId = string.Empty;
                e.Urls = new ServiceUrls();
            })
            .Create();
        var other = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithTitle(_fixture.CreateTitle())
            .WithRelease(sharedRelease.AddDays(-1))
            .WithLength(sharedLength)
            .Customize(e => e.YouTubeId = youTubeInput.YouTubeId)
            .Create();
        var resolver = new Mock<IYouTubeItemResolver>();
        resolver
            .Setup(x => x.FindEpisode(It.IsAny<EnrichmentRequest>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(CreateSearchResultResponse(
                youTubeInput.YouTubeId,
                sharedTitle,
                sharedRelease));
        var sut = CreateEnricher(youTubeItemResolver: resolver.Object);
        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [current, other], current),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        current.YouTubeId.Should().BeNullOrWhiteSpace();
        current.Urls.YouTube.Should().BeNull();
        enrichmentContext.YouTubeUrlUpdated.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a matching catalogue item is found and the episode is missing a YouTube thumbnail, " +
        "the enricher loads video details and applies the resolved image via the applicator.")]
    public async Task enrich_applies_thumbnail_when_episode_image_missing()
    {
        // Arrange
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b.WithDescription(string.Empty));
        var thumbnailUrl = _fixture.Create<Uri>();
        var podcast = _fixture.CreatePodcast();
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Title = youTubeInput.Title;
                e.Length = youTubeInput.Duration;
                e.Release = youTubeInput.Release;
                e.Description = _fixture.Create<string>();
                e.YouTubeId = string.Empty;
                e.Urls = new ServiceUrls();
                e.Images = new EpisodeImages();
            })
            .Create();
        var resolver = new Mock<IYouTubeItemResolver>();
        resolver
            .Setup(x => x.FindEpisode(It.IsAny<EnrichmentRequest>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync(CreateSearchResultResponse(
                youTubeInput.YouTubeId,
                youTubeInput.Title,
                youTubeInput.Release));
        var videoService = new Mock<IYouTubeVideoService>();
        videoService
            .Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                true,
                It.IsAny<bool>()))
            .ReturnsAsync([
                new Google.Apis.YouTube.v3.Data.Video
                {
                    Id = youTubeInput.YouTubeId,
                    Snippet = new VideoSnippet { Description = string.Empty }
                }
            ]);
        var thumbnailResolver = new Mock<IYouTubeThumbnailResolver>();
        thumbnailResolver
            .Setup(x => x.GetImageUrlAsync(It.IsAny<Google.Apis.YouTube.v3.Data.Video>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(thumbnailUrl);
        var sut = CreateEnricher(
            youTubeItemResolver: resolver.Object,
            youTubeVideoService: videoService.Object,
            youTubeThumbnailResolver: thumbnailResolver.Object);
        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        episode.Images.Should().NotBeNull();
        episode.Images!.YouTube.Should().Be(thumbnailUrl);
        enrichmentContext.YouTubeUrlUpdated.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When no matching YouTube catalogue item is found, the enricher leaves YouTube identity " +
        "unchanged and does not mark YouTube URL flags.")]
    public async Task enrich_leaves_episode_unchanged_when_no_catalogue_match()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.YouTubeId = string.Empty;
                e.Urls = new ServiceUrls();
            })
            .Create();
        var enrichmentContext = new EnrichmentContext();
        var resolver = new Mock<IYouTubeItemResolver>();
        resolver
            .Setup(x => x.FindEpisode(It.IsAny<EnrichmentRequest>(), It.IsAny<IndexingContext>()))
            .ReturnsAsync((FindEpisodeResponse?)null);
        var sut = CreateEnricher(youTubeItemResolver: resolver.Object);

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        episode.YouTubeId.Should().BeNullOrWhiteSpace();
        episode.Urls.YouTube.Should().BeNull();
        enrichmentContext.YouTubeUrlUpdated.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When the episode is still inside the delayed YouTube publishing window, YouTube enrichment " +
        "is bypassed and does not query the catalogue.")]
    public async Task enrich_is_bypassed_inside_delayed_youtube_publishing_window()
    {
        // Arrange
        var publishingDelay = TimeSpan.FromDays(1);
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        podcast.YouTubePublicationOffset = publishingDelay.Ticks;
        var inWindowRelease = DomainTestFixture.SpotifyCatalogueReleaseStillInsideDelayedPublishingWindow(
            publishingDelay);
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .WithRelease(inWindowRelease)
            .WithLength(_fixture.CreateDuration())
            .Customize(e =>
            {
                e.YouTubeId = string.Empty;
                e.Urls = new ServiceUrls();
            })
            .Create();
        var resolver = new Mock<IYouTubeItemResolver>();
        var sut = CreateEnricher(youTubeItemResolver: resolver.Object);
        var enrichmentContext = new EnrichmentContext();

        // Act
        await sut.Enrich(
            new EnrichmentRequest(podcast, [episode], episode),
            new IndexingContext(),
            enrichmentContext);

        // Assert
        resolver.Verify(
            x => x.FindEpisode(It.IsAny<EnrichmentRequest>(), It.IsAny<IndexingContext>()),
            Times.Never);
        enrichmentContext.YouTubeUrlUpdated.Should().BeFalse();
    }

    private YouTubeEpisodeEnricher CreateEnricher(
        IYouTubeItemResolver? youTubeItemResolver = null,
        IYouTubeVideoService? youTubeVideoService = null,
        ITextSanitiser? textSanitiser = null,
        IYouTubeThumbnailResolver? youTubeThumbnailResolver = null)
    {
        var youTubeService = new Mock<IYouTubeServiceWrapper>();
        var resolver = youTubeItemResolver ?? new Mock<IYouTubeItemResolver>().Object;
        var videoService = youTubeVideoService ?? new Mock<IYouTubeVideoService>().Object;
        var sanitiser = textSanitiser ?? new Mock<ITextSanitiser>().Object;
        var thumbnailResolver = youTubeThumbnailResolver ?? CreateDefaultThumbnailResolver();

        return new YouTubeEpisodeEnricher(
            youTubeService.Object,
            resolver,
            sanitiser,
            videoService,
            thumbnailResolver,
            new EpisodePlatformApplier(),
            new YouTubeEpisodeAdapter(),
            EpisodeDomainTestServices.CreateEnrichmentApplicator(),
            NullLogger<YouTubeEpisodeEnricher>.Instance);
    }

    private static FindEpisodeResponse CreateSearchResultResponse(
        string youTubeId,
        string title,
        DateTime release) =>
        new(new SearchResult
        {
            Id = new ResourceId { VideoId = youTubeId },
            Snippet = new SearchResultSnippet
            {
                Title = title,
                PublishedAtDateTimeOffset = new DateTimeOffset(release, TimeSpan.Zero)
            }
        });

    private static FindEpisodeResponse CreatePlaylistItemResponse(
        string youTubeId,
        string title,
        DateTime release) =>
        new(PlaylistItem: new PlaylistItem
        {
            Snippet = new PlaylistItemSnippet
            {
                ResourceId = new ResourceId { VideoId = youTubeId },
                Title = title,
                PublishedAtDateTimeOffset = new DateTimeOffset(release, TimeSpan.Zero)
            }
        });

    private static IYouTubeThumbnailResolver CreateDefaultThumbnailResolver()
    {
        var thumbnailResolver = new Mock<IYouTubeThumbnailResolver>();
        thumbnailResolver
            .Setup(x => x.GetImageUrlAsync(It.IsAny<Google.Apis.YouTube.v3.Data.Video>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Uri?)null);
        return thumbnailResolver.Object;
    }
}
