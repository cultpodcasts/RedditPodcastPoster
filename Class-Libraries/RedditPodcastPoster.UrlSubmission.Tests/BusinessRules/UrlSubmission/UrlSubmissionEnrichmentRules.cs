using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
    [Fact(DisplayName =
        "Submitting a URL for an episode that already exists enriches missing platform links on the stored episode.")]
    public void existing_episode_missing_platform_links_are_filled_from_resolved_items()
    {
        // Given an existing YouTube-only episode missing Spotify and Apple links
        var descriptionHelper = CreateDescriptionHelper();
        var enricher = new EpisodeEnricher(descriptionHelper, NullLogger<EpisodeEnricher>.Instance);

        var podcast = PodcastFixtures.Standard(Guid.Parse("21212121-2121-2121-2121-212121212121"));
        podcast.SpotifyId = "show-spotify-id";
        podcast.AppleId = 9876543210;

        var episode = EpisodeFixtures.FromYouTubeVideo(
            "yt-only-submit",
            "YouTube-only stored episode",
            new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromMinutes(45),
            "Stored description");
        episode.PodcastId = podcast.Id;
        episode.SpotifyId = string.Empty;
        episode.AppleId = null;
        episode.Urls.Spotify = null;
        episode.Urls.Apple = null;

        const string spotifyEpisodeId = "submit-spot-1";
        const long appleEpisodeId = 1112223334;
        const string youTubeEpisodeId = "yt-only-submit";
        var spotifyUrl = new Uri($"https://open.spotify.com/episode/{spotifyEpisodeId}");
        var appleUrl = new Uri($"https://podcasts.apple.com/us/podcast/episode/id{appleEpisodeId}");
        var youTubeUrl = new Uri($"https://www.youtube.com/watch?v={youTubeEpisodeId}");

        var categorisedItem = new CategorisedItem(
            podcast,
            [episode],
            episode,
            new ResolvedSpotifyItem(
                podcast.SpotifyId,
                spotifyEpisodeId,
                podcast.Name,
                string.Empty,
                "Publisher",
                episode.Title,
                "Resolved Spotify description",
                episode.Release,
                episode.Length,
                spotifyUrl,
                false,
                null),
            new ResolvedAppleItem(
                podcast.AppleId,
                appleEpisodeId,
                podcast.Name,
                string.Empty,
                "Publisher",
                episode.Title,
                "Resolved Apple description",
                episode.Release,
                episode.Length,
                appleUrl,
                false,
                null),
            new ResolvedYouTubeItem(
                "channel-submit",
                youTubeEpisodeId,
                podcast.Name,
                string.Empty,
                "Publisher",
                episode.Title,
                "Resolved YouTube description",
                episode.Release,
                episode.Length,
                youTubeUrl,
                false,
                null,
                null),
            null,
            Service.YouTube);

        var expected = EpisodeExpectation.From(episode)
            .WithSpotify(spotifyEpisodeId, spotifyUrl)
            .WithApple(appleEpisodeId, appleUrl)
            .WithYouTube(youTubeEpisodeId, youTubeUrl);

        // When resolved platform properties are applied to the existing episode
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Then missing Spotify, Apple, and YouTube links are filled and the episode is marked enriched
        EpisodeExpectation.From(episode).Should().Be(expected);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.Enriched);
    }

    [Fact(DisplayName =
        "When podcast show metadata is enriched, the podcast receives missing show identifiers from resolved items.")]
    public void podcast_show_metadata_is_enriched_from_resolved_items()
    {
        // Given an existing podcast missing Spotify, Apple, and YouTube show identifiers
        var descriptionHelper = CreateDescriptionHelper();
        var enricher = new EpisodeEnricher(descriptionHelper, NullLogger<EpisodeEnricher>.Instance);

        var podcast = PodcastFixtures.Standard(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        podcast.SpotifyId = string.Empty;
        podcast.AppleId = null;
        podcast.YouTubeChannelId = string.Empty;

        const string spotifyShowId = "show-submit-spotify";
        const long appleShowId = 5556667778;
        const string youTubeChannelId = "channel-submit-show";

        var categorisedItem = new CategorisedItem(
            podcast,
            [],
            null,
            new ResolvedSpotifyItem(
                spotifyShowId,
                "episode-spot-1",
                "Resolved show",
                string.Empty,
                "Publisher",
                "Resolved episode",
                "Resolved description",
                DateTime.UtcNow.AddDays(-1),
                TimeSpan.FromMinutes(45),
                new Uri("https://open.spotify.com/episode/episode-spot-1"),
                false,
                null),
            new ResolvedAppleItem(
                appleShowId,
                1231231234,
                "Resolved show",
                string.Empty,
                "Publisher",
                "Resolved episode",
                "Resolved description",
                DateTime.UtcNow.AddDays(-1),
                TimeSpan.FromMinutes(45),
                new Uri("https://podcasts.apple.com/us/podcast/episode/id1231231234"),
                false,
                null),
            new ResolvedYouTubeItem(
                youTubeChannelId,
                "yt-submit-1",
                "Resolved show",
                string.Empty,
                "Publisher",
                "Resolved episode",
                "Resolved description",
                DateTime.UtcNow.AddDays(-1),
                TimeSpan.FromMinutes(45),
                new Uri("https://www.youtube.com/watch?v=yt-submit-1"),
                false,
                null,
                "playlist-submit"),
            null,
            Service.Spotify);

        // When resolved platform properties are applied without a matching episode
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            null);

        // Then the podcast show metadata is enriched from the resolved items
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
        // Given an existing episode that already has Spotify, Apple, and YouTube links
        var descriptionHelper = CreateDescriptionHelper();
        var enricher = new EpisodeEnricher(descriptionHelper, NullLogger<EpisodeEnricher>.Instance);

        var podcast = PodcastFixtures.SpotifyPrimary("show-complete");
        podcast.AppleId = 4445556667;
        podcast.YouTubeChannelId = "channel-complete";

        var episode = EpisodeFixtures.FromSpotifyCatalogue(
            "complete-spot-1",
            "Complete episode",
            new Uri("https://open.spotify.com/episode/complete-spot-1"),
            new DateTime(2026, 5, 2, 0, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromMinutes(45),
            "Complete description");
        episode.PodcastId = podcast.Id;
        episode.AppleId = 8889990001;
        episode.YouTubeId = "complete-yt-1";
        episode.Urls.Apple = new Uri("https://podcasts.apple.com/us/podcast/episode/id8889990001");
        episode.Urls.YouTube = new Uri("https://www.youtube.com/watch?v=complete-yt-1");

        var expected = EpisodeExpectation.From(episode);

        var categorisedItem = new CategorisedItem(
            podcast,
            [episode],
            episode,
            new ResolvedSpotifyItem(
                podcast.SpotifyId,
                episode.SpotifyId,
                podcast.Name,
                string.Empty,
                "Publisher",
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
                "Publisher",
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
                "Publisher",
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

        // When resolved platform properties are applied without changing the episode
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Then the episode remains unchanged and the result is EpisodeAlreadyExists
        EpisodeExpectation.From(episode).Should().Be(expected);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.EpisodeAlreadyExists);
        response.PodcastResult.Should().Be(SubmitResultState.None);
    }

    [Fact(DisplayName =
        "When an existing episode gains missing platform links, the result becomes Enriched instead of EpisodeAlreadyExists.")]
    public void enriched_existing_episode_reports_enriched_result_state()
    {
        // Given an existing episode missing Spotify identity
        var descriptionHelper = CreateDescriptionHelper();
        var enricher = new EpisodeEnricher(descriptionHelper, NullLogger<EpisodeEnricher>.Instance);

        var podcast = PodcastFixtures.SpotifyPrimary("show-enrich-state");
        var episode = EpisodeFixtures.FromYouTubeVideo(
            "yt-enrich-state",
            "Episode awaiting Spotify",
            new DateTime(2026, 5, 3, 0, 0, 0, DateTimeKind.Utc),
            TimeSpan.FromMinutes(45),
            "Stored description");
        episode.PodcastId = podcast.Id;
        episode.SpotifyId = string.Empty;
        episode.Urls.Spotify = null;

        const string spotifyEpisodeId = "enrich-state-spot-1";
        var spotifyUrl = new Uri($"https://open.spotify.com/episode/{spotifyEpisodeId}");
        var expected = EpisodeExpectation.From(episode).WithSpotify(spotifyEpisodeId, spotifyUrl);

        var categorisedItem = new CategorisedItem(
            podcast,
            [episode],
            episode,
            new ResolvedSpotifyItem(
                podcast.SpotifyId,
                spotifyEpisodeId,
                podcast.Name,
                string.Empty,
                "Publisher",
                episode.Title,
                "Resolved Spotify description",
                episode.Release,
                episode.Length,
                spotifyUrl,
                false,
                null),
            null,
            null,
            null,
            Service.Spotify);

        // When resolved Spotify properties fill the missing link
        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Then the episode is enriched and no longer reported as merely existing
        EpisodeExpectation.From(episode).Should().Be(expected);
        response.AppliedEpisodeResult.Should().Be(SubmitResultState.Enriched);
    }

    private static IDescriptionHelper CreateDescriptionHelper()
    {
        var descriptionHelper = new Mock<IDescriptionHelper>();
        descriptionHelper
            .Setup(x => x.CollapseDescription(It.IsAny<string?>()))
            .Returns<string?>(description => description ?? string.Empty);
        descriptionHelper
            .Setup(x => x.EnrichMissingDescription(It.IsAny<CategorisedItem>()))
            .Returns("Resolved description");
        return descriptionHelper.Object;
    }
}
