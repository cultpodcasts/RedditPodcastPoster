using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Factories;
using RedditPodcastPoster.UrlSubmission.Models;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class UrlSubmissionPersistenceRules
{
    [Fact(DisplayName =
        "Submitting a URL for an existing episode that is enriched saves the episode.")]
    public async Task existing_episode_enriched_saves_episode()
    {
        // Given an existing podcast and an enriched submit result
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcastRepository = new InMemoryPodcastRepository();
        var podcast = PodcastFixtures.SpotifyPrimary("show-existing");
        podcastRepository.Seed(podcast);

        var enrichedEpisode = EpisodeFixtures.FromSpotifyCatalogue(
            "existing-spot-1",
            "Existing episode",
            new Uri("https://open.spotify.com/episode/existing-spot-1"),
            DateTime.UtcNow.AddDays(-3),
            TimeSpan.FromMinutes(45));
        enrichedEpisode.PodcastId = podcast.Id;

        var podcastProcessor = new Mock<IPodcastProcessor>();
        podcastProcessor
            .Setup(x => x.AddEpisodeToExistingPodcast(It.IsAny<CategorisedItem>()))
            .ReturnsAsync(new SubmitResult(
                SubmitResultState.Enriched,
                SubmitResultState.None,
                Episode: enrichedEpisode));

        var processor = CreateProcessor(
            podcastProcessor.Object,
            podcastRepository,
            episodeRepository);

        var categorisedItem = new CategorisedItem(
            podcast,
            [],
            enrichedEpisode,
            null,
            null,
            null,
            null,
            Service.Spotify);

        // When the URL is submitted with persistence enabled
        await processor.ProcessCategorisedItem(
            categorisedItem,
            new SubmitOptions(null, MatchOtherServices: true, PersistToDatabase: true));

        // Then only the episode is saved
        episodeRepository.SavedEpisodes.Should().ContainSingle();
        episodeRepository.SavedEpisodes.Single().Id.Should().Be(enrichedEpisode.Id);
        podcastRepository.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When an episode already exists and no fields change, neither podcast nor episode is saved.")]
    public async Task existing_episode_unchanged_saves_nothing()
    {
        // Given an existing podcast and an unchanged submit result
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcastRepository = new InMemoryPodcastRepository();
        var podcast = PodcastFixtures.SpotifyPrimary("show-unchanged");
        podcastRepository.Seed(podcast);

        var podcastProcessor = new Mock<IPodcastProcessor>();
        podcastProcessor
            .Setup(x => x.AddEpisodeToExistingPodcast(It.IsAny<CategorisedItem>()))
            .ReturnsAsync(new SubmitResult(SubmitResultState.None, SubmitResultState.None));

        var processor = CreateProcessor(
            podcastProcessor.Object,
            podcastRepository,
            episodeRepository);

        var categorisedItem = new CategorisedItem(
            podcast,
            [],
            null,
            null,
            null,
            null,
            null,
            Service.Spotify);

        // When the URL is submitted with persistence enabled
        await processor.ProcessCategorisedItem(
            categorisedItem,
            new SubmitOptions(null, MatchOtherServices: true, PersistToDatabase: true));

        // Then neither podcast nor episode is persisted
        episodeRepository.SavedEpisodes.Should().BeEmpty();
        podcastRepository.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When PersistToDatabase is false, no repository writes occur.")]
    public async Task persist_to_database_false_writes_nothing()
    {
        // Given a submit result that would normally persist both podcast and episode
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcastRepository = new InMemoryPodcastRepository();
        var newPodcast = PodcastFixtures.SpotifyPrimary("show-new-no-persist");
        var newEpisode = EpisodeFixtures.FromSpotifyCatalogue(
            "new-spot-1",
            "New episode",
            new Uri("https://open.spotify.com/episode/new-spot-1"),
            DateTime.UtcNow,
            TimeSpan.FromMinutes(45));
        newEpisode.PodcastId = newPodcast.Id;

        var factory = new Mock<IPodcastAndEpisodeFactory>();
        factory
            .Setup(x => x.CreatePodcastWithEpisode(It.IsAny<CategorisedItem>()))
            .ReturnsAsync(new CreatePodcastWithEpisodeResponse(
                newPodcast,
                newEpisode,
                new SubmitEpisodeDetails(false, false, false)));

        var processor = CreateProcessor(
            new Mock<IPodcastProcessor>().Object,
            podcastRepository,
            episodeRepository,
            factory.Object);

        var categorisedItem = new CategorisedItem(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            Service.Spotify);

        // When submission runs with PersistToDatabase disabled
        await processor.ProcessCategorisedItem(
            categorisedItem,
            new SubmitOptions(null, MatchOtherServices: true, PersistToDatabase: false));

        // Then no repository writes occur
        episodeRepository.SavedEpisodes.Should().BeEmpty();
        podcastRepository.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "New podcast submission saves both podcast and episode.")]
    public async Task new_podcast_submission_saves_podcast_and_episode()
    {
        // Given a categorised item for a brand-new podcast
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcastRepository = new InMemoryPodcastRepository();
        var newPodcast = PodcastFixtures.SpotifyPrimary("show-brand-new");
        var newEpisode = EpisodeFixtures.FromSpotifyCatalogue(
            "brand-new-spot-1",
            "Brand new episode",
            new Uri("https://open.spotify.com/episode/brand-new-spot-1"),
            DateTime.UtcNow,
            TimeSpan.FromMinutes(45));
        newEpisode.PodcastId = newPodcast.Id;

        var factory = new Mock<IPodcastAndEpisodeFactory>();
        factory
            .Setup(x => x.CreatePodcastWithEpisode(It.IsAny<CategorisedItem>()))
            .ReturnsAsync(new CreatePodcastWithEpisodeResponse(
                newPodcast,
                newEpisode,
                new SubmitEpisodeDetails(true, false, false)));

        var processor = CreateProcessor(
            new Mock<IPodcastProcessor>().Object,
            podcastRepository,
            episodeRepository,
            factory.Object);

        var categorisedItem = new CategorisedItem(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            Service.Spotify);

        // When the URL is submitted with persistence enabled
        await processor.ProcessCategorisedItem(
            categorisedItem,
            new SubmitOptions(null, MatchOtherServices: true, PersistToDatabase: true));

        // Then both podcast and episode are saved
        podcastRepository.SavedPodcasts.Should().ContainSingle();
        podcastRepository.SavedPodcasts.Single().Id.Should().Be(newPodcast.Id);
        episodeRepository.SavedEpisodes.Should().ContainSingle();
        episodeRepository.SavedEpisodes.Single().Id.Should().Be(newEpisode.Id);
    }

    private static CategorisedItemProcessor CreateProcessor(
        IPodcastProcessor podcastProcessor,
        InMemoryPodcastRepository podcastRepository,
        InMemoryEpisodeRepository episodeRepository,
        IPodcastAndEpisodeFactory? factory = null)
    {
        factory ??= new Mock<IPodcastAndEpisodeFactory>().Object;
        return new CategorisedItemProcessor(
            podcastProcessor,
            podcastRepository,
            episodeRepository,
            factory,
            NullLogger<CategorisedItem>.Instance);
    }
}
