using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures; // pragma: allowlist secret
using RedditPodcastPoster.Models.Episodes; // pragma: allowlist secret
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Processors;
using Xunit;

namespace RedditPodcastPoster.UrlSubmission.Tests.BusinessRules.UrlSubmission;

public class CategorisedItemMissingEpisodeRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "When submit reports an episode was created but returns no episode, nothing is saved, because there is no document to persist.")]
    public async Task created_result_without_episode_saves_nothing()
    {
        // Arrange
        var podcast = _fixture.CreateSpotifyPrimaryPodcast(_fixture.CreateSpotifyId());
        _mocker.GetMock<IPodcastProcessor>()
            .Setup(x => x.AddEpisodeToExistingPodcast(It.IsAny<CategorisedItem>()))
            .ReturnsAsync(new SubmitResult(SubmitResultState.Created, SubmitResultState.None));
        var processor = _mocker.CreateInstance<CategorisedItemProcessor>();
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
        var result = await processor.ProcessCategorisedItem(
            categorisedItem,
            new SubmitOptions(null, MatchOtherServices: true, PersistToDatabase: true));

        // Assert
        result.EpisodeResult.Should().Be(SubmitResultState.Created);
        result.Episode.Should().BeNull();
        _mocker.GetMock<IEpisodeRepository>()
            .Verify(repository => repository.Save(It.IsAny<Episode>()), Times.Never);
        _mocker.GetMock<IPodcastRepository>()
            .Verify(repository => repository.Save(It.IsAny<Podcast>()), Times.Never);
    }
}
