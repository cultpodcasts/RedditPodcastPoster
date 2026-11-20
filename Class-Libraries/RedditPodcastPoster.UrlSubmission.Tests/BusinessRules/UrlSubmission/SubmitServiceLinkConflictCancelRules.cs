using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Factories;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

/// <summary>
/// #1483: a curator's Cancel answer to a service-link conflict prompt writes nothing on any submit path, not only
/// when the submit is for an existing podcast.
/// </summary>
public class SubmitServiceLinkConflictCancelRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "When a curator cancels a service-link conflict on a submit that is not for an existing podcast, no podcast " +
        "or episode is created and nothing is saved.")]
    public async Task cancel_creates_nothing_for_a_new_podcast()
    {
        // Arrange
        var item = SpotifySubmitWithoutPodcast();
        var sut = _mocker.CreateInstance<CategorisedItemProcessor>();

        // Act
        var result = await sut.ProcessCategorisedItem(item, CancelOptions());

        // Assert
        result.PodcastResult.Should().Be(SubmitResultState.None);
        result.EpisodeResult.Should().Be(SubmitResultState.None);
        _mocker.GetMock<IPodcastAndEpisodeFactory>().Verify(
            x => x.CreatePodcastWithEpisode(It.IsAny<CategorisedItem>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);
        _mocker.GetMock<IPodcastRepository>().Verify(x => x.Save(It.IsAny<Podcast>()), Times.Never);
        _mocker.GetMock<IEpisodeRepository>().Verify(x => x.Save(It.IsAny<Episode>()), Times.Never);
    }

    [Fact(DisplayName =
        "When a curator cancels a service-link conflict, the catalogue kind submitter is never asked to store a " +
        "film, TV or news item.")]
    public async Task cancel_skips_the_catalogue_kind_submitter()
    {
        // Arrange
        var item = SpotifySubmitWithoutPodcast();
        _mocker.GetMock<ICatalogueKindSubmitter>()
            .Setup(x => x.TrySubmit(It.IsAny<CategorisedItem>(), It.IsAny<SubmitOptions>()))
            .ReturnsAsync(new SubmitResult(SubmitResultState.Created, SubmitResultState.Created));
        var sut = _mocker.CreateInstance<CategorisedItemProcessor>();

        // Act
        var result = await sut.ProcessCategorisedItem(item, CancelOptions());

        // Assert
        result.PodcastResult.Should().Be(SubmitResultState.None);
        result.EpisodeResult.Should().Be(SubmitResultState.None);
        _mocker.GetMock<ICatalogueKindSubmitter>().Verify(
            x => x.TrySubmit(It.IsAny<CategorisedItem>(), It.IsAny<SubmitOptions>()), Times.Never);
    }

    private static SubmitOptions CancelOptions() =>
        new(null, MatchOtherServices: true, CreatePodcast: true, ServiceLinkConflict: ServiceLinkConflictChoice.Cancel);

    private CategorisedItem SpotifySubmitWithoutPodcast()
    {
        var spotifyId = _fixture.CreateSpotifyId();
        var spotify = new CategorisedSpotifyItem(
            _fixture.CreateSpotifyId(), spotifyId, _fixture.CreateTitle(), string.Empty, string.Empty,
            _fixture.CreateLongTitle(), string.Empty, DomainTestFixture.UtcAtTime(-2, _fixture.CreateNonMidnightTimeOfDay()),
            _fixture.CreateDuration(), _fixture.DefaultSpotifyUrl(spotifyId), false, null);
        return new CategorisedItem(null, [], null, spotify, null, null, null, Service.Spotify);
    }
}
