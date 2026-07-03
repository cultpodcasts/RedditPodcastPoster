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
    private readonly DomainTestFixture _fixture = new();
    [Fact(DisplayName =
        "Submitting a URL for an episode that already exists enriches missing platform links on the stored episode.")]
    public void existing_episode_missing_platform_links_are_filled_from_resolved_items()
    {
        // Arrange
        var descriptionHelper = CreateDescriptionHelper();
        var enricher = new EpisodeEnricher(descriptionHelper, NullLogger<EpisodeEnricher>.Instance);

        var podcast = _fixture.CreatePodcast(p => p.Id = Guid.Parse("21212121-2121-2121-2121-212121212121"));
        podcast.SpotifyId = "6oTbi9wKZ2czCvSwBKxxoH";
        podcast.AppleId = 9876543210123;

        const string youTubeEpisodeId = "9aBcDeFgHiJ";
        var episode = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeEpisodeId)
            .WithRelease(DomainTestFixture.UtcAtTime(-63, TimeSpan.FromHours(12)))
            .WithDuration(TimeSpan.FromMinutes(45))
            .WithDescription("Stored description"));
        episode.PodcastId = podcast.Id;
        episode.SpotifyId = string.Empty;
        episode.AppleId = null;
        episode.Urls.Spotify = null;
        episode.Urls.Apple = null;

        const string spotifyEpisodeId = "1UncRhHtmojlTq2mO0Gntz";
        const long appleEpisodeId = 1112223334445;
        var spotifyUrl = _fixture.DefaultSpotifyUrl(spotifyEpisodeId);
        var appleUrl = _fixture.DefaultAppleUrl(appleEpisodeId);
        var youTubeUrl = _fixture.DefaultYouTubeUrl(youTubeEpisodeId);

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
        var descriptionHelper = CreateDescriptionHelper();
        var enricher = new EpisodeEnricher(descriptionHelper, NullLogger<EpisodeEnricher>.Instance);

        var podcast = _fixture.CreatePodcast(p => p.Id = Guid.Parse("22222222-2222-2222-2222-222222222222"));
        podcast.SpotifyId = string.Empty;
        podcast.AppleId = null;
        podcast.YouTubeChannelId = string.Empty;

        const string spotifyShowId = "6oTbi9wKZ2czCvSwBKxxoH";
        const long appleShowId = 5556667778889;
        const string youTubeChannelId = "UCchannel123456789";
        const string resolvedSpotifyEpisodeId = "3vKvHj9mNoPqRsTuVwXyZ1";
        const long resolvedAppleEpisodeId = 1231231234567;
        const string resolvedYouTubeEpisodeId = "kLmNoPqRsTu";

        var categorisedItem = new CategorisedItem(
            podcast,
            [],
            null,
            new ResolvedSpotifyItem(
                spotifyShowId,
                resolvedSpotifyEpisodeId,
                "Resolved show",
                string.Empty,
                "Publisher",
                "Resolved episode",
                "Resolved description",
                DateTime.UtcNow.AddDays(-1),
                TimeSpan.FromMinutes(45),
                _fixture.DefaultSpotifyUrl(resolvedSpotifyEpisodeId),
                false,
                null),
            new ResolvedAppleItem(
                appleShowId,
                resolvedAppleEpisodeId,
                "Resolved show",
                string.Empty,
                "Publisher",
                "Resolved episode",
                "Resolved description",
                DateTime.UtcNow.AddDays(-1),
                TimeSpan.FromMinutes(45),
                _fixture.DefaultAppleUrl(resolvedAppleEpisodeId),
                false,
                null),
            new ResolvedYouTubeItem(
                youTubeChannelId,
                resolvedYouTubeEpisodeId,
                "Resolved show",
                string.Empty,
                "Publisher",
                "Resolved episode",
                "Resolved description",
                DateTime.UtcNow.AddDays(-1),
                TimeSpan.FromMinutes(45),
                _fixture.DefaultYouTubeUrl(resolvedYouTubeEpisodeId),
                false,
                null,
                "playlist-submit"),
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
        var descriptionHelper = CreateDescriptionHelper();
        var enricher = new EpisodeEnricher(descriptionHelper, NullLogger<EpisodeEnricher>.Instance);

        var podcast = _fixture.CreateSpotifyPrimaryPodcast("6oTbi9wKZ2czCvSwBKxxoH");
        podcast.AppleId = 4445556667778;
        podcast.YouTubeChannelId = "UCchannel123456789";

        const string spotifyEpisodeId = "1UncRhHtmojlTq2mO0Gntz";
        const long appleEpisodeId = 8889990001234;
        const string youTubeEpisodeId = "dQw4w9WgXcQ";
        var episode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithSpotifyId(spotifyEpisodeId)
            .WithRelease(DomainTestFixture.UtcDateDaysAgo(1))
            .WithDuration(TimeSpan.FromMinutes(45))
            .WithDescription("Complete description"));
        episode.PodcastId = podcast.Id;
        episode.AppleId = appleEpisodeId;
        episode.YouTubeId = youTubeEpisodeId;
        episode.Urls.Apple = _fixture.DefaultAppleUrl(appleEpisodeId);
        episode.Urls.YouTube = _fixture.DefaultYouTubeUrl(youTubeEpisodeId);

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
        var descriptionHelper = CreateDescriptionHelper();
        var enricher = new EpisodeEnricher(descriptionHelper, NullLogger<EpisodeEnricher>.Instance);

        var podcast = _fixture.CreateSpotifyPrimaryPodcast("6oTbi9wKZ2czCvSwBKxxoH");
        const string youTubeEpisodeId = "xYzAbCdEfGh";
        var episode = _fixture.CreateYouTubeCatalogueEpisode(b => b
            .WithYouTubeId(youTubeEpisodeId)
            .WithRelease(DomainTestFixture.UtcDateDaysAgo(2))
            .WithDuration(TimeSpan.FromMinutes(45))
            .WithDescription("Stored description"));
        episode.PodcastId = podcast.Id;
        episode.SpotifyId = string.Empty;
        episode.Urls.Spotify = null;

        const string spotifyEpisodeId = "5nT8vW2xY4zA6bC8dE0fG2";
        var spotifyUrl = _fixture.DefaultSpotifyUrl(spotifyEpisodeId);
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

        var response = enricher.ApplyResolvedPodcastServiceProperties(
            podcast,
            categorisedItem,
            episode);

        // Assert the episode is enriched and no longer reported as merely existing
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
