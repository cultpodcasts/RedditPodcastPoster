using System.Xml;
using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Services;
using RedditPodcastPoster.PodcastServices.YouTube.Video;

namespace RedditPodcastPoster.PodcastServices.YouTube.Tests.Finders;

/// <summary>
/// Thin-wrapper rules: YouTube playlist finder delegates catalogue matching to the domain matcher.
/// </summary>
public class PlaylistItemFinderCatalogueWrapperRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    public PlaylistItemFinderCatalogueWrapperRules()
    {
        _mocker.Use(EpisodeDomainTestServices.CreatePlatformMatcher());
        _mocker.GetMock<IYouTubeVideoService>()
            .Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync((
                IYouTubeServiceWrapper _,
                IEnumerable<string> videoIds,
                IndexingContext _,
                bool _,
                bool _) =>
                videoIds.Select(id => CreateCompletedVideo(id, TimeSpan.FromHours(1))).ToList());
    }

    private IPlaylistItemFinder Sut => _mocker.CreateInstance<PlaylistItemFinder>();

    [Fact(DisplayName =
        "When the playlist finder resolves by exact episode title, " +
        "it returns the PlaylistItem whose title and duration match the stored episode.")]
    public async Task find_by_exact_title_delegates_duration_validation_and_maps_back()
    {
        // Arrange
        var sharedTitle = _fixture.CreateTitle();
        var episodeLength = TimeSpan.FromHours(1);
        var videoId = _fixture.CreateYouTubeId();
        var episode = _fixture.BuildEpisode()
            .Customize(e =>
            {
                e.Title = sharedTitle;
                e.Length = episodeLength;
            })
            .Create();
        var playlistItems = new List<PlaylistItem>
        {
            CreatePlaylistItem(videoId, sharedTitle, DomainTestFixture.UtcDaysAgo(1))
        };
        ConfigureVideoDuration(videoId, episodeLength);

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay: null,
            new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.PlaylistItem!.GetVideoId().Should().Be(videoId);
    }

    [Fact(DisplayName =
        "When duration-only matching fails but publish delay aligns with the domain catalogue matcher, " +
        "the playlist finder returns the delayed YouTube video via IsCatalogueMatch delegation.")]
    public async Task find_by_publish_delay_delegates_to_domain_is_catalogue_match()
    {
        // Arrange
        var episodeTitle = _fixture.CreateShortTitle();
        var catalogueTitle = DomainTestFixture.CreateFuzzyTitleVariant(episodeTitle);
        var episodeLength = TimeSpan.FromHours(1);
        var catalogueVideoLength = episodeLength - TimeSpan.FromMinutes(3);
        var youTubePublishDelay = TimeSpan.FromHours(-6);
        var release = DomainTestFixture.UtcAtTime(-4, _fixture.CreateNonMidnightTimeOfDay());
        var appleId = _fixture.CreateAppleId();
        var spotifyId = _fixture.CreateSpotifyId();
        var appleUrl = new Uri($"https://podcasts.apple.com/us/podcast/episode/id{appleId}");
        var spotifyUrl = _fixture.DefaultSpotifyUrl(spotifyId);
        var matchingVideoId = _fixture.CreateYouTubeId();
        var episode = _fixture.BuildEpisode()
            .Customize(e =>
            {
                e.Title = episodeTitle;
                e.Length = episodeLength;
                e.Release = release;
                e.AppleId = appleId;
                e.SpotifyId = spotifyId;
                e.Urls = new ServiceUrls { Apple = appleUrl, Spotify = spotifyUrl };
            })
            .Create();
        var publishedAt = release.Add(youTubePublishDelay);
        var playlistItems = new List<PlaylistItem>
        {
            CreatePlaylistItem(matchingVideoId, catalogueTitle, publishedAt)
        };
        ConfigureVideoDuration(matchingVideoId, catalogueVideoLength);

        // Act
        var result = await Sut.FindMatchingYouTubeVideo(
            episode,
            playlistItems,
            youTubePublishDelay,
            new IndexingContext());

        // Assert
        result.Should().NotBeNull();
        result!.PlaylistItem!.GetVideoId().Should().Be(matchingVideoId);
    }

    private void ConfigureVideoDuration(string videoId, TimeSpan duration)
    {
        _mocker.GetMock<IYouTubeVideoService>()
            .Setup(x => x.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.Is<IEnumerable<string>>(ids => ids.Contains(videoId)),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync((
                IYouTubeServiceWrapper _,
                IEnumerable<string> videoIds,
                IndexingContext _,
                bool _,
                bool _) =>
                videoIds.Select(id => CreateCompletedVideo(id, id == videoId ? duration : TimeSpan.FromMinutes(20)))
                    .ToList());
    }

    private static PlaylistItem CreatePlaylistItem(
        string videoId,
        string title,
        DateTimeOffset publishedAt) =>
        new()
        {
            Snippet = new PlaylistItemSnippet
            {
                Title = title,
                ResourceId = new ResourceId { VideoId = videoId },
                PublishedAtDateTimeOffset = publishedAt
            }
        };

    private static Google.Apis.YouTube.v3.Data.Video CreateCompletedVideo(string videoId, TimeSpan duration) =>
        new()
        {
            Id = videoId,
            ContentDetails = new VideoContentDetails
            {
                Duration = XmlConvert.ToString(duration)
            },
            Snippet = new VideoSnippet { LiveBroadcastContent = "none" }
        };
}
