using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.People.Enrichers;
using RedditPodcastPoster.People.Models;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.Subjects.Enrichers;
using RedditPodcastPoster.Subjects.Factories;
using RedditPodcastPoster.Subjects.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Enrichers;
using RedditPodcastPoster.UrlSubmission.Factories;
using RedditPodcastPoster.UrlSubmission.Matching;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class NonPodcastPodcastProcessorRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private Episode _createdEpisode = null!;

    public NonPodcastPodcastProcessorRules()
    {
        _createdEpisode = _fixture.CreateStoredEpisode(_fixture.CreatePodcast());
        _createdEpisode.Subjects = [_fixture.Create<string>()];

        _mocker.GetMock<IEpisodeHelper>()
            .Setup(x => x.IsMatchingEpisode(It.IsAny<Episode>(), It.IsAny<CategorisedItem>()))
            .Returns(true);

        _mocker.GetMock<IEpisodeEnricher>()
            .Setup(x => x.ApplyResolvedPodcastServiceProperties(
                It.IsAny<Podcast>(),
                It.IsAny<CategorisedItem>(),
                It.IsAny<Episode?>()))
            .Returns(new ApplyResolvePodcastServicePropertiesResponse(
                SubmitResultState.None,
                SubmitResultState.Enriched,
                new SubmitEpisodeDetails(false, false, false)));

        _mocker.GetMock<IEpisodeFactory>()
            .Setup(x => x.CreateEpisode(It.IsAny<CategorisedItem>()))
            .Returns(() => _createdEpisode);

        _mocker.GetMock<ISubjectEnricher>()
            .Setup(x => x.EnrichSubjects(It.IsAny<Episode>(), It.IsAny<SubjectEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichSubjectsResult([_fixture.Create<string>()], []));

        _mocker.GetMock<ISubjectEnrichmentOptionsFactory>()
            .Setup(x => x.CreateAsync(
                It.IsAny<Podcast>(),
                It.IsAny<Episode?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectEnrichmentOptions(null, null, null, string.Empty));

        _mocker.GetMock<IEpisodeGuestEnricher>()
            .Setup(x => x.EnrichGuests(It.IsAny<Episode>(), It.IsAny<GuestEnrichmentOptions?>()))
            .ReturnsAsync(new EnrichGuestsResult([], []));
    }

    private PodcastProcessor Sut => _mocker.CreateInstance<PodcastProcessor>();

    [Fact(DisplayName =
        "When two stored episodes both match a Sounds submit, FuzzyMatcher uses the Sounds title " +
        "and ApplyResolvedPodcastServiceProperties is invoked with the closer episode.")]
    public async Task sounds_title_selects_closer_of_two_matching_episodes()
    {
        // Arrange
        var closerTitle = _fixture.CreateTitle();
        var fartherTitle = DistinctFrom(closerTitle);
        var podcast = _fixture.CreatePodcast();
        var farther = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, title: fartherTitle);
        var closer = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, title: closerTitle);
        var categorisedItem = SoundsSubmit(podcast, [farther, closer], closerTitle);

        // Act
        await Sut.AddEpisodeToExistingPodcast(categorisedItem);

        // Assert
        _mocker.GetMock<IEpisodeEnricher>().Verify(
            x => x.ApplyResolvedPodcastServiceProperties(
                podcast,
                categorisedItem,
                closer),
            Times.Once);
    }

    [Fact(DisplayName =
        "When two stored episodes match a Sounds submit but the Sounds title is blank, " +
        "matchingEpisode stays null so a new episode is created instead of First().")]
    public async Task blank_sounds_title_creates_instead_of_picking_first_match()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var first = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, title: _fixture.CreateTitle());
        var second = _fixture.CreateStoredEpisodeWithSpotifyOnly(podcast, title: _fixture.CreateTitle());
        var categorisedItem = SoundsSubmit(podcast, [first, second], title: " ");

        // Act
        var result = await Sut.AddEpisodeToExistingPodcast(categorisedItem);

        // Assert
        result.EpisodeResult.Should().Be(SubmitResultState.Created);
        result.Episode.Should().BeSameAs(_createdEpisode);
        _mocker.GetMock<IEpisodeEnricher>().Verify(
            x => x.ApplyResolvedPodcastServiceProperties(
                podcast,
                categorisedItem,
                null),
            Times.Once);
        _mocker.GetMock<IEpisodeFactory>().Verify(
            x => x.CreateEpisode(categorisedItem),
            Times.Once);
    }

    private string DistinctFrom(string other)
    {
        string candidate;
        do
        {
            candidate = _fixture.CreateTitle();
        } while (candidate.Contains(other) || other.Contains(candidate));

        return candidate;
    }

    private CategorisedItem SoundsSubmit(Podcast podcast, IEnumerable<Episode> episodes, string title)
    {
        var playId = _fixture.CreateGuid().ToString("N");
        var url = new Uri($"https://www.bbc.co.uk/sounds/play/{playId}");
        return new CategorisedItem(
            podcast,
            episodes,
            null,
            null,
            null,
            null,
            new ResolvedNonPodcastServiceItem(
                StreamingService.BbcSounds,
                podcast,
                null,
                url,
                title),
            Service.Other);
    }
}
