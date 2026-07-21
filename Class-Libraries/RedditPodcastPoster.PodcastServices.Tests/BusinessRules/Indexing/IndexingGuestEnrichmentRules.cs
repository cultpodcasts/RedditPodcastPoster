using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using Moq;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Indexing.Models;
using RedditPodcastPoster.Indexing.Services;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.People.Enrichers;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.PodcastServices.Abstractions;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.PodcastServices.Clients;
using RedditPodcastPoster.Subjects.Enrichers;
using RedditPodcastPoster.Subjects.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// After PodcastUpdater, Indexer enriches Guests on updated episodes and re-saves when guests are added.
/// </summary>
public class IndexingGuestEnrichmentRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(400);
    private readonly DomainTestFixture _fixture = new();

    private static PersonMatch CreatePersonMatch(string name) =>
        new(
            new PersonMatchPerson(Guid.NewGuid(), name, null, null),
            [new PersonMatchResult(name, 1)]);

    [Fact(DisplayName =
        "When indexing adds an episode, guest enrichment unions guests and persists the episode.")]
    public async Task indexing_added_episode_enriches_guests()
    {
        // Arrange
        var podcastRepository = new InMemoryPodcastRepository();
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.IndexAllEpisodes = true;
        podcastRepository.Seed(podcast);

        var added = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
        added.Id = _fixture.CreateGuid();
        added.PodcastId = podcast.Id;
        added.Guests = null;

        var podcastUpdater = new Mock<IPodcastUpdater>();
        podcastUpdater
            .Setup(x => x.Update(It.IsAny<Podcast>(), false, It.IsAny<IndexingContext>()))
            .ReturnsAsync(new IndexPodcastResult(
                podcast,
                new EpisodeMergeResult([], [added], [], []),
                new FilterResult([]),
                new EnrichmentResults([]),
                SpotifyBypassed: false,
                YouTubeBypassed: false));

        var subjectEnricher = new Mock<ISubjectEnricher>();
        subjectEnricher
            .Setup(x => x.EnrichSubjects(added, It.IsAny<SubjectEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichSubjectsResult([], []));

        var guestEnricher = new Mock<IEpisodeGuestEnricher>();
        guestEnricher
            .Setup(x => x.EnrichGuests(added, It.IsAny<GuestEnrichmentOptions?>()))
            .Callback<Episode, GuestEnrichmentOptions?>((episode, _) =>
            {
                episode.Guests = ["Ada Example"];
            })
            .ReturnsAsync(new EnrichGuestsResult([CreatePersonMatch("Ada Example")], []));

        var indexer = new Indexer(
            podcastRepository,
            episodeRepository,
            podcastUpdater.Object,
            subjectEnricher.Object,
            guestEnricher.Object,
            NullLogger<Indexer>.Instance);

        // Act
        var response = await indexer.Index(
            podcast.Id,
            new IndexingContext(ReleasedSince));

        // Assert
        response.IndexStatus.Should().Be(IndexStatus.Performed);
        guestEnricher.Verify(
            x => x.EnrichGuests(added, It.IsAny<GuestEnrichmentOptions?>()),
            Times.Once);
        episodeRepository.SavedEpisodes.Should().ContainSingle();
        var saved = episodeRepository.SavedEpisodes.Single();
        saved.Guests.Should().Equal("Ada Example");
    }

    [Fact(DisplayName =
        "When guest enrichment adds no guests and subjects are unchanged, indexing does not re-save the episode.")]
    public async Task indexing_skips_save_when_guests_and_subjects_unchanged()
    {
        // Arrange
        var podcastRepository = new InMemoryPodcastRepository();
        var episodeRepository = new InMemoryEpisodeRepository();
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.IndexAllEpisodes = true;
        podcastRepository.Seed(podcast);

        var added = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
        added.Id = _fixture.CreateGuid();
        added.PodcastId = podcast.Id;
        added.Guests = ["Already Linked"];

        var podcastUpdater = new Mock<IPodcastUpdater>();
        podcastUpdater
            .Setup(x => x.Update(It.IsAny<Podcast>(), false, It.IsAny<IndexingContext>()))
            .ReturnsAsync(new IndexPodcastResult(
                podcast,
                new EpisodeMergeResult([], [added], [], []),
                new FilterResult([]),
                new EnrichmentResults([]),
                SpotifyBypassed: false,
                YouTubeBypassed: false));

        var subjectEnricher = new Mock<ISubjectEnricher>();
        subjectEnricher
            .Setup(x => x.EnrichSubjects(added, It.IsAny<SubjectEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichSubjectsResult([], []));

        var guestEnricher = new Mock<IEpisodeGuestEnricher>();
        guestEnricher
            .Setup(x => x.EnrichGuests(added, It.IsAny<GuestEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichGuestsResult([], []));

        var indexer = new Indexer(
            podcastRepository,
            episodeRepository,
            podcastUpdater.Object,
            subjectEnricher.Object,
            guestEnricher.Object,
            NullLogger<Indexer>.Instance);

        // Act
        await indexer.Index(podcast.Id, new IndexingContext(ReleasedSince));

        // Assert
        guestEnricher.Verify(
            x => x.EnrichGuests(added, It.IsAny<GuestEnrichmentOptions?>()),
            Times.Once);
        episodeRepository.SavedEpisodes.Should().BeEmpty();
        added.Guests.Should().Equal("Already Linked");
    }
}
