using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.BBC.Extractors;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.InternetArchive.Extractors;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Categorisers;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using RedditPodcastPoster.UrlSubmission.Tests.Support;
using RedditPodcastPoster.PodcastServices.Abstractions.Streaming;
using RedditPodcastPoster.Models.Services;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class UrlMembershipLookupRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private readonly InMemoryPodcastRepository _podcasts = new();

    public UrlMembershipLookupRules()
    {
        _mocker.Use(Options.Create(new SubmitContentTypesOptions()));
        _mocker.Use<IEpisodeRepository>(_episodes);
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<INonPodcastServiceAdapterResolver>(NonPodcastSubmitAdapterResolverSupport.Create(
            _mocker.GetMock<IBBCPageMetaDataExtractor>().Object,
            archiveExtractor: _mocker.GetMock<IInternetArchivePageMetaDataExtractor>().Object));
    }

    [Fact(DisplayName =
        "When a Spotify episode URL is already stored on one series, URL membership lookup returns that podcast id and name without writing episodes.")]
    public async Task known_spotify_url_returns_unique_series()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var url = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify)!;
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.PodcastId.Should().Be(podcast.Id);
        result.PodcastName.Should().Be(podcast.Name);
        result.Kind.Should().Be(UrlMembershipLookupKinds.PodcastService);
        result.Ambiguous.Should().BeFalse();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a Spotify share URL with a tracking query is pasted, URL membership lookup matches nested ids.spotify (and the cleaned catalog URL) on the existing series and does not write episodes.")]
    public async Task known_spotify_share_query_matches_nested_id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var storedUrl = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify)!;
        var url = new Uri(storedUrl.AbsoluteUri + "?si=" + _fixture.CreateSpotifyId());
        url.Should().NotBe(storedUrl);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.PodcastId.Should().Be(podcast.Id);
        result.Kind.Should().Be(UrlMembershipLookupKinds.PodcastService);
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a youtu.be URL is pasted and the series stores watch?v= plus ids.youtube, URL membership lookup returns that podcast without writing episodes.")]
    public async Task known_youtube_short_url_matches_nested_id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var youTubeId = EpisodeServicePresence.YouTubeEpisodeId(episode)!;
        var storedUrl = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube)!;
        var url = new Uri($"https://youtu.be/{youTubeId}");
        url.Should().NotBe(storedUrl);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.PodcastId.Should().Be(podcast.Id);
        result.Kind.Should().Be(UrlMembershipLookupKinds.PodcastService);
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When an Apple episode URL uses a different locale path than the stored /us/ catalog URL, URL membership lookup matches ids.apple and does not write episodes.")]
    public async Task known_apple_locale_path_matches_nested_id()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var appleEpisodeId = _fixture.CreateAppleId();
        var applePodcastId = _fixture.CreateAppleId();
        var storedUrl = new Uri($"https://podcasts.apple.com/us/podcast/episode/id{applePodcastId}?i={appleEpisodeId}");
        var url = new Uri($"https://podcasts.apple.com/gb/podcast/episode/id{applePodcastId}?i={appleEpisodeId}");
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            EpisodeServicePresence.SetAppleIdentity(e, appleEpisodeId);
            EpisodeServicePresence.Upsert(e, ServiceKeys.Apple, storedUrl, null);
        });
        url.Should().NotBe(storedUrl);
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.PodcastId.Should().Be(podcast.Id);
        result.Kind.Should().Be(UrlMembershipLookupKinds.PodcastService);
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a Spotify episode URL is not stored, URL membership lookup returns unknown podcast-service so submit can still create from platform metadata.")]
    public async Task unknown_spotify_url_returns_podcast_service()
    {
        // Arrange
        var url = _fixture.DefaultSpotifyUrl(_fixture.CreateSpotifyId());
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(new UrlMembershipLookupResult(
            false,
            UrlMembershipLookupKinds.PodcastService));
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a streaming URL is already stored on one series, URL membership lookup returns that podcast from URL membership only and does not scrape metadata.")]
    public async Task known_sounds_url_returns_unique_series()
    {
        // Arrange
        var url = BbcSoundsUrl();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e => SeedBbcSoundsLookup(e, url));
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.PodcastId.Should().Be(podcast.Id);
        result.PodcastName.Should().Be(podcast.Name);
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.Service.Should().Be(StreamingServiceWire.ToKey(StreamingService.BbcSounds));
        _episodes.SavedEpisodes.Should().BeEmpty();
        _mocker.GetMock<IBBCPageMetaDataExtractor>().Verify(e => e.GetMetaData(It.IsAny<Uri>()), Times.Never);
    }

    [Fact(DisplayName =
        "When a streaming URL is not stored, URL membership lookup returns unknown streaming with service key and null podcastName, " +
        "never calls the adapter extractor, because membership classifies only and prepare owns HTML fetch.")]
    public async Task unknown_sounds_url_returns_streaming()
    {
        // Arrange
        var url = BbcSoundsUrl();
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(new UrlMembershipLookupResult(
            false,
            UrlMembershipLookupKinds.Streaming,
            Service: StreamingServiceWire.ToKey(StreamingService.BbcSounds)));
        result.PodcastName.Should().BeNull();
        result.PodcastId.Should().BeNull();
        _episodes.SavedEpisodes.Should().BeEmpty();
        _mocker.GetMock<IBBCPageMetaDataExtractor>().Verify(e => e.GetMetaData(It.IsAny<Uri>()), Times.Never);
    }

    [Fact(DisplayName =
        "When a BBC iPlayer episode URL is already stored on one series, URL membership lookup returns that podcast with StreamingServiceWire.ToKey(StreamingService.BbcIplayer) " +
        "because catalogue path resolution distinguishes iPlayer from Sounds, and does not scrape metadata.")]
    public async Task known_iplayer_url_returns_unique_series_with_bbc_iplayer_service()
    {
        // Arrange
        var url = BbcIplayerUrl();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e => SeedBbcIplayerLookup(e, url));
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.PodcastId.Should().Be(podcast.Id);
        result.PodcastName.Should().Be(podcast.Name);
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.Service.Should().Be(StreamingServiceWire.ToKey(StreamingService.BbcIplayer));
        _episodes.SavedEpisodes.Should().BeEmpty();
        _mocker.GetMock<IBBCPageMetaDataExtractor>().Verify(e => e.GetMetaData(It.IsAny<Uri>()), Times.Never);
    }

    [Fact(DisplayName =
        "When a BBC iPlayer episode URL is not stored, URL membership lookup returns unknown streaming with StreamingServiceWire.ToKey(StreamingService.BbcIplayer) and null podcastName " +
        "because StreamingServiceCatalog.TryResolveKey prefers /iplayer/ and membership does not scrape.")]
    public async Task unknown_iplayer_url_returns_streaming_with_bbc_iplayer_service()
    {
        // Arrange
        var url = BbcIplayerUrl();
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(new UrlMembershipLookupResult(
            false,
            UrlMembershipLookupKinds.Streaming,
            Service: StreamingServiceWire.ToKey(StreamingService.BbcIplayer)));
        result.PodcastName.Should().BeNull();
        _episodes.SavedEpisodes.Should().BeEmpty();
        _mocker.GetMock<IBBCPageMetaDataExtractor>().Verify(e => e.GetMetaData(It.IsAny<Uri>()), Times.Never);
    }

    [Fact(DisplayName =
        "When an unknown Vimeo URL is classified, URL membership lookup returns service without podcastName " +
        "because membership does not scrape the Vimeo author.")]
    public async Task unknown_vimeo_leaves_podcast_name_null()
    {
        // Arrange
        var url = new Uri($"https://vimeo.com/{_fixture.CreateAppleId()}");
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse();
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.Service.Should().Be(StreamingServiceWire.ToKey(StreamingService.Vimeo));
        result.PodcastName.Should().BeNull();
        result.PodcastId.Should().BeNull();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a stored Vimeo URL is https://vimeo.com/{id} and the pasted URL is /video/{id}, " +
        "URL membership lookup returns that podcast as known, because both forms are the same video.")]
    public async Task stored_compact_vimeo_url_matches_video_prefix_paste()
    {
        // Arrange
        var id = _fixture.CreateAppleId();
        var storedUrl = new Uri($"https://vimeo.com/{id}");
        var pasted = new Uri($"https://vimeo.com/video/{id}");
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
            EpisodeServicePresence.Upsert(e, StreamingServiceWire.ToKey(StreamingService.Vimeo), storedUrl, null));
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(pasted, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.PodcastId.Should().Be(podcast.Id);
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.Service.Should().Be(StreamingServiceWire.ToKey(StreamingService.Vimeo));
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When an unknown BcVideo URL is classified, URL membership lookup returns service without podcastName " +
        "because membership does not scrape the BcVideo author.")]
    public async Task unknown_bc_video_leaves_podcast_name_null()
    {
        // Arrange
        var id = _fixture.CreateBcVideoId();
        var host = "bitchute.com";
        var url = new Uri($"https://www.{host}/video/{id}/");
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse();
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.Service.Should().Be(StreamingServiceWire.ToKey(StreamingService.BcVideo));
        result.PodcastName.Should().BeNull();
        result.PodcastId.Should().BeNull();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a stored BcVideo watch URL is /video/{id} and the pasted URL is /embed/{id}, " +
        "URL membership lookup returns that podcast as known, because embed and watch are the same video.")]
    public async Task stored_watch_url_matches_embed_paste()
    {
        // Arrange
        var id = _fixture.CreateBcVideoId();
        var host = "bitchute.com";
        var storedUrl = new Uri($"https://www.{host}/video/{id}");
        var embedUrl = new Uri($"https://www.{host}/embed/{id}");
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
            EpisodeServicePresence.Upsert(e, StreamingServiceWire.ToKey(StreamingService.BcVideo), storedUrl, null));
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(embedUrl, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.PodcastId.Should().Be(podcast.Id);
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.Service.Should().Be(StreamingServiceWire.ToKey(StreamingService.BcVideo));
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a stored BcVideo URL is the canonical /video/{id} watch form, " +
        "looking up that same watch URL returns known, which is the inverse of an embed paste after store.")]
    public async Task stored_canonical_watch_url_matches_watch_paste()
    {
        // Arrange
        var id = _fixture.CreateBcVideoId();
        var host = "bitchute.com";
        var storedUrl = new Uri($"https://www.{host}/video/{id}");
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
            EpisodeServicePresence.Upsert(e, StreamingServiceWire.ToKey(StreamingService.BcVideo), storedUrl, null));
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(storedUrl, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.PodcastId.Should().Be(podcast.Id);
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.Service.Should().Be(StreamingServiceWire.ToKey(StreamingService.BcVideo));
        _episodes.SavedEpisodes.Should().BeEmpty();
    }


    [Fact(DisplayName =
        "When an unknown Tubi URL is classified, URL membership lookup returns service without podcastName " +
        "because membership does not scrape the film title.")]
    public async Task unknown_tubi_leaves_podcast_name_null()
    {
        // Arrange
        var id = _fixture.CreateAppleId();
        var url = new Uri($"https://tubitv.com/en-au/movies/{id}/{_fixture.CreateYouTubeId()}");
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse();
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.Service.Should().Be(StreamingServiceWire.ToKey(StreamingService.Tubi));
        result.PodcastName.Should().BeNull();
        result.PodcastId.Should().BeNull();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a stored Tubi URL is the canonical /movies/{id} form and the pasted URL is locale plus slug, " +
        "URL membership lookup returns that podcast as known, because both forms are the same title.")]
    public async Task stored_canonical_tubi_url_matches_locale_paste()
    {
        // Arrange
        var id = _fixture.CreateAppleId();
        var storedUrl = new Uri($"https://tubitv.com/movies/{id}");
        var pasted = new Uri($"https://tubitv.com/en-au/movies/{id}/{_fixture.CreateYouTubeId()}");
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
            EpisodeServicePresence.Upsert(e, StreamingServiceWire.ToKey(StreamingService.Tubi), storedUrl, null));
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(pasted, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.PodcastId.Should().Be(podcast.Id);
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.Service.Should().Be(StreamingServiceWire.ToKey(StreamingService.Tubi));
        _episodes.SavedEpisodes.Should().BeEmpty();
    }


    [Fact(DisplayName =
        "When an unknown Netflix URL is classified, URL membership lookup returns service without podcastName " +
        "because membership does not scrape series metadata.")]
    public async Task unknown_netflix_leaves_podcast_name_null()
    {
        // Arrange
        var url = new Uri($"https://www.netflix.com/watch/{_fixture.CreateAppleId()}");
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse();
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.Service.Should().Be(StreamingServiceWire.ToKey(StreamingService.Netflix));
        result.PodcastName.Should().BeNull();
    }

    [Fact(DisplayName =
        "When an unknown Prime Video URL is classified, URL membership lookup returns service without podcastName " +
        "because membership does not scrape series metadata.")]
    public async Task unknown_prime_leaves_podcast_name_null()
    {
        // Arrange
        var url = new Uri($"https://www.primevideo.com/detail/{_fixture.CreateYouTubeId()}");
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse();
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.Service.Should().Be(StreamingServiceWire.ToKey(StreamingService.AmazonPrime));
        result.PodcastName.Should().BeNull();
    }

    [Fact(DisplayName =
        "When an unknown Internet Archive playlist URL is classified, URL membership lookup returns service without podcastName " +
        "because membership does not scrape collection metadata.")]
    public async Task unknown_archive_leaves_podcast_name_null()
    {
        // Arrange
        var url = new Uri($"https://archive.org/details/{_fixture.CreateYouTubeId()}");
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse();
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.Service.Should().Be(StreamingServiceWire.ToKey(StreamingService.InternetArchive));
        result.PodcastName.Should().BeNull();
        _mocker.GetMock<IInternetArchivePageMetaDataExtractor>().Verify(e => e.GetMetaData(It.IsAny<Uri>()), Times.Never);
    }

    [Fact(DisplayName =
        "When the same stored URL belongs to more than one podcast, URL membership lookup returns known false with ambiguous true and the podcast ids, because the UI must still offer Series.")]
    public async Task ambiguous_stored_url_returns_podcast_ids()
    {
        // Arrange
        var url = BbcSoundsUrl();
        var first = _fixture.CreatePodcast();
        var second = _fixture.CreatePodcast();
        _podcasts.Seed(first, second);
        _episodes.Seed(
            _fixture.CreateStoredEpisode(first, e => SeedBbcSoundsLookup(e, url)),
            _fixture.CreateStoredEpisode(second, e => SeedBbcSoundsLookup(e, url)));
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse();
        result.Ambiguous.Should().BeTrue();
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        result.Service.Should().Be(StreamingServiceWire.ToKey(StreamingService.BbcSounds));
        result.PodcastIds.Should().BeEquivalentTo([first.Id, second.Id]);
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the host is not a submittable podcast-service or streaming URL, URL membership lookup returns unrecognised without querying writes.")]
    public async Task unrecognised_host_returns_unrecognised_kind()
    {
        // Arrange
        var url = new Uri($"https://example.com/{_fixture.CreateGuid():N}");
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(new UrlMembershipLookupResult(
            false,
            UrlMembershipLookupKinds.Unrecognised));
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When the host is a lookalike of a podcast-service domain, URL membership lookup returns unrecognised and does not run a catalog membership query.")]
    public async Task lookalike_spotify_host_returns_unrecognised()
    {
        // Arrange
        var url = new Uri($"https://open.spotify.com.example.test/episode/{_fixture.CreateSpotifyId()}");
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(new UrlMembershipLookupResult(
            false,
            UrlMembershipLookupKinds.Unrecognised));
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When submit content types are off, a Netflix URL stored only on a Film stays unknown " +
        "and membership does not query the Film container, because persist is still Podcast + Episode.")]
    public async Task flag_off_does_not_return_a_film_match()
    {
        // Arrange
        var url = NetflixWatchUrl();
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse();
        result.ContentKind.Should().BeNull();
        result.ParentName.Should().BeNull();
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
        _mocker.GetMock<IFilmRepository>().Verify(
            x => x.GetBy(It.IsAny<Expression<Func<Film, bool>>>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When submit content types are on, URL membership lookup returns a Film with no parent name " +
        "when that URL is stored on a Film, and it does not treat the film as a podcast.")]
    public async Task flag_on_returns_a_known_film_without_a_parent()
    {
        // Arrange
        var url = NetflixWatchUrl();
        var service = StreamingServiceWire.ToKey(StreamingService.Netflix);
        var film = new Film(_fixture.CreateTitle())
        {
            Services = new Dictionary<string, ServiceLink>
            {
                [service] = new() { Url = url }
            }
        };
        _mocker.Use(Options.Create(new SubmitContentTypesOptions { Enabled = true }));
        _mocker.GetMock<IFilmRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .ReturnsAsync((Expression<Func<Film, bool>> predicate) =>
                predicate.Compile()(film) ? film : null);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.ContentKind.Should().Be(SubmitClassification.Film);
        result.ParentName.Should().BeNull();
        result.PodcastId.Should().BeNull();
        result.PodcastName.Should().BeNull();
        result.Service.Should().Be(service);
        result.Kind.Should().Be(UrlMembershipLookupKinds.Streaming);
    }

    [Fact(DisplayName =
        "When submit content types are on and a Spotify URL is already on one series, " +
        "membership returns contentKind Episode and the series name as parentName.")]
    public async Task flag_on_known_spotify_url_returns_episode_kind()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast);
        var url = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify)!;
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        _mocker.Use(Options.Create(new SubmitContentTypesOptions { Enabled = true }));
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.ParentName.Should().Be(podcast.Name);
        result.PodcastName.Should().Be(podcast.Name);
    }

    [Fact(DisplayName =
        "When submit content types are on, a BBC /news/ URL is reported as a NewsReport " +
        "and is not treated as a known podcast, because news has no Sounds or iPlayer catalogue row.")]
    public async Task flag_on_bbc_news_url_is_a_news_report_hint()
    {
        // Arrange
        var url = new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}");
        _mocker.Use(Options.Create(new SubmitContentTypesOptions { Enabled = true }));
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse();
        result.Kind.Should().Be(UrlMembershipLookupKinds.Unrecognised);
        result.ContentKind.Should().Be(SubmitClassification.NewsReport);
        result.PodcastId.Should().BeNull();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When submit content types are off, a BBC /news/ URL stays unrecognised and omits contentKind.")]
    public async Task flag_off_bbc_news_url_omits_content_kind()
    {
        // Arrange
        var url = new Uri($"https://www.bbc.co.uk/news/{_fixture.CreateGuid():N}");
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse();
        result.Kind.Should().Be(UrlMembershipLookupKinds.Unrecognised);
        result.ContentKind.Should().BeNull();
    }

    [Fact(DisplayName =
        "When submit content types are off, a Spotify URL already stored on a film is still a catalogue service-url owner with no parent, because the indexer must not re-add a moved episode while submit still writes podcasts.")]
    public async Task flag_off_spotify_url_on_a_film_is_a_catalogue_owner()
    {
        // Arrange
        var url = SpotifyServiceUrl();
        var film = new Film(_fixture.CreateTitle())
        {
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.Spotify] = new() { Url = url }
            }
        };
        _mocker.GetMock<IFilmRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .ReturnsAsync((Expression<Func<Film, bool>> predicate) =>
                predicate.Compile()(film) ? film : null);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var owner = await sut.FindCatalogueServiceUrlOwner(url, CancellationToken.None);

        // Assert
        owner.Should().NotBeNull();
        owner!.ContentKind.Should().Be(SubmitClassification.Film);
        owner.ParentName.Should().BeNull();
        _mocker.GetMock<ITvShowEpisodeRepository>().Verify(
            x => x.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When submit content types are off, a YouTube URL already stored on a TV episode is a catalogue service-url owner named for that show, because a moved TV item must not be indexed again as a podcast episode.")]
    public async Task flag_off_youtube_url_on_a_tv_episode_is_a_catalogue_owner()
    {
        // Arrange
        var url = YouTubeServiceUrl();
        var showName = _fixture.CreateTitle();
        var tvEpisode = new TvShowEpisode(_fixture.CreateTitle())
        {
            TvShowName = showName,
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = url }
            }
        };
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync((Expression<Func<TvShowEpisode, bool>> predicate) =>
                predicate.Compile()(tvEpisode) ? tvEpisode : null);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var owner = await sut.FindCatalogueServiceUrlOwner(url, CancellationToken.None);

        // Assert
        owner.Should().NotBeNull();
        owner!.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        owner.ParentName.Should().Be(showName);
    }

    [Fact(DisplayName =
        "When submit content types are off, an Apple URL already stored on a news report is a catalogue service-url owner named for that organisation, because a moved news item must not be indexed again as a podcast episode.")]
    public async Task flag_off_apple_url_on_a_news_report_is_a_catalogue_owner()
    {
        // Arrange
        var url = AppleServiceUrl();
        var organisationName = _fixture.CreateTitle();
        var report = new NewsReport(_fixture.CreateTitle())
        {
            NewsOrganisationName = organisationName,
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.Apple] = new() { Url = url }
            }
        };
        _mocker.GetMock<INewsReportRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .ReturnsAsync((Expression<Func<NewsReport, bool>> predicate) =>
                predicate.Compile()(report) ? report : null);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var owner = await sut.FindCatalogueServiceUrlOwner(url, CancellationToken.None);

        // Assert
        owner.Should().NotBeNull();
        owner!.ContentKind.Should().Be(SubmitClassification.NewsReport);
        owner.ParentName.Should().Be(organisationName);
    }

    [Fact(DisplayName =
        "When submit content types are off, URL membership lookup does not query films for a Spotify URL, because submit membership stays podcast-only until the flag is on.")]
    public async Task flag_off_lookup_does_not_query_films_for_a_spotify_url()
    {
        // Arrange
        var url = SpotifyServiceUrl();
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse();
        result.ContentKind.Should().BeNull();
        _mocker.GetMock<IFilmRepository>().Verify(
            x => x.GetBy(It.IsAny<Expression<Func<Film, bool>>>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "A streaming URL is not a catalogue service-url owner, because the indexer ownership check only covers YouTube, Spotify, and Apple.")]
    public async Task streaming_url_is_not_a_catalogue_service_url_owner()
    {
        // Arrange
        var url = NetflixWatchUrl();
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var owner = await sut.FindCatalogueServiceUrlOwner(url, CancellationToken.None);

        // Assert
        owner.Should().BeNull();
        _mocker.GetMock<IFilmRepository>().Verify(
            x => x.GetBy(It.IsAny<Expression<Func<Film, bool>>>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When submit content types are on and a youtu.be link points at a video a film holds as a trailer, URL membership lookup reports it as part of that film, because the trailer's episode row was deleted on attach and must not come back as a new podcast episode.")]
    public async Task flag_on_youtube_short_link_to_a_film_trailer_returns_the_film()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var film = FilmWithTrailer(new Uri($"https://www.youtube.com/watch?v={videoId}"));
        _mocker.Use(Options.Create(new SubmitContentTypesOptions { Enabled = true }));
        SetupFilms(film);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(new Uri($"https://youtu.be/{videoId}"), CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.ContentKind.Should().Be(SubmitClassification.Film);
        result.ParentName.Should().BeNull();
        result.PodcastId.Should().BeNull();
        result.PodcastName.Should().BeNull();
        result.Kind.Should().Be(UrlMembershipLookupKinds.PodcastService);
        result.Service.Should().BeNull();
        _episodes.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When submit content types are on and a Spotify episode URL is a film's trailer URL, URL membership lookup reports Film with no parent name.")]
    public async Task flag_on_spotify_film_trailer_url_returns_the_film()
    {
        // Arrange
        var url = SpotifyServiceUrl();
        var film = FilmWithTrailer(url);
        _mocker.Use(Options.Create(new SubmitContentTypesOptions { Enabled = true }));
        SetupFilms(film);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.ContentKind.Should().Be(SubmitClassification.Film);
        result.ParentName.Should().BeNull();
        result.Kind.Should().Be(UrlMembershipLookupKinds.PodcastService);
    }

    [Fact(DisplayName =
        "When submit content types are on and a YouTube URL is neither a stored episode nor a film trailer, URL membership lookup leaves it unknown, because another video on the same film must not match.")]
    public async Task flag_on_youtube_url_for_a_different_video_is_not_a_film_trailer()
    {
        // Arrange
        var film = FilmWithTrailer(new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}"));
        _mocker.Use(Options.Create(new SubmitContentTypesOptions { Enabled = true }));
        SetupFilms(film);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(
            new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}"),
            CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse();
        result.ContentKind.Should().BeNull();
        result.Kind.Should().Be(UrlMembershipLookupKinds.PodcastService);
    }

    [Fact(DisplayName =
        "When submit content types are on and a YouTube URL is still stored on a podcast episode, URL membership lookup reports that episode and does not query films.")]
    public async Task flag_on_youtube_url_on_a_podcast_episode_does_not_query_films()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var url = EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube)!;
        _podcasts.Seed(podcast);
        _episodes.Seed(episode);
        _mocker.Use(Options.Create(new SubmitContentTypesOptions { Enabled = true }));
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.ContentKind.Should().Be(SubmitClassification.Episode);
        result.PodcastId.Should().Be(podcast.Id);
        _mocker.GetMock<IFilmRepository>().Verify(
            x => x.GetBy(It.IsAny<Expression<Func<Film, bool>>>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When submit content types are off, a YouTube URL a film holds as a trailer is still a catalogue service-url owner with no parent, because the indexer must not re-add the episode that was attached as the trailer.")]
    public async Task flag_off_youtube_film_trailer_url_is_a_catalogue_owner()
    {
        // Arrange
        var url = YouTubeServiceUrl();
        var film = FilmWithTrailer(url);
        SetupFilms(film);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var owner = await sut.FindCatalogueServiceUrlOwner(url, CancellationToken.None);

        // Assert
        owner.Should().NotBeNull();
        owner!.ContentKind.Should().Be(SubmitClassification.Film);
        owner.ParentName.Should().BeNull();
    }

    [Theory(DisplayName =
        "When submit content types are on and an Apple episode URL (cleaned, or with a storefront segment and the &uo=4 tracking suffix) is a film's trailer URL, URL membership lookup reports Film.")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task flag_on_apple_film_trailer_url_returns_the_film(bool withTrackingSuffix)
    {
        // Arrange
        // Stored Apple links are cleaned: no storefront segment and no &uo=4 suffix.
        var path = $"/podcast/synthetic-show/id{_fixture.CreateAppleId()}?i={_fixture.CreateAppleId()}";
        var stored = new Uri($"https://podcasts.apple.com{path}");
        var pasted = withTrackingSuffix ? new Uri($"https://podcasts.apple.com/us{path}&uo=4") : stored;
        _mocker.Use(Options.Create(new SubmitContentTypesOptions { Enabled = true }));
        SetupFilms(FilmWithTrailer(stored));
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(pasted, CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.ContentKind.Should().Be(SubmitClassification.Film);
        result.ParentName.Should().BeNull();
    }

    [Fact(DisplayName =
        "When submit content types are on and a youtu.be link points at a video stored as a TV episode's YouTube link, URL membership lookup reports TvShowEpisode with the show name, because the service-link check uses the stored URL form too.")]
    public async Task flag_on_youtube_short_link_on_a_tv_episode_returns_the_show()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var showName = _fixture.CreateTitle();
        var tvEpisode = new TvShowEpisode(_fixture.CreateTitle())
        {
            TvShowName = showName,
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = new Uri($"https://www.youtube.com/watch?v={videoId}") }
            }
        };
        _mocker.Use(Options.Create(new SubmitContentTypesOptions { Enabled = true }));
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync((Expression<Func<TvShowEpisode, bool>> predicate) =>
                predicate.Compile()(tvEpisode) ? tvEpisode : null);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(new Uri($"https://youtu.be/{videoId}"), CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.ContentKind.Should().Be(SubmitClassification.TvShowEpisode);
        result.ParentName.Should().Be(showName);
        result.Kind.Should().Be(UrlMembershipLookupKinds.PodcastService);
    }

    [Fact(DisplayName =
        "When submit content types are on and a YouTube URL with extra query parameters is a news report's YouTube link, URL membership lookup reports NewsReport with the organisation name.")]
    public async Task flag_on_youtube_url_with_extra_params_on_a_news_report_returns_the_organisation()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var organisationName = _fixture.CreateTitle();
        var report = new NewsReport(_fixture.CreateTitle())
        {
            NewsOrganisationName = organisationName,
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = new Uri($"https://www.youtube.com/watch?v={videoId}") }
            }
        };
        _mocker.Use(Options.Create(new SubmitContentTypesOptions { Enabled = true }));
        _mocker.GetMock<INewsReportRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .ReturnsAsync((Expression<Func<NewsReport, bool>> predicate) =>
                predicate.Compile()(report) ? report : null);
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(
            new Uri($"https://www.youtube.com/watch?v={videoId}&si={_fixture.CreateYouTubeId()}"),
            CancellationToken.None);

        // Assert
        result.Known.Should().BeTrue();
        result.ContentKind.Should().Be(SubmitClassification.NewsReport);
        result.ParentName.Should().Be(organisationName);
    }

    [Fact(DisplayName =
        "When submit content types are off, URL membership lookup of a film trailer's YouTube URL stays unknown and does not query films, because submit membership stays podcast-only until the flag is on.")]
    public async Task flag_off_lookup_of_a_film_trailer_url_does_not_query_films()
    {
        // Arrange
        var url = YouTubeServiceUrl();
        SetupFilms(FilmWithTrailer(url));
        var sut = _mocker.CreateInstance<UrlMembershipLookup>();

        // Act
        var result = await sut.Lookup(url, CancellationToken.None);

        // Assert
        result.Known.Should().BeFalse();
        result.ContentKind.Should().BeNull();
        _mocker.GetMock<IFilmRepository>().Verify(
            x => x.GetBy(It.IsAny<Expression<Func<Film, bool>>>()),
            Times.Never);
    }

    private Film FilmWithTrailer(Uri trailerUrl) =>
        new(_fixture.CreateTitle())
        {
            Trailers =
            [
                new FilmTrailer
                {
                    Id = _fixture.CreateGuid(),
                    Title = _fixture.CreateTitle(),
                    Url = trailerUrl
                }
            ]
        };

    private void SetupFilms(Film film) =>
        _mocker.GetMock<IFilmRepository>()
            .Setup(x => x.GetBy(It.IsAny<Expression<Func<Film, bool>>>()))
            .ReturnsAsync((Expression<Func<Film, bool>> predicate) =>
                predicate.Compile()(film) ? film : null);

    private Uri SpotifyServiceUrl() =>
        EpisodeServicePresence.TryGetUrl(
            _fixture.CreateStoredEpisodeWithSpotifyOnly(_fixture.CreatePodcast()),
            ServiceKeys.Spotify)!;

    private Uri YouTubeServiceUrl() =>
        EpisodeServicePresence.TryGetUrl(
            _fixture.CreateYouTubeCatalogueEpisode(),
            ServiceKeys.YouTube)!;

    private Uri AppleServiceUrl() =>
        EpisodeServicePresence.TryGetUrl(
            _fixture.CreateAppleCatalogueEpisode(),
            ServiceKeys.Apple)!;

    private Uri NetflixWatchUrl() =>
        new($"https://www.netflix.com/watch/{_fixture.CreateAppleId()}");

    private Uri BbcSoundsUrl() =>
        new($"https://www.bbc.co.uk/sounds/play/{_fixture.CreateYouTubeId()}");

    private Uri BbcIplayerUrl() =>
        new($"https://www.bbc.co.uk/iplayer/episode/{_fixture.CreateYouTubeId()}");

    private static void SeedBbcSoundsLookup(Episode episode, Uri soundsUrl)
    {
        episode.Services = new Dictionary<string, ServiceLink>(StringComparer.Ordinal)
        {
            [StreamingServiceWire.ToKey(StreamingService.BbcIplayer)] = new(),
            [StreamingServiceWire.ToKey(StreamingService.BbcSounds)] = new() { Url = soundsUrl }
        };
    }

    private static void SeedBbcIplayerLookup(Episode episode, Uri iplayerUrl)
    {
        episode.Services = new Dictionary<string, ServiceLink>(StringComparer.Ordinal)
        {
            [StreamingServiceWire.ToKey(StreamingService.BbcIplayer)] = new() { Url = iplayerUrl },
            [StreamingServiceWire.ToKey(StreamingService.BbcSounds)] = new()
        };
    }
}
