using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.Adapters.Inputs;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.TestSupport;
using RedditPodcastPoster.Episodes.TestSupport.Assertions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Enrichers;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class UrlSubmissionEnrichmentRules
{
    private readonly DomainTestFixture _fixture = new();
    [Fact(DisplayName =
        "Submitting a URL for an episode that already exists enriches missing platform links on the stored episode.")]
    public void existing_episode_missing_platform_links_are_filled_from_resolved_items()
    {
        // Arrange
        var enricher = CreateEnricher();

        var podcast = _fixture.CreatePodcast();
        podcast.SpotifyId = _fixture.CreateSpotifyId();
        podcast.AppleId = _fixture.CreateAppleId();

        var storedDescription = _fixture.Create<string>();
        var publisher = _fixture.Create<string>();
        var resolvedSpotifyDescription = _fixture.Create<string>();
        var resolvedAppleDescription = _fixture.Create<string>();
        var resolvedYouTubeDescription = _fixture.Create<string>();
        var youTubeChannelId = _fixture.CreateYouTubeChannelId();
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithDuration(_fixture.CreateDuration())
            .WithDescription(storedDescription));
        var episode = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId)
            .WithDuration(youTubeInput.Duration)
            .WithDescription(youTubeInput.Description));
        episode.PodcastId = podcast.Id;
        EpisodeServicePresence.SetSpotifyIdentity(episode, null);
        EpisodeServicePresence.SetAppleIdentity(episode, null);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Spotify, null, null);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Apple, null, null);

        var spotifyInput = _fixture.CreateResolvedSpotifyItemInput();
        var appleInput = _fixture.CreateResolvedAppleItemInput();

        var categorisedItem = new CategorisedItem(
            podcast,
            [episode],
            episode,
            new CategorisedSpotifyItem(
                podcast.SpotifyId,
                spotifyInput.EpisodeId,
                podcast.Name,
                string.Empty,
                publisher,
                episode.Title,
                resolvedSpotifyDescription,
                episode.Release,
                episode.Length,
                spotifyInput.Url!,
                false,
                null),
            new CategorisedAppleItem(
                podcast.AppleId,
                appleInput.EpisodeId,
                podcast.Name,
                string.Empty,
                publisher,
                episode.Title,
                resolvedAppleDescription,
                episode.Release,
                episode.Length,
                appleInput.Url!,
                false,
                null),
            new CategorisedYouTubeItem(
                youTubeChannelId,
                youTubeInput.YouTubeId,
                podcast.Name,
                string.Empty,
                publisher,
                episode.Title,
                resolvedYouTubeDescription,
                episode.Release,
                episode.Length,
                youTubeInput.YouTubeUrl,
                false,
                null,
                null),
            null,
            Service.YouTube);

        var expected = EpisodeExpectation.From(episode)
            .WithSpotify(spotifyInput.EpisodeId, spotifyInput.Url!)
            .WithApple(appleInput.EpisodeId!.Value, appleInput.Url!)
            .WithYouTube(youTubeInput.YouTubeId, youTubeInput.YouTubeUrl);

        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert missing Spotify, Apple, and YouTube links are filled and the episode is marked enriched
        EpisodeExpectation.From(episode).Should().Be(expected);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.Enriched);
    }

    [Fact(DisplayName =
        "When podcast show metadata is enriched, the podcast receives missing show identifiers from resolved items.")]
    public void podcast_show_metadata_is_enriched_from_resolved_items()
    {
        // Arrange
        var enricher = CreateEnricher();

        var podcast = _fixture.CreatePodcast();
        podcast.SpotifyId = string.Empty;
        podcast.AppleId = null;
        podcast.YouTubeChannelId = string.Empty;

        var spotifyShowId = _fixture.CreateSpotifyId();
        var appleShowId = _fixture.CreateAppleId();
        var youTubeChannelId = _fixture.CreateYouTubeChannelId();
        var showName = _fixture.Create<string>();
        var episodeName = _fixture.CreateTitle();
        var resolvedDescription = _fixture.Create<string>();
        var publisher = _fixture.Create<string>();
        var playlistId = _fixture.Create<string>();
        var spotifyInput = _fixture.CreateResolvedSpotifyItemInput();
        var appleInput = _fixture.CreateResolvedAppleItemInput();
        var youTubeInput = _fixture.CreateResolvedYouTubeItemInput();

        var categorisedItem = new CategorisedItem(
            podcast,
            [],
            null,
            new CategorisedSpotifyItem(
                spotifyShowId,
                spotifyInput.EpisodeId,
                showName,
                string.Empty,
                publisher,
                episodeName,
                resolvedDescription,
                spotifyInput.Release,
                spotifyInput.Duration,
                spotifyInput.Url!,
                false,
                null),
            new CategorisedAppleItem(
                appleShowId,
                appleInput.EpisodeId,
                showName,
                string.Empty,
                publisher,
                episodeName,
                resolvedDescription,
                appleInput.Release,
                appleInput.Duration,
                appleInput.Url!,
                false,
                null),
            new CategorisedYouTubeItem(
                youTubeChannelId,
                youTubeInput.EpisodeId,
                showName,
                string.Empty,
                publisher,
                episodeName,
                resolvedDescription,
                youTubeInput.Release,
                youTubeInput.Duration,
                youTubeInput.Url!,
                false,
                null,
                playlistId),
            null,
            Service.Spotify);

        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            null);

        // Assert the podcast show metadata is enriched from the resolved items
        podcast.SpotifyId.Should().Be(spotifyShowId);
        podcast.AppleId.Should().Be(appleShowId);
        podcast.YouTubeChannelId.Should().Be(youTubeChannelId);
        response.PodcastResult.Should().Be(SubmitResultState.Enriched);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.None);
    }

    [Fact(DisplayName =
        "When an existing episode already has all resolved platform links, the result remains EpisodeAlreadyExists.")]
    public void unchanged_existing_episode_remains_episode_already_exists()
    {
        // Arrange
        var enricher = CreateEnricher();

        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.AppleId = _fixture.CreateAppleId();
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();

        var episode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration())
            .WithDescription(_fixture.Create<string>()));
        episode.PodcastId = podcast.Id;
        var appleInput = _fixture.CreateResolvedAppleItemInput();
        var youTubeInput = _fixture.CreateResolvedYouTubeItemInput();
        EpisodeServicePresence.SetAppleIdentity(episode, appleInput.EpisodeId);
        EpisodeServicePresence.SetYouTubeIdentity(episode, youTubeInput.EpisodeId);
        episode.ApplyListenUrl(ServiceKeys.Apple, appleInput.Url);
        episode.ApplyListenUrl(ServiceKeys.YouTube, youTubeInput.Url);

        var expected = EpisodeExpectation.From(episode);

        var publisher = _fixture.Create<string>();
        var categorisedItem = new CategorisedItem(
            podcast,
            [episode],
            episode,
            new CategorisedSpotifyItem(
                podcast.SpotifyId,
                EpisodeServicePresence.SpotifyEpisodeId(episode),
                podcast.Name,
                string.Empty,
                publisher,
                episode.Title,
                episode.Description,
                episode.Release,
                episode.Length,
                EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Spotify)!,
                false,
                null),
            new CategorisedAppleItem(
                podcast.AppleId,
                EpisodeServicePresence.AppleEpisodeId(episode),
                podcast.Name,
                string.Empty,
                publisher,
                episode.Title,
                episode.Description,
                episode.Release,
                episode.Length,
                EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Apple)!,
                false,
                null),
            new CategorisedYouTubeItem(
                podcast.YouTubeChannelId,
                EpisodeServicePresence.YouTubeEpisodeId(episode),
                podcast.Name,
                string.Empty,
                publisher,
                episode.Title,
                episode.Description,
                episode.Release,
                episode.Length,
                EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.YouTube)!,
                false,
                null,
                null),
            null,
            Service.Spotify);

        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert the episode remains unchanged and the result is EpisodeAlreadyExists
        EpisodeExpectation.From(episode).Should().Be(expected);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.EpisodeAlreadyExists);
        response.PodcastResult.Should().Be(SubmitResultState.None);
    }

    [Fact(DisplayName =
        "When an existing episode gains missing platform links, the result becomes Enriched instead of EpisodeAlreadyExists.")]
    public void enriched_existing_episode_reports_enriched_result_state()
    {
        // Arrange
        var enricher = CreateEnricher();

        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var storedDescription = _fixture.Create<string>();
        var youTubeInput = _fixture.CreateYouTubeCatalogueInput(b => b
            .WithDuration(_fixture.CreateDuration())
            .WithDescription(storedDescription));
        var episode = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeInput.YouTubeId)
            .WithDuration(youTubeInput.Duration)
            .WithDescription(youTubeInput.Description));
        episode.PodcastId = podcast.Id;
        EpisodeServicePresence.SetSpotifyIdentity(episode, null);
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Spotify, null, null);

        var spotifyInput = _fixture.CreateResolvedSpotifyItemInput();
        var expected = EpisodeExpectation.From(episode).WithSpotify(spotifyInput.EpisodeId, spotifyInput.Url!);
        var publisher = _fixture.Create<string>();
        var resolvedSpotifyDescription = _fixture.Create<string>();

        var categorisedItem = new CategorisedItem(
            podcast,
            [episode],
            episode,
            new CategorisedSpotifyItem(
                podcast.SpotifyId,
                spotifyInput.EpisodeId,
                podcast.Name,
                string.Empty,
                publisher,
                episode.Title,
                resolvedSpotifyDescription,
                episode.Release,
                episode.Length,
                spotifyInput.Url!,
                false,
                null),
            null,
            null,
            null,
            Service.Spotify);

        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert the episode is enriched and no longer reported as merely existing
        EpisodeExpectation.From(episode).Should().Be(expected);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.Enriched);
    }

    [Fact(DisplayName =
        "When an existing episode has midnight UTC release and a resolved Apple item carries publish time-of-day, " +
        "UrlSubmission enrichment backfills release time parity with the domain applicator.")]
    public void enrich_backfills_apple_release_from_midnight_stored_release()
    {
        // Arrange
        var enricher = CreateEnricher();
        var podcast = _fixture.CreatePodcast();
        podcast.AppleId = _fixture.CreateAppleId();
        var midnightRelease = DomainTestFixture.UtcDateDaysAgo(2);
        var appleRelease = DomainTestFixture.SameCalendarDateWithTime(
            midnightRelease,
            _fixture.CreateNonMidnightTimeOfDay());
        var appleInput = _fixture.CreateResolvedAppleItemInput(b => b.WithRelease(appleRelease));
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Release = midnightRelease;
                e.AppleId = appleInput.EpisodeId;
                e.Urls = new ServiceUrls { Apple = appleInput.Url };
            })
            .Create();
        var categorisedItem = CreateAppleOnlyCategorisedItem(podcast, episode, appleInput);

        // Act
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert
        episode.Release.Should().Be(appleRelease);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.Enriched);
    }

    [Fact(DisplayName =
        "When an existing episode has midnight UTC release and a resolved YouTube item carries publish time-of-day, " +
        "UrlSubmission enrichment backfills release time parity with the domain applicator.")]
    public void enrich_backfills_youtube_release_from_midnight_stored_release()
    {
        // Arrange
        var enricher = CreateEnricher();
        var podcast = _fixture.CreatePodcast();
        var midnightRelease = DomainTestFixture.UtcDateDaysAgo(3);
        var youTubeRelease = DomainTestFixture.SameCalendarDateWithTime(
            midnightRelease,
            _fixture.CreateNonMidnightTimeOfDay());
        var youTubeInput = _fixture.CreateResolvedYouTubeItemInput(b => b.WithRelease(youTubeRelease));
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Release = midnightRelease;
                e.YouTubeId = youTubeInput.EpisodeId;
                e.Urls = new ServiceUrls { YouTube = youTubeInput.Url };
            })
            .Create();
        var categorisedItem = CreateYouTubeOnlyCategorisedItem(podcast, episode, youTubeInput);

        // Act
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert
        episode.Release.Should().Be(youTubeRelease);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.Enriched);
    }

    [Fact(DisplayName =
        "When an existing episode has a truncated Apple description ending in ellipsis, " +
        "UrlSubmission enrichment extends the description parity with the domain applicator.")]
    public void enrich_extends_truncated_description_from_apple_resolved_item()
    {
        // Arrange
        const string truncatedDescription = "Short Apple preview...";
        const string fullDescription =
            "Short Apple preview with the complete episode summary and additional details.";
        var enricher = CreateEnricher(descriptionHelper =>
        {
            descriptionHelper
                .Setup(x => x.CollapseDescription(fullDescription))
                .Returns(fullDescription);
        });
        var podcast = _fixture.CreatePodcast();
        podcast.AppleId = _fixture.CreateAppleId();
        var appleInput = _fixture.CreateResolvedAppleItemInput(b => b.WithDescription(fullDescription));
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Description = truncatedDescription;
                e.AppleId = appleInput.EpisodeId;
                e.Urls = new ServiceUrls { Apple = appleInput.Url };
            })
            .Create();
        var categorisedItem = CreateAppleOnlyCategorisedItem(podcast, episode, appleInput);

        // Act
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert
        episode.Description.Should().Be(fullDescription);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.Enriched);
    }

    [Fact(DisplayName =
        "When an existing episode has a truncated Spotify description ending in ellipsis, " +
        "UrlSubmission enrichment extends the description parity with the domain applicator.")]
    public void enrich_extends_truncated_description_from_spotify_resolved_item()
    {
        // Arrange
        const string truncatedDescription = "Short Spotify preview...";
        const string fullDescription =
            "Short Spotify preview with the complete episode summary and additional details.";
        var enricher = CreateEnricher(descriptionHelper =>
        {
            descriptionHelper
                .Setup(x => x.CollapseDescription(fullDescription))
                .Returns(fullDescription);
        });
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var spotifyInput = _fixture.CreateResolvedSpotifyItemInput(b => b.WithDescription(fullDescription));
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Description = truncatedDescription;
                e.SpotifyId = spotifyInput.EpisodeId;
                e.Urls = new ServiceUrls { Spotify = spotifyInput.Url };
            })
            .Create();
        var categorisedItem = CreateSpotifyOnlyCategorisedItem(podcast, episode, spotifyInput);

        // Act
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert
        episode.Description.Should().Be(fullDescription);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.Enriched);
    }

    [Fact(DisplayName =
        "When an existing episode has a truncated YouTube description ending in ellipsis, " +
        "UrlSubmission enrichment extends the description parity with the domain applicator.")]
    public void enrich_extends_truncated_description_from_youtube_resolved_item()
    {
        // Arrange
        const string truncatedDescription = "Short YouTube preview...";
        const string fullDescription =
            "Short YouTube preview with the complete episode summary and additional details.";
        var enricher = CreateEnricher(descriptionHelper =>
        {
            descriptionHelper
                .Setup(x => x.CollapseDescription(fullDescription))
                .Returns(fullDescription);
        });
        var podcast = _fixture.CreatePodcast();
        var youTubeInput = _fixture.CreateResolvedYouTubeItemInput(b => b.WithDescription(fullDescription));
        var episode = _fixture.BuildEpisode()
            .WithPodcast(podcast)
            .Customize(e =>
            {
                e.Description = truncatedDescription;
                e.YouTubeId = youTubeInput.EpisodeId;
                e.Urls = new ServiceUrls { YouTube = youTubeInput.Url };
            })
            .Create();
        var categorisedItem = CreateYouTubeOnlyCategorisedItem(podcast, episode, youTubeInput);

        // Act
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert
        episode.Description.Should().Be(fullDescription);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.Enriched);
    }

    [Fact(DisplayName =
        "When an existing episode is missing a BBC link and a resolved non-podcast BBC item is present, " +
        "UrlSubmission enrichment fills the BBC URL on the stored episode.")]
    public void enrich_fills_missing_bbc_url_from_non_podcast_item()
    {
        // Arrange
        var enricher = CreateEnricher();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e => e.Urls = new ServiceUrls());
        var bbcUrl = _fixture.Create<Uri>();
        var nonPodcastItem = new ResolvedNonPodcastServiceItem(
            NonPodcastService.BBC,
            Url: bbcUrl,
            Title: episode.Title,
            Description: episode.Description);
        var categorisedItem = CreateNonPodcastOnlyCategorisedItem(podcast, episode, nonPodcastItem);

        // Act
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert
        episode.Urls.BBC.Should().Be(bbcUrl);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.Enriched);
        response.SubmitEpisodeDetails.BBC.Should().BeTrue();
        response.SubmitEpisodeDetails.ExtraServiceKeys.Should().Contain(ServiceKeys.BbcSounds);
    }

    [Fact(DisplayName =
        "When an existing episode is missing an Internet Archive link and a resolved non-podcast item is present, " +
        "UrlSubmission enrichment fills the Internet Archive URL on the stored episode.")]
    public void enrich_fills_missing_internet_archive_url_from_non_podcast_item()
    {
        // Arrange
        var enricher = CreateEnricher();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e => e.Urls = new ServiceUrls());
        var internetArchiveUrl = _fixture.Create<Uri>();
        var nonPodcastItem = new ResolvedNonPodcastServiceItem(
            NonPodcastService.InternetArchive,
            Url: internetArchiveUrl,
            Title: episode.Title,
            Description: episode.Description);
        var categorisedItem = CreateNonPodcastOnlyCategorisedItem(podcast, episode, nonPodcastItem);

        // Act
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert
        episode.Urls.InternetArchive.Should().Be(internetArchiveUrl);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.Enriched);
        response.SubmitEpisodeDetails.InternetArchive.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When a non-podcast resolved item carries artwork, UrlSubmission enrichment stores the image on that catalog service only — not leftover Images.Other.")]
    public void enrich_stores_non_podcast_image_on_bbc_catalog_and_leftover_other()
    {
        // Arrange
        var enricher = CreateEnricher();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e => e.Images = new EpisodeImages());
        var image = _fixture.Create<Uri>();
        var bbcUrl = _fixture.Create<Uri>();
        var nonPodcastItem = new ResolvedNonPodcastServiceItem(
            NonPodcastService.BBC,
            Url: bbcUrl,
            Title: episode.Title,
            Description: episode.Description,
            Image: image);
        var categorisedItem = CreateNonPodcastOnlyCategorisedItem(podcast, episode, nonPodcastItem);
        var bbcKey = ServiceCatalog.TryResolveKey(bbcUrl) ?? ServiceKeys.BbcSounds;

        // Act
        enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert
        EpisodeServicePresence.TryGetImage(episode, bbcKey).Should().Be(image);
        EpisodeServicePresence.ToEpisodeImages(episode)?.Other.Should().BeNull();
        EpisodeServicePresence.TryGetImage(episode, ServiceKeys.YouTube).Should().BeNull();
        EpisodeServicePresence.TryGetImage(episode, ServiceKeys.InternetArchive).Should().BeNull();
    }

    [Fact(DisplayName =
        "When a Vimeo resolved item carries artwork, UrlSubmission enrichment stores the image on the vimeo catalog key, " +
        "not leftover Images.Other and not the Internet Archive slot.")]
    public void enrich_stores_vimeo_image_on_vimeo_catalog_key()
    {
        // Arrange
        var enricher = CreateEnricher();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e => e.Images = new EpisodeImages());
        var image = _fixture.Create<Uri>();
        var vimeoUrl = new Uri($"https://vimeo.com/{_fixture.CreateAppleId()}");
        var nonPodcastItem = new ResolvedNonPodcastServiceItem(
            NonPodcastService.Vimeo,
            Url: vimeoUrl,
            Title: episode.Title,
            Description: episode.Description,
            Image: image);
        var categorisedItem = CreateNonPodcastOnlyCategorisedItem(podcast, episode, nonPodcastItem);

        // Act
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert
        EpisodeServicePresence.TryGetImage(episode, ServiceKeys.Vimeo).Should().Be(image);
        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Vimeo).Should().Be(vimeoUrl);
        EpisodeServicePresence.TryGetImage(episode, ServiceKeys.InternetArchive).Should().BeNull();
        EpisodeServicePresence.ToEpisodeImages(episode)?.Other.Should().BeNull();
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.Enriched);
        response.SubmitEpisodeDetails.Vimeo.Should().BeTrue();
        response.SubmitEpisodeDetails.ExtraServiceKeys.Should().Contain(ServiceKeys.Vimeo);
    }

    [Fact(DisplayName =
        "When an existing episode is missing a Netflix link and a resolved Netflix item is present, " +
        "UrlSubmission enrichment fills the Netflix catalog URL and sets the Netflix submit flag.")]
    public void enrich_fills_missing_netflix_url_from_non_podcast_item()
    {
        // Arrange
        var enricher = CreateEnricher();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e => e.Urls = new ServiceUrls());
        var netflixUrl = new Uri($"https://www.netflix.com/title/{_fixture.CreateAppleId()}");
        var nonPodcastItem = new ResolvedNonPodcastServiceItem(
            NonPodcastService.Netflix,
            Url: netflixUrl,
            Title: episode.Title,
            Description: episode.Description);
        var categorisedItem = CreateNonPodcastOnlyCategorisedItem(podcast, episode, nonPodcastItem);

        // Act
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert
        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Netflix).Should().Be(netflixUrl);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.Enriched);
        response.SubmitEpisodeDetails.Netflix.Should().BeTrue();
        response.SubmitEpisodeDetails.BBC.Should().BeFalse();
        response.SubmitEpisodeDetails.InternetArchive.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When an existing episode is missing a Prime Video link and a resolved Prime item is present, " +
        "UrlSubmission enrichment fills the amazonPrime catalog URL and sets the Amazon Prime submit flag.")]
    public void enrich_fills_missing_prime_url_from_non_podcast_item()
    {
        // Arrange
        var enricher = CreateEnricher();
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(podcast, e => e.Urls = new ServiceUrls());
        var primeUrl = new Uri($"https://www.primevideo.com/detail/{_fixture.CreateYouTubeId()}");
        var nonPodcastItem = new ResolvedNonPodcastServiceItem(
            NonPodcastService.AmazonPrime,
            Url: primeUrl,
            Title: episode.Title,
            Description: episode.Description);
        var categorisedItem = CreateNonPodcastOnlyCategorisedItem(podcast, episode, nonPodcastItem);

        // Act
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert
        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.AmazonPrime).Should().Be(primeUrl);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.Enriched);
        response.SubmitEpisodeDetails.AmazonPrime.Should().BeTrue();
        response.SubmitEpisodeDetails.ExtraServiceKeys.Should().Contain(ServiceKeys.AmazonPrime);
    }

    [Fact(DisplayName =
        "When an existing episode has midnight UTC release and a resolved non-podcast item carries publish time-of-day, " +
        "UrlSubmission enrichment backfills release on the stored episode.")]
    public void enrich_backfills_non_podcast_release_from_midnight_stored_release()
    {
        // Arrange
        var enricher = CreateEnricher();
        var podcast = _fixture.CreatePodcast();
        var midnightRelease = DomainTestFixture.UtcDateDaysAgo(2);
        var timedRelease = DomainTestFixture.SameCalendarDateWithTime(
            midnightRelease,
            _fixture.CreateNonMidnightTimeOfDay());
        var episode = _fixture.CreateStoredEpisode(
            podcast,
            e => e.Release = midnightRelease);
        var nonPodcastItem = new ResolvedNonPodcastServiceItem(
            NonPodcastService.BBC,
            Url: _fixture.Create<Uri>(),
            Title: episode.Title,
            Description: episode.Description,
            Release: timedRelease);
        var categorisedItem = CreateNonPodcastOnlyCategorisedItem(podcast, episode, nonPodcastItem);

        // Act
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert
        episode.Release.Should().Be(timedRelease);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.Enriched);
    }

    [Fact(DisplayName =
        "When an existing episode already has a BBC URL, " +
        "UrlSubmission enrichment does not replace the stored BBC link.")]
    public void enrich_skips_bbc_url_when_episode_already_has_bbc_link()
    {
        // Arrange
        var enricher = CreateEnricher();
        var podcast = _fixture.CreatePodcast();
        var existingBbcUrl = _fixture.Create<Uri>();
        var episode = _fixture.CreateStoredEpisode(
            podcast,
            e => e.Urls = new ServiceUrls { BBC = existingBbcUrl });
        var nonPodcastItem = new ResolvedNonPodcastServiceItem(
            NonPodcastService.BBC,
            Url: _fixture.Create<Uri>(),
            Title: episode.Title,
            Description: episode.Description);
        var categorisedItem = CreateNonPodcastOnlyCategorisedItem(podcast, episode, nonPodcastItem);

        // Act
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert
        episode.Urls.BBC.Should().Be(existingBbcUrl);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.EpisodeAlreadyExists);
        response.SubmitEpisodeDetails.BBC.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When an existing episode already has an Internet Archive URL, " +
        "UrlSubmission enrichment does not replace the stored link.")]
    public void enrich_skips_internet_archive_url_when_episode_already_has_link()
    {
        // Arrange
        var enricher = CreateEnricher();
        var podcast = _fixture.CreatePodcast();
        var existingUrl = _fixture.Create<Uri>();
        var episode = _fixture.CreateStoredEpisode(
            podcast,
            e => e.Urls = new ServiceUrls { InternetArchive = existingUrl });
        var nonPodcastItem = new ResolvedNonPodcastServiceItem(
            NonPodcastService.InternetArchive,
            Url: _fixture.Create<Uri>(),
            Title: episode.Title,
            Description: episode.Description);
        var categorisedItem = CreateNonPodcastOnlyCategorisedItem(podcast, episode, nonPodcastItem);

        // Act
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert
        episode.Urls.InternetArchive.Should().Be(existingUrl);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.EpisodeAlreadyExists);
        response.SubmitEpisodeDetails.InternetArchive.Should().BeFalse();
    }

    [Fact(DisplayName =
        "When a stored episode description is complete and a resolved non-podcast item offers longer text, " +
        "UrlSubmission enrichment does not replace the stored description.")]
    public void enrich_does_not_extend_non_podcast_description_when_stored_description_is_complete()
    {
        // Arrange
        var enricher = CreateEnricher();
        var podcast = _fixture.CreatePodcast();
        const string completeDescription = "A complete episode description without truncation.";
        var episode = _fixture.CreateStoredEpisode(
            podcast,
            e =>
            {
                e.Description = completeDescription;
                e.Urls = new ServiceUrls { BBC = _fixture.Create<Uri>() };
            });
        var nonPodcastItem = new ResolvedNonPodcastServiceItem(
            NonPodcastService.BBC,
            Url: _fixture.Create<Uri>(),
            Title: episode.Title,
            Description: "A much longer resolved description that would replace a truncated stored value.");
        var categorisedItem = CreateNonPodcastOnlyCategorisedItem(podcast, episode, nonPodcastItem);

        // Act
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert
        episode.Description.Should().Be(completeDescription);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.EpisodeAlreadyExists);
    }

    [Fact(DisplayName =
        "When an existing episode already has a platform identifier but is missing the platform URL, " +
        "UrlSubmission enrichment fills the missing URL via the domain applicator.")]
    public void enrich_fills_missing_platform_url_when_platform_id_already_present()
    {
        // Arrange
        var enricher = CreateEnricher();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var spotifyInput = _fixture.CreateResolvedSpotifyItemInput();
        var episode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration())
            .WithDescription(_fixture.Create<string>()));
        episode.PodcastId = podcast.Id;
        EpisodeServicePresence.SetSpotifyIdentity(episode, spotifyInput.EpisodeId);
        if (episode.Services is { } services &&
            services.TryGetValue(ServiceKeys.Spotify, out var spotifyLink))
        {
            spotifyLink.Url = null;
        }

        EpisodeServicePresence.Upsert(episode, ServiceKeys.Spotify, null, null);
        var categorisedItem = CreateSpotifyOnlyCategorisedItem(podcast, episode, spotifyInput);

        // Act
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert
        episode.Urls.Spotify.Should().Be(spotifyInput.Url);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.Enriched);
        response.SubmitEpisodeDetails.Spotify.Should().BeTrue();
    }

    [Fact(DisplayName =
        "When an existing episode has a truncated non-podcast description ending in ellipsis, " +
        "UrlSubmission enrichment extends the description from the resolved item.")]
    public void enrich_extends_truncated_non_podcast_description()
    {
        // Arrange
        const string truncatedDescription = "Short BBC preview...";
        const string fullDescription =
            "Short BBC preview with the complete episode summary and additional details.";
        var enricher = CreateEnricher(descriptionHelper =>
        {
            descriptionHelper
                .Setup(x => x.CollapseDescription(fullDescription))
                .Returns(fullDescription);
        });
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisode(
            podcast,
            e =>
            {
                e.Description = truncatedDescription;
                e.Urls = new ServiceUrls { BBC = _fixture.Create<Uri>() };
            });
        var nonPodcastItem = new ResolvedNonPodcastServiceItem(
            NonPodcastService.BBC,
            Url: _fixture.Create<Uri>(),
            Title: episode.Title,
            Description: fullDescription);
        var categorisedItem = CreateNonPodcastOnlyCategorisedItem(podcast, episode, nonPodcastItem);

        // Act
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert
        episode.Description.Should().Be(fullDescription);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.Enriched);
    }

    [Fact(DisplayName =
        "When the podcast already has Apple, Spotify, and YouTube show identifiers, " +
        "resolved platform items do not re-enrich podcast metadata.")]
    public void podcast_show_metadata_unchanged_when_platform_ids_already_present()
    {
        // Arrange
        var enricher = CreateEnricher();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.AppleId = _fixture.CreateAppleId();
        podcast.YouTubeChannelId = _fixture.CreateYouTubeChannelId();
        var existingSpotifyId = podcast.SpotifyId;
        var existingAppleId = podcast.AppleId;
        var existingYouTubeChannelId = podcast.YouTubeChannelId;

        var spotifyInput = _fixture.CreateResolvedSpotifyItemInput();
        var appleInput = _fixture.CreateResolvedAppleItemInput();
        var youTubeInput = _fixture.CreateResolvedYouTubeItemInput();
        var publisher = _fixture.Create<string>();
        var showName = _fixture.Create<string>();
        var episodeName = _fixture.CreateTitle();
        var resolvedDescription = _fixture.Create<string>();

        var categorisedItem = new CategorisedItem(
            podcast,
            [],
            null,
            new CategorisedSpotifyItem(
                _fixture.CreateSpotifyId(),
                spotifyInput.EpisodeId,
                showName,
                string.Empty,
                publisher,
                episodeName,
                resolvedDescription,
                spotifyInput.Release,
                spotifyInput.Duration,
                spotifyInput.Url!,
                false,
                null),
            new CategorisedAppleItem(
                _fixture.CreateAppleId(),
                appleInput.EpisodeId,
                showName,
                string.Empty,
                publisher,
                episodeName,
                resolvedDescription,
                appleInput.Release,
                appleInput.Duration,
                appleInput.Url!,
                false,
                null),
            new CategorisedYouTubeItem(
                _fixture.CreateYouTubeChannelId(),
                youTubeInput.EpisodeId,
                showName,
                string.Empty,
                publisher,
                episodeName,
                resolvedDescription,
                youTubeInput.Release,
                youTubeInput.Duration,
                youTubeInput.Url!,
                false,
                null,
                null),
            null,
            Service.Spotify);

        // Act
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            null);

        // Assert
        podcast.SpotifyId.Should().Be(existingSpotifyId);
        podcast.AppleId.Should().Be(existingAppleId);
        podcast.YouTubeChannelId.Should().Be(existingYouTubeChannelId);
        response.PodcastResult.Should().Be(SubmitResultState.None);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.None);
    }

    private static CategorisedItem CreateAppleOnlyCategorisedItem(
        Podcast podcast,
        Episode episode,
        ResolvedAppleItemInput appleInput)
    {
        var publisher = string.Empty;
        return new CategorisedItem(
            podcast,
            [episode],
            episode,
            null,
            new CategorisedAppleItem(
                podcast.AppleId,
                appleInput.EpisodeId,
                podcast.Name,
                string.Empty,
                publisher,
                episode.Title,
                appleInput.EpisodeDescription,
                appleInput.Release,
                appleInput.Duration,
                appleInput.Url!,
                false,
                appleInput.Image),
            null,
            null,
            Service.Apple);
    }

    private static CategorisedItem CreateYouTubeOnlyCategorisedItem(
        Podcast podcast,
        Episode episode,
        ResolvedYouTubeItemInput youTubeInput)
    {
        var publisher = string.Empty;
        return new CategorisedItem(
            podcast,
            [episode],
            episode,
            null,
            null,
            new CategorisedYouTubeItem(
                podcast.YouTubeChannelId,
                youTubeInput.EpisodeId,
                podcast.Name,
                string.Empty,
                publisher,
                episode.Title,
                youTubeInput.EpisodeDescription,
                youTubeInput.Release,
                youTubeInput.Duration,
                youTubeInput.Url!,
                false,
                youTubeInput.Image,
                null),
            null,
            Service.YouTube);
    }

    private static CategorisedItem CreateSpotifyOnlyCategorisedItem(
        Podcast podcast,
        Episode episode,
        ResolvedSpotifyItemInput spotifyInput)
    {
        var publisher = string.Empty;
        return new CategorisedItem(
            podcast,
            [episode],
            episode,
            new CategorisedSpotifyItem(
                podcast.SpotifyId,
                spotifyInput.EpisodeId,
                podcast.Name,
                string.Empty,
                publisher,
                episode.Title,
                spotifyInput.EpisodeDescription,
                spotifyInput.Release,
                spotifyInput.Duration,
                spotifyInput.Url!,
                false,
                spotifyInput.Image),
            null,
            null,
            null,
            Service.Spotify);
    }

    [Fact(DisplayName =
        "When refresh-meta is requested for an existing streaming episode, UrlSubmission enrichment overwrites " +
        "title, description, release, length, and service image even when the streaming URL is already present.")]
    public void refresh_meta_overwrites_non_podcast_fields_when_streaming_url_already_present()
    {
        // Arrange
        var enricher = CreateEnricher();
        var podcast = _fixture.CreatePodcast();
        var staleTitle = _fixture.CreateTitle();
        var freshTitle = _fixture.CreateTitle();
        var freshDescription = _fixture.Create<string>();
        var staleRelease = DateTime.MinValue;
        var freshRelease = DomainTestFixture.UtcAtTime(-10, new TimeSpan(21, 15, 0));
        var freshLength = _fixture.CreateDuration();
        var staleImage = new Uri($"https://app.10ft.itv.com/itvstatic/assets/images/brands/itvx/{_fixture.CreateYouTubeId()}.jpg");
        var freshImage = new Uri($"https://ovp.itv.com/v2/images/special/{_fixture.CreateYouTubeId()}/hero.jpg");
        var itvxUrl = new Uri($"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Title = staleTitle;
            e.Description = _fixture.Create<string>();
            e.Release = staleRelease;
            e.Length = TimeSpan.Zero;
        });
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Itvx, itvxUrl, staleImage);
        var nonPodcastItem = new ResolvedNonPodcastServiceItem(
            NonPodcastService.Itvx,
            Url: itvxUrl,
            Title: freshTitle,
            Description: freshDescription,
            Image: freshImage,
            Release: freshRelease,
            Duration: freshLength);
        var categorisedItem = CreateNonPodcastOnlyCategorisedItem(podcast, episode, nonPodcastItem);

        // Act
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode,
            refreshMeta: true);

        // Assert
        episode.Title.Should().Be(freshTitle);
        episode.Description.Should().Be(freshDescription);
        episode.Release.Should().Be(freshRelease);
        episode.Length.Should().Be(freshLength);
        EpisodeServicePresence.TryGetUrl(episode, ServiceKeys.Itvx).Should().Be(itvxUrl);
        EpisodeServicePresence.TryGetImage(episode, ServiceKeys.Itvx).Should().Be(freshImage);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.Enriched);
    }

    [Fact(DisplayName =
        "When refresh-meta is off and the streaming URL is already present, UrlSubmission enrichment leaves " +
        "existing title, length, and image unchanged.")]
    public void without_refresh_meta_existing_streaming_meta_is_left_alone()
    {
        // Arrange
        var enricher = CreateEnricher();
        var podcast = _fixture.CreatePodcast();
        var title = _fixture.CreateTitle();
        var description = _fixture.Create<string>();
        var staleImage = new Uri($"https://app.10ft.itv.com/itvstatic/assets/images/brands/itvx/{_fixture.CreateYouTubeId()}.jpg");
        var freshImage = new Uri($"https://ovp.itv.com/v2/images/special/{_fixture.CreateYouTubeId()}/hero.jpg");
        var itvxUrl = new Uri($"https://www.itv.com/watch/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}");
        var episode = _fixture.CreateStoredEpisode(podcast, e =>
        {
            e.Title = title;
            e.Description = description;
            e.Release = DateTime.MinValue;
            e.Length = TimeSpan.Zero;
        });
        EpisodeServicePresence.Upsert(episode, ServiceKeys.Itvx, itvxUrl, staleImage);
        var nonPodcastItem = new ResolvedNonPodcastServiceItem(
            NonPodcastService.Itvx,
            Url: itvxUrl,
            Title: _fixture.CreateTitle(),
            Description: _fixture.Create<string>(),
            Image: freshImage,
            Release: DomainTestFixture.UtcAtTime(-10, new TimeSpan(21, 15, 0)),
            Duration: _fixture.CreateDuration());
        var categorisedItem = CreateNonPodcastOnlyCategorisedItem(podcast, episode, nonPodcastItem);

        // Act
        enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode,
            refreshMeta: false);

        // Assert
        episode.Title.Should().Be(title);
        episode.Description.Should().Be(description);
        episode.Length.Should().Be(TimeSpan.Zero);
        EpisodeServicePresence.TryGetImage(episode, ServiceKeys.Itvx).Should().Be(staleImage);
    }

    private static CategorisedItem CreateNonPodcastOnlyCategorisedItem(
        Podcast podcast,
        Episode episode,
        ResolvedNonPodcastServiceItem nonPodcastItem) =>
        new(
            podcast,
            [episode],
            episode,
            null,
            null,
            null,
            nonPodcastItem,
            Service.Other);

    private static EpisodeEnricher CreateEnricher(
        Action<Mock<IDescriptionHelper>>? configureDescriptionHelper = null)
    {
        var mocker = new AutoMocker();
        var descriptionHelper = mocker.GetMock<IDescriptionHelper>();
        descriptionHelper
            .Setup(x => x.CollapseDescription(It.IsAny<string?>()))
            .Returns<string?>(description => description ?? string.Empty);
        descriptionHelper
            .Setup(x => x.EnrichMissingDescription(It.IsAny<CategorisedItem>()))
            .Returns("Resolved description");
        configureDescriptionHelper?.Invoke(descriptionHelper);
        mocker.Use(EpisodeDomainTestServices.CreateEnrichmentApplicator());
        mocker.Use(NullLogger<EpisodeEnricher>.Instance);
        return mocker.CreateInstance<EpisodeEnricher>();
    }
}
