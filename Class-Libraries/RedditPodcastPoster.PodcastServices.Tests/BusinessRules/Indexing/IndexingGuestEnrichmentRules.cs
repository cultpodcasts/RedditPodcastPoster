using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Indexing.Models;
using RedditPodcastPoster.Indexing.Services;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.People;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.People.Enrichers;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Updaters;
using RedditPodcastPoster.PodcastServices.Updaters;
using RedditPodcastPoster.Subjects.Enrichers;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Subjects.Models;

namespace RedditPodcastPoster.PodcastServices.Tests.BusinessRules.Indexing;

/// <summary>
/// After PodcastUpdater, Indexer enriches Guests on updated episodes and re-saves when guests are added.
/// </summary>
public class IndexingGuestEnrichmentRules
{
    private static readonly DateTime ReleasedSince = DomainTestFixture.UtcDateDaysAgo(400);
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryPodcastRepository _podcasts = new();
    private readonly InMemoryEpisodeRepository _episodes = new();
    private Episode? _added;

    public IndexingGuestEnrichmentRules()
    {
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use<IEpisodeRepository>(_episodes);
        _mocker.Use<IParentIndexingLockStore>(new InMemoryParentIndexingLockStore());
        _mocker.Use<TimeProvider>(TimeProvider.System);
        _mocker.Use(NullLogger<Indexer>.Instance);
        _mocker.Use(Options.Create(new PodcastUpdateBudgetOptions()));

        _mocker.GetMock<IPodcastUpdater>()
            .Setup(x => x.Update(It.IsAny<Podcast>(), false, It.IsAny<IndexingContext>()))
            .ReturnsAsync((Podcast podcast, bool _, IndexingContext _) =>
                new IndexPodcastResult(
                    podcast,
                    new EpisodeMergeResult([], _added is null ? [] : [_added], [], []),
                    new FilterResult([]),
                    new EnrichmentResults([]),
                    SpotifyBypassed: false,
                    YouTubeBypassed: false));

        _mocker.GetMock<ISubjectEnricher>()
            .Setup(x => x.EnrichSubjects(It.IsAny<Episode>(), It.IsAny<SubjectEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichSubjectsResult([], []));

        _mocker.GetMock<ISubjectEnrichmentOptionsFactory>()
            .Setup(x => x.CreateAsync(
                It.IsAny<Podcast>(),
                It.IsAny<Episode?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectEnrichmentOptions(null, null, null, string.Empty));
    }

    private static PersonMatch CreatePersonMatch(string name) =>
        new(
            new Person(name),
            [new PersonMatchResult(name, 1)]);

    [Fact(DisplayName =
        "When indexing adds an episode, guest enrichment unions guests and persists the episode.")]
    public async Task indexing_added_episode_enriches_guests()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.IndexAllEpisodes = true;
        _podcasts.Seed(podcast);

        _added = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
        _added.Id = _fixture.CreateGuid();
        _added.PodcastId = podcast.Id;
        _added.Guests = null;

        _mocker.GetMock<IEpisodeGuestEnricher>()
            .Setup(x => x.EnrichGuests(_added, It.IsAny<GuestEnrichmentOptions?>()))
            .Callback<Episode, GuestEnrichmentOptions?>((episode, _) =>
            {
                episode.Guests = ["Ada Example"];
            })
            .ReturnsAsync(new EnrichGuestsResult([CreatePersonMatch("Ada Example")], []));

        var indexer = _mocker.CreateInstance<Indexer>();

        // Act
        var response = await indexer.Index(
            podcast.Id,
            new IndexingContext(ReleasedSince));

        // Assert
        response.IndexStatus.Should().Be(IndexStatus.Performed);
        _mocker.GetMock<IEpisodeGuestEnricher>().Verify(
            x => x.EnrichGuests(_added, It.IsAny<GuestEnrichmentOptions?>()),
            Times.Once);
        _episodes.SavedEpisodes.Should().ContainSingle();
        var saved = _episodes.SavedEpisodes.Single();
        saved.Guests.Should().Equal("Ada Example");
    }

    [Fact(DisplayName =
        "When guest enrichment adds no guests and subjects are unchanged, indexing does not re-save the episode.")]
    public async Task indexing_skips_save_when_guests_and_subjects_unchanged()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        podcast.IndexAllEpisodes = true;
        _podcasts.Seed(podcast);

        _added = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
        _added.Id = _fixture.CreateGuid();
        _added.PodcastId = podcast.Id;
        _added.Guests = ["Already Linked"];

        _mocker.GetMock<IEpisodeGuestEnricher>()
            .Setup(x => x.EnrichGuests(_added, It.IsAny<GuestEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichGuestsResult([], []));

        var indexer = _mocker.CreateInstance<Indexer>();

        // Act
        await indexer.Index(podcast.Id, new IndexingContext(ReleasedSince));

        // Assert
        _mocker.GetMock<IEpisodeGuestEnricher>().Verify(
            x => x.EnrichGuests(_added, It.IsAny<GuestEnrichmentOptions?>()),
            Times.Once);
        _episodes.SavedEpisodes.Should().BeEmpty();
        _added.Guests.Should().Equal("Already Linked");
    }
}
