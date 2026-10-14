using System.Net;
using FluentAssertions;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Thumbnails;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.PodcastServices.YouTube.Clients;
using RedditPodcastPoster.PodcastServices.YouTube.Thumbnails;
using RedditPodcastPoster.PodcastServices.YouTube.Video;
using static RedditPodcastPoster.Episodes.TestSupport.Fixtures.YouTubeThumbnailSpecimens;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Thumbnails;

/// <summary>
/// Delayed re-check of recent YouTube images, from a real episode indexed about an hour after release
/// with <c>maxresdefault.jpg</c>. By the next morning i.ytimg.com answered 404 for maxresdefault and
/// hq720 with the 1097-byte 120x90 grey placeholder; sddefault (640x480, 60207 bytes), hqdefault
/// (480x360, 39854 bytes) and mqdefault (320x180, 20678 bytes) answered 200.
/// Uses the real <see cref="ImageUpdater"/> and <see cref="YouTubeThumbnailResolver"/> over stubbed HTTP.
/// </summary>
public class RecentYouTubeThumbnailRecheckRules
{
    private const int PlaceholderWidth = 120;
    private const int PlaceholderHeight = 90;
    private const int PlaceholderBytes = 1097;
    private const int StandardWidth = 640;
    private const int StandardHeight = 480;
    private const int StandardBytes = 60207;
    private const int HighWidth = 480;
    private const int HighHeight = 360;
    private const int HighBytes = 39854;
    private const int MediumWidth = 320;
    private const int MediumHeight = 180;
    private const int MediumBytes = 20678;
    private const int MaxresWidth = 1280;
    private const int MaxresHeight = 720;

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly YouTubeThumbnailHttpStub _http = new();
    private readonly YouTubeThumbnailRecheckOptions _options = new();
    private readonly DateTime _now;
    private readonly Podcast _podcast;
    private readonly List<Google.Apis.YouTube.v3.Data.Video> _videos = [];

