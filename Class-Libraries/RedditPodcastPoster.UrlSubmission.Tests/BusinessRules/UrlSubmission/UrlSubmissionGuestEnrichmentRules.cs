using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedditPodcastPoster.Common.Podcasts;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.People;
using RedditPodcastPoster.People;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.Subjects.Enrichers;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Enrichers;
using RedditPodcastPoster.UrlSubmission.Factories;
using RedditPodcastPoster.UrlSubmission.Matching;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

/// <summary>
/// Guest enrichment on URL/discovery submit: create path enriches; existing empty guests enrich;
/// existing guests are preserved.
/// </summary>
public class UrlSubmissionGuestEnrichmentRules
{
    private readonly DomainTestFixture _fixture = new();

    private static PersonMatch CreatePersonMatch(string name) =>
        new(
            new PersonMatchPerson(Guid.NewGuid(), name, null, null),
            [new PersonMatchResult(name, 1)]);

    [Fact(DisplayName =
        "When a new episode is created on an existing podcast, guest enrichment unions high-confidence guests onto the episode.")]
    public async Task new_episode_create_path_enriches_guests()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var created = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
        created.PodcastId = podcast.Id;
        created.Subjects = ["Cults"];

        var guestEnricher = new Mock<IEpisodeGuestEnricher>();
        guestEnricher
            .Setup(x => x.EnrichGuests(created, It.IsAny<GuestEnrichmentOptions?>()))
            .Callback<Episode, GuestEnrichmentOptions?>((episode, _) =>
            {
                episode.Guests = ["Ada Example"];
            })
            .ReturnsAsync(new EnrichGuestsResult([CreatePersonMatch("Ada Example")], []));

        var processor = CreateProcessor(
            matchingEpisode: null,
            createdEpisode: created,
            guestEnricher: guestEnricher.Object);

        var categorisedItem = CreateSpotifyCategorisedItem(podcast, matchingEpisode: null, podcastEpisodes: []);

        // Act
        var result = await processor.AddEpisodeToExistingPodcast(categorisedItem);

