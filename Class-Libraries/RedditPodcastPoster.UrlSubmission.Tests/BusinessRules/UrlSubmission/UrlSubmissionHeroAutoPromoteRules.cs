using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Episodes.Logging;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.PodcastServices.Abstractions.Heroes;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Factories;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class UrlSubmissionHeroAutoPromoteRules
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName =
        "URL submit creates an in-week episode on an always-promote podcast and persists it: promoter receives that episode id, because curated URL adds must notify hero DO the same as indexing.")]
    public async Task created_in_week_on_flagged_podcast_promotes()
    {
        // Arrange
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcastRepository = new InMemoryPodcastRepository();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.AlwaysPromoteAsHero = true;
        podcastRepository.Seed(podcast);

        var episodeId = _fixture.CreateGuid();
        var newEpisode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration())
            .WithRelease(DomainTestFixture.UtcDateDaysAgo(1)));
        newEpisode.Id = episodeId;
        newEpisode.PodcastId = podcast.Id;
        newEpisode.Ignored = false;
        newEpisode.Removed = false;

        var podcastProcessor = new Mock<IPodcastProcessor>();
        podcastProcessor
            .Setup(x => x.AddEpisodeToExistingPodcast(It.IsAny<CategorisedItem>()))
            .ReturnsAsync(new SubmitResult(
                SubmitResultState.Created,
                SubmitResultState.None,
                Episode: newEpisode,
                Podcast: podcast));

        var heroEpisodePromoter = new Mock<IHeroEpisodePromoter>();
        IReadOnlyList<Guid>? promotedIds = null;
        heroEpisodePromoter
            .Setup(x => x.PromoteAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<Guid>, CancellationToken>((ids, _) => promotedIds = ids)
            .Returns(Task.CompletedTask);

        var processor = CreateProcessor(
            podcastProcessor.Object,
            podcastRepository,
            episodeRepository,
            heroEpisodePromoter: heroEpisodePromoter.Object);

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
        promotedIds.Should().Equal(episodeId);
    }

    [Fact(DisplayName =
        "Discovery curation creates an in-week episode on an always-promote podcast and persists it: promoter receives that episode id, because discovery submits share the URL-submit create path into hero DO.")]
    public async Task discovery_created_in_week_on_flagged_podcast_promotes()
    {
        // Arrange
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcastRepository = new InMemoryPodcastRepository();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.AlwaysPromoteAsHero = true;
        podcastRepository.Seed(podcast);

        var episodeId = _fixture.CreateGuid();
        var newEpisode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration())
            .WithRelease(DomainTestFixture.UtcDateDaysAgo(1)));
        newEpisode.Id = episodeId;
        newEpisode.PodcastId = podcast.Id;
        newEpisode.Ignored = false;
        newEpisode.Removed = false;

        var podcastProcessor = new Mock<IPodcastProcessor>();
        podcastProcessor
            .Setup(x => x.AddEpisodeToExistingPodcast(It.IsAny<CategorisedItem>()))
            .ReturnsAsync(new SubmitResult(
                SubmitResultState.Created,
                SubmitResultState.None,
                Episode: newEpisode,
                Podcast: podcast));

        var heroEpisodePromoter = new Mock<IHeroEpisodePromoter>();
        IReadOnlyList<Guid>? promotedIds = null;
        heroEpisodePromoter
            .Setup(x => x.PromoteAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<Guid>, CancellationToken>((ids, _) => promotedIds = ids)
            .Returns(Task.CompletedTask);

        var processor = CreateProcessor(
            podcastProcessor.Object,
            podcastRepository,
            episodeRepository,
            heroEpisodePromoter: heroEpisodePromoter.Object);

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
            new SubmitOptions(
                null,
                MatchOtherServices: true,
                PersistToDatabase: true,
                CreationSource: EpisodeCreationSource.Discovery));

        // Assert
        promotedIds.Should().Equal(episodeId);
    }

    [Fact(DisplayName =
        "URL submit creates an episode when always-promote is off: promoter is not called, because only flagged podcasts auto-append to heroes.")]
    public async Task created_when_flag_off_does_not_promote()
    {
        // Arrange
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcastRepository = new InMemoryPodcastRepository();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.AlwaysPromoteAsHero = false;
        podcastRepository.Seed(podcast);

        var newEpisode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration())
            .WithRelease(DomainTestFixture.UtcDateDaysAgo(1)));
        newEpisode.PodcastId = podcast.Id;
        newEpisode.Ignored = false;
        newEpisode.Removed = false;

        var podcastProcessor = new Mock<IPodcastProcessor>();
        podcastProcessor
            .Setup(x => x.AddEpisodeToExistingPodcast(It.IsAny<CategorisedItem>()))
            .ReturnsAsync(new SubmitResult(
                SubmitResultState.Created,
                SubmitResultState.None,
                Episode: newEpisode,
                Podcast: podcast));

        var heroEpisodePromoter = new Mock<IHeroEpisodePromoter>();
        var processor = CreateProcessor(
            podcastProcessor.Object,
            podcastRepository,
            episodeRepository,
            heroEpisodePromoter: heroEpisodePromoter.Object);

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
        heroEpisodePromoter.Verify(
            x => x.PromoteAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "URL submit creates an always-promote episode but PersistToDatabase is false: promoter is not called, because dry-run submits must not notify hero DO.")]
    public async Task created_without_persist_does_not_promote()
    {
        // Arrange
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcastRepository = new InMemoryPodcastRepository();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.AlwaysPromoteAsHero = true;
        podcastRepository.Seed(podcast);

        var newEpisode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration())
            .WithRelease(DomainTestFixture.UtcDateDaysAgo(1)));
        newEpisode.PodcastId = podcast.Id;
        newEpisode.Ignored = false;
        newEpisode.Removed = false;

        var podcastProcessor = new Mock<IPodcastProcessor>();
        podcastProcessor
            .Setup(x => x.AddEpisodeToExistingPodcast(It.IsAny<CategorisedItem>()))
            .ReturnsAsync(new SubmitResult(
                SubmitResultState.Created,
                SubmitResultState.None,
                Episode: newEpisode,
                Podcast: podcast));

        var heroEpisodePromoter = new Mock<IHeroEpisodePromoter>();
        var processor = CreateProcessor(
            podcastProcessor.Object,
            podcastRepository,
            episodeRepository,
            heroEpisodePromoter: heroEpisodePromoter.Object);

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
            new SubmitOptions(null, MatchOtherServices: true, PersistToDatabase: false));

        // Assert
        heroEpisodePromoter.Verify(
            x => x.PromoteAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "URL submit enriches an existing episode on an always-promote podcast: promoter is not called, because auto-promote is create-only and does not backfill.")]
    public async Task enriched_existing_episode_does_not_promote()
    {
        // Arrange
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcastRepository = new InMemoryPodcastRepository();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.AlwaysPromoteAsHero = true;
        podcastRepository.Seed(podcast);

        var enrichedEpisode = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration())
            .WithRelease(DomainTestFixture.UtcDateDaysAgo(1)));
        enrichedEpisode.PodcastId = podcast.Id;
        enrichedEpisode.Ignored = false;
        enrichedEpisode.Removed = false;

        var podcastProcessor = new Mock<IPodcastProcessor>();
        podcastProcessor
            .Setup(x => x.AddEpisodeToExistingPodcast(It.IsAny<CategorisedItem>()))
            .ReturnsAsync(new SubmitResult(
                SubmitResultState.Enriched,
                SubmitResultState.None,
                Episode: enrichedEpisode,
                Podcast: podcast));

        var heroEpisodePromoter = new Mock<IHeroEpisodePromoter>();
        var processor = CreateProcessor(
            podcastProcessor.Object,
            podcastRepository,
            episodeRepository,
            heroEpisodePromoter: heroEpisodePromoter.Object);

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
        heroEpisodePromoter.Verify(
            x => x.PromoteAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static CategorisedItemProcessor CreateProcessor(
        IPodcastProcessor podcastProcessor,
        InMemoryPodcastRepository podcastRepository,
        InMemoryEpisodeRepository episodeRepository,
        IPodcastAndEpisodeFactory? factory = null,
        IHeroEpisodePromoter? heroEpisodePromoter = null)
    {
        factory ??= new Mock<IPodcastAndEpisodeFactory>().Object;
        heroEpisodePromoter ??= new Mock<IHeroEpisodePromoter>().Object;
        return new CategorisedItemProcessor(
            podcastProcessor,
            podcastRepository,
            episodeRepository,
            factory,
            heroEpisodePromoter,
            NullLogger<CategorisedItem>.Instance);
    }
}