    public RecentYouTubeThumbnailRecheckRules()
    {
        _now = DomainTestFixture.UtcAtTime(0, _fixture.CreateNonMidnightTimeOfDay());
        _podcast = _fixture.CreatePodcast();
        _mocker.Use<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(_now, TimeSpan.Zero)));
        _mocker.Use(Options.Create(_options));
        _mocker.GetMock<IHttpClientFactory>()
            .Setup(factory => factory.CreateClient(YouTubeThumbnailResolver.HttpClientName))
            .Returns(() => new HttpClient(_http, disposeHandler: false));
        _mocker.GetMock<IYouTubeVideoService>()
            .Setup(service => service.GetVideoContentDetails(
                It.IsAny<IYouTubeServiceWrapper>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IndexingContext>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .ReturnsAsync((IYouTubeServiceWrapper _, IEnumerable<string> ids, IndexingContext _, bool _, bool _,
                bool _) => _videos.Where(video => ids.Contains(video.Id)).ToList());
        _mocker.Use<IYouTubeThumbnailResolver>(_mocker.CreateInstance<YouTubeThumbnailResolver>());
        _mocker.Use<IImageUpdater>(_mocker.CreateInstance<ImageUpdater>());
    }

    [Fact(DisplayName =
        "When a YouTube episode released an hour ago has maxresdefault stored and YouTube now answers 404 with the " +
        "120x90 placeholder, the re-check replaces it with sddefault (640x480) and saves, because the episode page " +
        "must not show the grey 'missing image' placeholder.")]
    public async Task withdrawn_maxres_is_replaced_with_standard_tier()
    {
        // Arrange
        var episode = RecentYouTubeEpisode(_now.AddHours(-1), MaxresTier);
        var youTubeId = YouTubeId(episode);
        ServeWithdrawnMaxres(youTubeId);

        // Act
        var result = await Sut().Recheck([new PodcastEpisode(_podcast, episode)], new IndexingContext());

        // Assert
        StoredImage(episode).Should().Be(Url(youTubeId, StandardTier));
        _mocker.GetMock<IEpisodeRepository>().Verify(
            repository => repository.Save(It.Is<Episode>(saved =>
                saved.Id == episode.Id && StoredImage(saved) == Url(youTubeId, StandardTier))),
            Times.Once);
        result.Replaced.Should().Be(1);
    }

    [Fact(DisplayName =
        "When the stored maxresdefault still answers 200 with a 1280x720 JPEG, the re-check keeps it and saves " +
        "nothing, because a live high-resolution image is the best one available.")]
    public async Task live_maxres_is_kept()
    {
        // Arrange
        var episode = RecentYouTubeEpisode(_now.AddHours(-1), MaxresTier);
        var youTubeId = YouTubeId(episode);
        _http.Serve(Url(youTubeId, MaxresTier), HttpStatusCode.OK, Jpeg(MaxresWidth, MaxresHeight));

        // Act
        var result = await Sut().Recheck([new PodcastEpisode(_podcast, episode)], new IndexingContext());

        // Assert
        StoredImage(episode).Should().Be(Url(youTubeId, MaxresTier));
        _mocker.GetMock<IEpisodeRepository>().Verify(repository => repository.Save(It.IsAny<Episode>()),
            Times.Never);
        result.StillUsable.Should().Be(1);
    }

    [Fact(DisplayName =
        "When the stored maxresdefault is withdrawn and no other thumbnail is usable, the re-check keeps the " +
        "stored image and saves nothing, because an episode image is never cleared.")]
    public async Task stored_image_is_kept_when_nothing_better_resolves()
    {
        // Arrange
        var episode = RecentYouTubeEpisode(_now.AddHours(-1), MaxresTier);
        var youTubeId = YouTubeId(episode);
        var placeholder = Jpeg(PlaceholderWidth, PlaceholderHeight, PlaceholderBytes);
        foreach (var tier in new[] { MaxresTier, StandardTier, HighTier, MediumTier, DefaultTier })
        {
            _http.Serve(Url(youTubeId, tier), HttpStatusCode.NotFound, placeholder);
        }

        _videos.Add(VideoWithApiThumbnails(youTubeId));

        // Act
        var result = await Sut().Recheck([new PodcastEpisode(_podcast, episode)], new IndexingContext());

        // Assert
        StoredImage(episode).Should().Be(Url(youTubeId, MaxresTier));
        _mocker.GetMock<IEpisodeRepository>().Verify(repository => repository.Save(It.IsAny<Episode>()),
            Times.Never);
        result.Unresolved.Should().Be(1);
    }

    [Fact(DisplayName =
        "An episode released longer ago than the re-check window is not probed, because YouTube only withdraws " +
        "high-resolution frames shortly after upload and older images are settled.")]
    public async Task episode_outside_the_window_is_not_probed()
    {
        // Arrange
        var episode = RecentYouTubeEpisode(_now.AddHours(-(_options.RecheckWindowHours + 1)), MaxresTier);
        ServeWithdrawnMaxres(YouTubeId(episode));

        // Act
        var result = await Sut().Recheck([new PodcastEpisode(_podcast, episode)], new IndexingContext());

        // Assert
        _http.Requested.Should().BeEmpty();
        result.Eligible.Should().Be(0);
    }

    [Fact(DisplayName =
        "A stored image on a tier YouTube does not withdraw (sddefault) is not probed, because only " +
        "maxresdefault and hq720 disappear after release.")]
    public async Task stable_tier_is_not_probed()
    {
        // Arrange
        var episode = RecentYouTubeEpisode(_now.AddHours(-1), StandardTier);

        // Act
        var result = await Sut().Recheck([new PodcastEpisode(_podcast, episode)], new IndexingContext());

        // Assert
        _http.Requested.Should().BeEmpty();
        result.Eligible.Should().Be(0);
    }

    [Fact(DisplayName =
        "Each run probes at most the configured number of stored images, newest release first, and leaves the " +
        "rest for a later run, so HTTP calls per run stay bounded.")]
    public async Task probes_are_bounded_newest_first()
    {
        // Arrange
        _options.MaxStoredImageProbesPerRun = 2;
        var newest = RecentYouTubeEpisode(_now.AddHours(-1), MaxresTier);
        var middle = RecentYouTubeEpisode(_now.AddHours(-2), Hq720Tier);
        var oldest = RecentYouTubeEpisode(_now.AddHours(-3), MaxresTier);
        foreach (var episode in new[] { newest, middle, oldest })
        {
            _http.Serve(StoredImage(episode)!, HttpStatusCode.OK, Jpeg(MaxresWidth, MaxresHeight));
        }

        // Act
        var result = await Sut().Recheck(
            [new PodcastEpisode(_podcast, oldest), new PodcastEpisode(_podcast, newest), new PodcastEpisode(_podcast, middle)],
            new IndexingContext());

        // Assert
        _http.Requested.Should().Equal(StoredImage(newest), StoredImage(middle));
        result.DeferredOverBudget.Should().Be(1);
    }

    [Fact(DisplayName =
        "Each run re-resolves at most the configured number of withdrawn images, so YouTube API calls per run stay bounded.")]
    public async Task re_resolves_are_bounded()
    {
        // Arrange
        _options.MaxReResolvesPerRun = 1;
        var newest = RecentYouTubeEpisode(_now.AddHours(-1), MaxresTier);
        var older = RecentYouTubeEpisode(_now.AddHours(-2), MaxresTier);
        ServeWithdrawnMaxres(YouTubeId(newest));
        ServeWithdrawnMaxres(YouTubeId(older));

        // Act
        var result = await Sut().Recheck(
            [new PodcastEpisode(_podcast, newest), new PodcastEpisode(_podcast, older)],
            new IndexingContext());

        // Assert
        StoredImage(newest).Should().Be(Url(YouTubeId(newest), StandardTier));
        StoredImage(older).Should().Be(Url(YouTubeId(older), MaxresTier));
        result.Replaced.Should().Be(1);
        result.DeferredOverBudget.Should().Be(1);
    }

    [Fact(DisplayName =
        "When YouTube resolving is skipped for this run (quota), the re-check does nothing, because replacing an " +
        "image needs a YouTube API call; a later run picks it up inside the window.")]
    public async Task skipped_when_youtube_resolving_is_skipped()
    {
        // Arrange
        var episode = RecentYouTubeEpisode(_now.AddHours(-1), MaxresTier);
        ServeWithdrawnMaxres(YouTubeId(episode));

        // Act
        await Sut().Recheck(
            [new PodcastEpisode(_podcast, episode)],
            new IndexingContext(SkipYouTubeUrlResolving: true));

        // Assert
        _http.Requested.Should().BeEmpty();
        StoredImage(episode).Should().Be(Url(YouTubeId(episode), MaxresTier));
    }

    [Fact(DisplayName = "When the re-check is disabled in configuration, no image is probed or changed.")]
    public async Task disabled_does_nothing()
    {
        // Arrange
        _options.Enabled = false;
        var episode = RecentYouTubeEpisode(_now.AddHours(-1), MaxresTier);
        ServeWithdrawnMaxres(YouTubeId(episode));

        // Act
        await Sut().Recheck([new PodcastEpisode(_podcast, episode)], new IndexingContext());

        // Assert
        _http.Requested.Should().BeEmpty();
        StoredImage(episode).Should().Be(Url(YouTubeId(episode), MaxresTier));
    }

    [Fact(DisplayName =
        "When the image updater re-resolves YouTube and no thumbnail is usable, it keeps the stored image and " +
        "reports no update, because an episode image is never cleared and an unchanged episode is not re-saved.")]
    public async Task image_updater_keeps_stored_youtube_image_when_nothing_resolves()
    {
        // Arrange
        var episode = RecentYouTubeEpisode(_now.AddHours(-1), MaxresTier);
        var youTubeId = YouTubeId(episode);
        _videos.Add(VideoWithApiThumbnails(youTubeId));
        var sut = _mocker.Get<IImageUpdater>();

        // Act
        var updated = await sut.UpdateImages(
            _podcast,
            episode,
            new Models.EpisodeImageUpdateRequest(UpdateYouTubeImage: true),
            new IndexingContext());

        // Assert
        updated.Should().BeFalse();
        StoredImage(episode).Should().Be(Url(youTubeId, MaxresTier));
    }

    private RecentYouTubeThumbnailRechecker Sut() => _mocker.CreateInstance<RecentYouTubeThumbnailRechecker>();

    private Episode RecentYouTubeEpisode(DateTime release, string storedTier)
    {
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(_podcast, release);
        var youTubeId = YouTubeId(episode);
        EpisodeServicePresence.Upsert(
            episode,
            ServiceKeys.YouTube,
            EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube),
            Url(youTubeId, storedTier));
        return episode;
    }

    private void ServeWithdrawnMaxres(string youTubeId)
    {
        var placeholder = Jpeg(PlaceholderWidth, PlaceholderHeight, PlaceholderBytes);
        _http.Serve(Url(youTubeId, MaxresTier), HttpStatusCode.NotFound, placeholder)
            .Serve(Url(youTubeId, Hq720Tier), HttpStatusCode.NotFound, placeholder)
            .Serve(Url(youTubeId, StandardTier), HttpStatusCode.OK,
                Jpeg(StandardWidth, StandardHeight, StandardBytes))
            .Serve(Url(youTubeId, HighTier), HttpStatusCode.OK, Jpeg(HighWidth, HighHeight, HighBytes))
            .Serve(Url(youTubeId, MediumTier), HttpStatusCode.OK, Jpeg(MediumWidth, MediumHeight, MediumBytes));
        _videos.Add(VideoWithApiThumbnails(youTubeId));
    }

    private static Google.Apis.YouTube.v3.Data.Video VideoWithApiThumbnails(string youTubeId) => new()
    {
        Id = youTubeId,
        Snippet = new VideoSnippet
        {
            Thumbnails = new ThumbnailDetails
            {
                Maxres = Thumbnail(youTubeId, MaxresTier, MaxresHeight),
                Standard = Thumbnail(youTubeId, StandardTier, StandardHeight),
                High = Thumbnail(youTubeId, HighTier, HighHeight),
                Medium = Thumbnail(youTubeId, MediumTier, MediumHeight),
                Default__ = Thumbnail(youTubeId, DefaultTier, PlaceholderHeight)
            }
        }
    };

    private static Thumbnail Thumbnail(string youTubeId, string tier, int height) =>
        new() { Url = Url(youTubeId, tier).AbsoluteUri, Height = height };

    private static string YouTubeId(Episode episode) => EpisodeServicePresence.YouTubeEpisodeId(episode)!;

    private static Uri? StoredImage(Episode episode) => EpisodeServicePresence.TryGetImage(episode, ServiceKeys.YouTube);
}
