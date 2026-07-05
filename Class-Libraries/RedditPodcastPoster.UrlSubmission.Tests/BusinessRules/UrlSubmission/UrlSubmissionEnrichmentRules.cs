using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.Applying;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify.Models;
using RedditPodcastPoster.PodcastServices.YouTube.Models;
using RedditPodcastPoster.UrlSubmission;
using RedditPodcastPoster.UrlSubmission.Categorisation;

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
        episode.SpotifyId = string.Empty;
        episode.AppleId = null;
        episode.Urls.Spotify = null;
        episode.Urls.Apple = null;

        var spotifyInput = _fixture.CreateResolvedSpotifyItemInput();
        var appleInput = _fixture.CreateResolvedAppleItemInput();

        var categorisedItem = new CategorisedItem(
            podcast,
            [episode],
            episode,
            new ResolvedSpotifyItem(
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
            new ResolvedAppleItem(
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
            new ResolvedYouTubeItem(
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
            new ResolvedSpotifyItem(
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
            new ResolvedAppleItem(
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
            new ResolvedYouTubeItem(
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
        episode.AppleId = appleInput.EpisodeId;
        episode.YouTubeId = youTubeInput.EpisodeId;
        episode.Urls.Apple = appleInput.Url;
        episode.Urls.YouTube = youTubeInput.Url;

        var expected = EpisodeExpectation.From(episode);

        var publisher = _fixture.Create<string>();
        var categorisedItem = new CategorisedItem(
            podcast,
            [episode],
            episode,
            new ResolvedSpotifyItem(
                podcast.SpotifyId,
                episode.SpotifyId,
                podcast.Name,
                string.Empty,
                publisher,
                episode.Title,
                episode.Description,
                episode.Release,
                episode.Length,
                episode.Urls.Spotify!,
                false,
                null),
            new ResolvedAppleItem(
                podcast.AppleId,
                episode.AppleId,
                podcast.Name,
                string.Empty,
                publisher,
                episode.Title,
                episode.Description,
                episode.Release,
                episode.Length,
                episode.Urls.Apple!,
                false,
                null),
            new ResolvedYouTubeItem(
                podcast.YouTubeChannelId,
                episode.YouTubeId,
                podcast.Name,
                string.Empty,
                publisher,
                episode.Title,
                episode.Description,
                episode.Release,
                episode.Length,
                episode.Urls.YouTube!,
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
        episode.SpotifyId = string.Empty;
        episode.Urls.Spotify = null;

        var spotifyInput = _fixture.CreateResolvedSpotifyItemInput();
        var expected = EpisodeExpectation.From(episode).WithSpotify(spotifyInput.EpisodeId, spotifyInput.Url!);
        var publisher = _fixture.Create<string>();
        var resolvedSpotifyDescription = _fixture.Create<string>();

        var categorisedItem = new CategorisedItem(
            podcast,
            [episode],
            episode,
            new ResolvedSpotifyItem(
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

    private static EpisodeEnricher CreateEnricher()
    {
        var descriptionHelper = new Mock<IDescriptionHelper>();
        descriptionHelper
            .Setup(x => x.CollapseDescription(It.IsAny<string?>()))
            .Returns<string?>(description => description ?? string.Empty);
        descriptionHelper
            .Setup(x => x.EnrichMissingDescription(It.IsAny<CategorisedItem>()))
            .Returns("Resolved description");

        return new EpisodeEnricher(
            descriptionHelper.Object,
            new EpisodePlatformApplier(),
            NullLogger<EpisodeEnricher>.Instance);
    }
}
