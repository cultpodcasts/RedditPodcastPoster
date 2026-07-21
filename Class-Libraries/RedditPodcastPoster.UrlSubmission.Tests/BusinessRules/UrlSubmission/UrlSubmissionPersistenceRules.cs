using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Factories;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class UrlSubmissionPersistenceRules
{
    private readonly DomainTestFixture _fixture = new();
    [Fact(DisplayName =
        "Submitting a URL for an existing episode that is enriched saves the episode.")]
    public async Task existing_episode_enriched_saves_episode()
    {
        // Arrange
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcastRepository = new InMemoryPodcastRepository();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcastRepository.Seed(podcast);

        var enrichedEpisode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
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

        // Act
        await processor.ProcessCategorisedItem(
            categorisedItem,
            new SubmitOptions(null, MatchOtherServices: true, PersistToDatabase: true));

        // Assert
        episodeRepository.SavedEpisodes.Should().ContainSingle();
        episodeRepository.SavedEpisodes.Single().Id.Should().Be(enrichedEpisode.Id);
        podcastRepository.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When an episode already exists and no fields change, neither podcast nor episode is saved.")]
    public async Task existing_episode_unchanged_saves_nothing()
    {
        // Arrange
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcastRepository = new InMemoryPodcastRepository();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
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

        // Act
        await processor.ProcessCategorisedItem(
            categorisedItem,
            new SubmitOptions(null, MatchOtherServices: true, PersistToDatabase: true));

        // Assert
        episodeRepository.SavedEpisodes.Should().BeEmpty();
        podcastRepository.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When PersistToDatabase is false, no repository writes occur.")]
    public async Task persist_to_database_false_writes_nothing()
    {
        // Arrange
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcastRepository = new InMemoryPodcastRepository();
        var newPodcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var newEpisode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
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

        // Act
        await processor.ProcessCategorisedItem(
            categorisedItem,
            new SubmitOptions(null, MatchOtherServices: true, PersistToDatabase: false));

        // Assert
        episodeRepository.SavedEpisodes.Should().BeEmpty();
        podcastRepository.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When a new episode is added to an existing podcast, only the episode is saved " +
        "because the podcast row is unchanged unless show metadata was enriched.")]
    public async Task new_episode_on_existing_podcast_saves_episode_only()
    {
        // Arrange
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcastRepository = new InMemoryPodcastRepository();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcastRepository.Seed(podcast);

        var newEpisode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
        newEpisode.PodcastId = podcast.Id;

        var podcastProcessor = new Mock<IPodcastProcessor>();
        podcastProcessor
            .Setup(x => x.AddEpisodeToExistingPodcast(It.IsAny<CategorisedItem>()))
            .ReturnsAsync(new SubmitResult(
                SubmitResultState.Created,
                SubmitResultState.None,
                Episode: newEpisode));

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

        // Act
        await processor.ProcessCategorisedItem(
            categorisedItem,
            new SubmitOptions(null, MatchOtherServices: true, PersistToDatabase: true));

        // Assert
        episodeRepository.SavedEpisodes.Should().ContainSingle();
        episodeRepository.SavedEpisodes.Single().Id.Should().Be(newEpisode.Id);
        podcastRepository.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When podcast show metadata is enriched but the episode is unchanged, only the podcast is saved " +
        "because unchanged episodes must not trigger a redundant episode write.")]
    public async Task enriched_podcast_metadata_saves_podcast_only_when_episode_unchanged()
    {
        // Arrange
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcastRepository = new InMemoryPodcastRepository();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcastRepository.Seed(podcast);

        var podcastProcessor = new Mock<IPodcastProcessor>();
        podcastProcessor
            .Setup(x => x.AddEpisodeToExistingPodcast(It.IsAny<CategorisedItem>()))
            .ReturnsAsync(new SubmitResult(SubmitResultState.None, SubmitResultState.Enriched));

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

        // Act
        await processor.ProcessCategorisedItem(
            categorisedItem,
            new SubmitOptions(null, MatchOtherServices: true, PersistToDatabase: true));

        // Assert
        podcastRepository.SavedPodcasts.Should().ContainSingle();
        podcastRepository.SavedPodcasts.Single().Id.Should().Be(podcast.Id);
        episodeRepository.SavedEpisodes.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "New podcast submission saves both podcast and episode.")]
    public async Task new_podcast_submission_saves_podcast_and_episode()
    {
        // Arrange
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcastRepository = new InMemoryPodcastRepository();
        var newPodcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var newEpisode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
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

        // Act
        await processor.ProcessCategorisedItem(
            categorisedItem,
            new SubmitOptions(null, MatchOtherServices: true, PersistToDatabase: true));

        // Assert
        podcastRepository.SavedPodcasts.Should().ContainSingle();
        podcastRepository.SavedPodcasts.Single().Id.Should().Be(newPodcast.Id);
        episodeRepository.SavedEpisodes.Should().ContainSingle();
        episodeRepository.SavedEpisodes.Single().Id.Should().Be(newEpisode.Id);
    }

    [Fact(DisplayName =
        "When PersistToDatabase is false for an existing podcast submission, no repository writes occur.")]
    public async Task existing_podcast_persist_false_writes_nothing()
    {
        // Arrange
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcastRepository = new InMemoryPodcastRepository();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcastRepository.Seed(podcast);

        var enrichedEpisode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
        enrichedEpisode.PodcastId = podcast.Id;

        var podcastProcessor = new Mock<IPodcastProcessor>();
        podcastProcessor
            .Setup(x => x.AddEpisodeToExistingPodcast(It.IsAny<CategorisedItem>()))
            .ReturnsAsync(new SubmitResult(
                SubmitResultState.Enriched,
                SubmitResultState.Enriched,
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

        // Act
        await processor.ProcessCategorisedItem(
            categorisedItem,
            new SubmitOptions(null, MatchOtherServices: true, PersistToDatabase: false));

        // Assert
        episodeRepository.SavedEpisodes.Should().BeEmpty();
        podcastRepository.SavedPodcasts.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "When both podcast metadata and episode are enriched on an existing podcast submission, " +
        "both podcast and episode are saved.")]
    public async Task existing_podcast_podcast_and_episode_enriched_saves_both()
    {
        // Arrange
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcastRepository = new InMemoryPodcastRepository();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcastRepository.Seed(podcast);

        var enrichedEpisode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
        enrichedEpisode.PodcastId = podcast.Id;

        var podcastProcessor = new Mock<IPodcastProcessor>();
        podcastProcessor
            .Setup(x => x.AddEpisodeToExistingPodcast(It.IsAny<CategorisedItem>()))
            .ReturnsAsync(new SubmitResult(
                SubmitResultState.Enriched,
                SubmitResultState.Enriched,
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

        // Act
        await processor.ProcessCategorisedItem(
            categorisedItem,
            new SubmitOptions(null, MatchOtherServices: true, PersistToDatabase: true));

        // Assert
        podcastRepository.SavedPodcasts.Should().ContainSingle();
        podcastRepository.SavedPodcasts.Single().Id.Should().Be(podcast.Id);
        episodeRepository.SavedEpisodes.Should().ContainSingle();
        episodeRepository.SavedEpisodes.Single().Id.Should().Be(enrichedEpisode.Id);
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