        // Assert
        result.EpisodeResult.Should().Be(SubmitResultState.Created);
        result.Episode!.Guests.Should().Equal("Ada Example");
        result.SubmitEpisodeDetails!.People.Should().ContainSingle()
            .Which.Person.Name.Should().Be("Ada Example");
        guestEnricher.Verify(
            x => x.EnrichGuests(created, It.IsAny<GuestEnrichmentOptions?>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When guest enrichment skips low-confidence matches, submit episode details include guest suggestions.")]
    public async Task new_episode_create_path_includes_guest_suggestions()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var created = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
        created.PodcastId = podcast.Id;
        created.Subjects = ["Cults"];

        var skipped = new PersonMatch(
            new PersonMatchPerson(Guid.NewGuid(), "Sam", null, null),
            [new PersonMatchResult("Sam", 1)]);

        var guestEnricher = new Mock<IEpisodeGuestEnricher>();
        guestEnricher
            .Setup(x => x.EnrichGuests(created, It.IsAny<GuestEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichGuestsResult([], [skipped]));

        var processor = CreateProcessor(
            matchingEpisode: null,
            createdEpisode: created,
            guestEnricher: guestEnricher.Object);

        var categorisedItem = CreateSpotifyCategorisedItem(podcast, matchingEpisode: null, podcastEpisodes: []);

        // Act
        var result = await processor.AddEpisodeToExistingPodcast(categorisedItem);

        // Assert
        result.SubmitEpisodeDetails!.People.Should().BeEmpty();
        result.SubmitEpisodeDetails!.GuestSuggestions.Should().ContainSingle()
            .Which.Person.Name.Should().Be("Sam");
    }

    [Fact(DisplayName =
        "When an existing episode has no Guests, URL submission enriches guests and reports Enriched.")]
    public async Task existing_episode_without_guests_is_enriched()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var existing = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
        existing.PodcastId = podcast.Id;
        existing.Subjects = ["Cults"];
        existing.Guests = null;

        var guestEnricher = new Mock<IEpisodeGuestEnricher>();
        guestEnricher
            .Setup(x => x.EnrichGuests(existing, It.IsAny<GuestEnrichmentOptions?>()))
            .Callback<Episode, GuestEnrichmentOptions?>((episode, _) =>
            {
                episode.Guests = ["Pat Placeholder"];
            })
            .ReturnsAsync(new EnrichGuestsResult([CreatePersonMatch("Pat Placeholder")], []));

        var processor = CreateProcessor(
            matchingEpisode: existing,
            createdEpisode: null,
            guestEnricher: guestEnricher.Object,
            appliedEpisodeResult: SubmitResultState.EpisodeAlreadyExists);

        var categorisedItem = CreateSpotifyCategorisedItem(
            podcast,
            matchingEpisode: existing,
            podcastEpisodes: [existing]);

        // Act
        var result = await processor.AddEpisodeToExistingPodcast(categorisedItem);

        // Assert
        result.EpisodeResult.Should().Be(SubmitResultState.Enriched);
        result.Episode!.Guests.Should().Equal("Pat Placeholder");
        result.SubmitEpisodeDetails!.People.Should().ContainSingle()
            .Which.Person.Name.Should().Be("Pat Placeholder");
        guestEnricher.Verify(
            x => x.EnrichGuests(existing, It.IsAny<GuestEnrichmentOptions?>()),
            Times.Once);
    }

    [Fact(DisplayName =
        "When an existing episode already has Guests, URL submission does not re-run guest enrichment.")]
    public async Task existing_episode_with_guests_skips_guest_enrichment()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        var existing = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));
        existing.PodcastId = podcast.Id;
        existing.Subjects = ["Cults"];
        existing.Guests = ["Existing Guest"];

        var guestEnricher = new Mock<IEpisodeGuestEnricher>();
        var processor = CreateProcessor(
            matchingEpisode: existing,
            createdEpisode: null,
            guestEnricher: guestEnricher.Object,
            appliedEpisodeResult: SubmitResultState.EpisodeAlreadyExists);

        var categorisedItem = CreateSpotifyCategorisedItem(
            podcast,
            matchingEpisode: existing,
            podcastEpisodes: [existing]);

        // Act
        var result = await processor.AddEpisodeToExistingPodcast(categorisedItem);

        // Assert
        result.EpisodeResult.Should().Be(SubmitResultState.EpisodeAlreadyExists);
        result.Episode!.Guests.Should().Equal("Existing Guest");
        guestEnricher.Verify(
            x => x.EnrichGuests(It.IsAny<Episode>(), It.IsAny<GuestEnrichmentOptions?>()),
            Times.Never);
    }

    [Fact(DisplayName =
        "When creating a brand-new podcast with episode, guest enrichment runs on the created episode.")]
    public async Task new_podcast_factory_enriches_guests()
    {
        // Arrange
        var created = _fixture.CreateSpotifyCatalogueEpisode(b => b
            .WithDuration(_fixture.CreateDuration()));

        var episodeFactory = new Mock<IEpisodeFactory>();
        episodeFactory
            .Setup(x => x.CreateEpisode(It.IsAny<CategorisedItem>()))
            .Returns(created);

        var podcastFactory = new Mock<IPodcastFactory>();
        podcastFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .ReturnsAsync(_fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId()));

        var subjectEnricher = new Mock<ISubjectEnricher>();
        subjectEnricher
            .Setup(x => x.EnrichSubjects(created, It.IsAny<SubjectEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichSubjectsResult(["Cults"], []));

        var guestEnricher = new Mock<IEpisodeGuestEnricher>();
        guestEnricher
            .Setup(x => x.EnrichGuests(created, It.IsAny<GuestEnrichmentOptions?>()))
            .Callback<Episode, GuestEnrichmentOptions?>((episode, _) =>
            {
                episode.Guests = ["Ada Example"];
            })
            .ReturnsAsync(new EnrichGuestsResult([CreatePersonMatch("Ada Example")], []));

        var factory = new PodcastAndEpisodeFactory(
            episodeFactory.Object,
            podcastFactory.Object,
            subjectEnricher.Object,
            guestEnricher.Object,
            NullLogger<PodcastAndEpisodeFactory>.Instance);

        var spotifyInput = _fixture.CreateResolvedSpotifyItemInput();
        var categorisedItem = new CategorisedItem(
            null,
            null,
            null,
            new CategorisedSpotifyItem(
                _fixture.CreateSpotifyId(),
                spotifyInput.EpisodeId,
                "Show",
                string.Empty,
                "Publisher",
                created.Title,
                created.Description,
                created.Release,
                created.Length,
                spotifyInput.Url!,
                false,
                null),
            null,
            null,
            null,
            Service.Spotify);

        // Act
        var response = await factory.CreatePodcastWithEpisode(categorisedItem);

        // Assert
        response.NewEpisode.Guests.Should().Equal("Ada Example");
        guestEnricher.Verify(
            x => x.EnrichGuests(created, It.IsAny<GuestEnrichmentOptions?>()),
            Times.Once);
    }

    private PodcastProcessor CreateProcessor(
        Episode? matchingEpisode,
        Episode? createdEpisode,
        IEpisodeGuestEnricher guestEnricher,
        SubmitResultState appliedEpisodeResult = SubmitResultState.None)
    {
        var episodeHelper = new Mock<IEpisodeHelper>();
        episodeHelper
            .Setup(x => x.IsMatchingEpisode(It.IsAny<Episode>(), It.IsAny<CategorisedItem>()))
            .Returns(true);

        var episodeEnricher = new Mock<IEpisodeEnricher>();
        episodeEnricher
            .Setup(x => x.ApplyResolvedPodcastServiceProperties(
                It.IsAny<Podcast>(),
                It.IsAny<CategorisedItem>(),
                It.IsAny<Episode?>()))
            .Returns(new ApplyResolvePodcastServicePropertiesResponse(
                SubmitResultState.None,
                appliedEpisodeResult,
                new SubmitEpisodeDetails(false, false, false)));

        var episodeFactory = new Mock<IEpisodeFactory>();
        if (createdEpisode != null)
        {
            episodeFactory
                .Setup(x => x.CreateEpisode(It.IsAny<CategorisedItem>()))
                .Returns(createdEpisode);
        }

        var subjectEnricher = new Mock<ISubjectEnricher>();
        subjectEnricher
            .Setup(x => x.EnrichSubjects(It.IsAny<Episode>(), It.IsAny<SubjectEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichSubjectsResult(["Cults"], []));

        return new PodcastProcessor(
            episodeHelper.Object,
            episodeEnricher.Object,
            episodeFactory.Object,
            subjectEnricher.Object,
            guestEnricher,
            NullLogger<PodcastProcessor>.Instance);
    }

    private CategorisedItem CreateSpotifyCategorisedItem(
        Podcast podcast,
        Episode? matchingEpisode,
        Episode[] podcastEpisodes)
    {
        var spotifyInput = _fixture.CreateResolvedSpotifyItemInput();
        var title = matchingEpisode?.Title ?? _fixture.CreateTitle();
        var description = matchingEpisode?.Description ?? _fixture.Create<string>();
        var release = matchingEpisode?.Release ?? DomainTestFixture.UtcDateDaysAgo(1);
        var length = matchingEpisode?.Length ?? _fixture.CreateDuration();

        return new CategorisedItem(
            podcast,
            podcastEpisodes,
            matchingEpisode,
            new CategorisedSpotifyItem(
                podcast.SpotifyId,
                matchingEpisode?.SpotifyId ?? spotifyInput.EpisodeId,
                podcast.Name,
                string.Empty,
                "Publisher",
                title,
                description,
                release,
                length,
                matchingEpisode?.Urls.Spotify ?? spotifyInput.Url!,
                false,
                null),
            null,
            null,
            null,
            Service.Spotify);
    }
}
