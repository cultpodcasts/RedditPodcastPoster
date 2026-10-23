using System.Linq.Expressions;
using System.Net;
using Api.Handlers;
using Api.Handlers.Discovery;
using Api.Models;
using Api.Services.Discovery;
using Api.Services.Podcasts;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Auth0.Models;
using RedditPodcastPoster.Catalogue.Podcasts;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using Xunit;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api.Services.Discovery;

public class DiscoveryChannelBanRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<DiscoveryResultsDocument> _documents = [];
    private readonly List<Podcast> _podcasts = [];
    private readonly List<NewsOrganisation> _organisations = [];
    private readonly List<Podcast> _savedPodcasts = [];
    private readonly List<PodcastChangeRequestWrapper> _updates = [];

    public DiscoveryChannelBanRules()
    {
        _mocker.GetMock<IDiscoveryResultsRepository>()
            .Setup(r => r.GetAllUnprocessed())
            .Returns(() => ToAsync(_documents));
        _mocker.GetMock<IDiscoveryResultsRepository>()
            .Setup(r => r.Save(It.IsAny<DiscoveryResultsDocument>()))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns((Expression<Func<Podcast, bool>> predicate) => ToAsync(_podcasts.Where(predicate.Compile())));
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.Save(It.IsAny<Podcast>()))
            .Callback<Podcast>(podcast => _savedPodcasts.Add(podcast))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<NewsOrganisation, bool>>>()))
            .Returns((Expression<Func<NewsOrganisation, bool>> predicate) =>
                ToAsync(_organisations.Where(predicate.Compile())));
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<RedditPodcastPoster.Models.TvShows.TvShow, bool>>>()))
            .Returns(ToAsync(Array.Empty<RedditPodcastPoster.Models.TvShows.TvShow>()));
        _mocker.GetMock<IPodcastFactory>()
            .Setup(f => f.Create(It.IsAny<string>()))
            .ReturnsAsync((string name) => _fixture.CreatePodcast(p => p.Name = name));
        _mocker.GetMock<IPodcastUpdateService>()
            .Setup(s => s.UpdateAsync(It.IsAny<PodcastChangeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .Callback<PodcastChangeRequestWrapper, CancellationToken>((wrapper, _) => _updates.Add(wrapper))
            .ReturnsAsync(new PodcastUpdateResult(PodcastUpdateStatus.Accepted));
    }

    [Fact(DisplayName =
        "Banning a discovery result with no catalogue podcast creates a removed podcast from the show name and channel id, and links other open results with that show name, because a never-before-seen channel has to exist before later runs can skip it.")]
    public async Task unknown_channel_becomes_a_removed_podcast()
    {
        // Arrange
        var showName = _fixture.CreateTitle();
        var channelId = _fixture.CreateYouTubeChannelId();
        var result = new DiscoveryResult
        {
            Id = _fixture.CreateGuid(),
            ShowName = showName,
            YouTubeChannelId = channelId
        };
        var sibling = new DiscoveryResult { Id = _fixture.CreateGuid(), ShowName = showName };
        _documents.Add(Document([result, sibling]));
        var sut = _mocker.CreateInstance<DiscoveryChannelBanService>();

        // Act
        var outcome = await sut.BanAsync(result.Id, CancellationToken.None);

        // Assert
        outcome.Status.Should().Be(DiscoveryChannelBanStatus.Accepted);
        var created = _savedPodcasts.Should().ContainSingle().Subject;
        created.Removed.Should().BeTrue();
        created.Name.Should().Be(showName);
        created.YouTubeChannelId.Should().Be(channelId);
        result.MatchingPodcastIds.Should().Equal(created.Id);
        sibling.MatchingPodcastIds.Should().Equal(created.Id);
        _updates.Should().BeEmpty();
        _mocker.GetMock<IPodcastService>().Verify(
            s => s.GetEpisodeUrlOwner(It.IsAny<Uri>(), It.IsAny<IndexingContext>()), Times.Never);
    }

    [Fact(DisplayName =
        "Banning a discovery result whose channel is already a podcast sets that podcast removed and does not create another, because the catalogue row is what later discovery runs filter on.")]
    public async Task existing_podcast_is_marked_removed()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        var podcast = _fixture.CreatePodcast(p => p.YouTubeChannelId = channelId);
        _podcasts.Add(podcast);
        var result = new DiscoveryResult
        {
            Id = _fixture.CreateGuid(),
            ShowName = _fixture.CreateTitle(),
            YouTubeChannelId = channelId
        };
        _documents.Add(Document([result]));
        var sut = _mocker.CreateInstance<DiscoveryChannelBanService>();

        // Act
        var outcome = await sut.BanAsync(result.Id, CancellationToken.None);

        // Assert
        outcome.Status.Should().Be(DiscoveryChannelBanStatus.Accepted);
        _updates.Should().ContainSingle().Which.Podcast.Removed.Should().BeTrue();
        _updates[0].PodcastId.Should().Be(podcast.Id);
        _mocker.GetMock<IPodcastFactory>().Verify(f => f.Create(It.IsAny<string>()), Times.Never);
        result.MatchingPodcastIds.Should().Equal(podcast.Id);
    }

    [Fact(DisplayName =
        "Banning a result that is not in the open queue is not found and writes nothing, because there is no show id to remove.")]
    public async Task missing_result_is_not_found()
    {
        // Arrange
        var sut = _mocker.CreateInstance<DiscoveryChannelBanService>();

        // Act
        var outcome = await sut.BanAsync(_fixture.CreateGuid(), CancellationToken.None);

        // Assert
        outcome.Status.Should().Be(DiscoveryChannelBanStatus.NotFound);
        _savedPodcasts.Should().BeEmpty();
        _updates.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Banning a channel a news organisation already owns is a conflict and creates no podcast, because that organisation is the catalogue owner.")]
    public async Task parent_owner_is_a_conflict()
    {
        // Arrange
        var channelId = _fixture.CreateYouTubeChannelId();
        _organisations.Add(new NewsOrganisation(_fixture.CreateTitle())
        {
            Indexing = new CatalogueIndexingSource { YouTubeChannelId = channelId }
        });
        var result = new DiscoveryResult
        {
            Id = _fixture.CreateGuid(),
            ShowName = _fixture.CreateTitle(),
            YouTubeChannelId = channelId
        };
        _documents.Add(Document([result]));
        var sut = _mocker.CreateInstance<DiscoveryChannelBanService>();

        // Act
        var outcome = await sut.BanAsync(result.Id, CancellationToken.None);

        // Assert
        outcome.Status.Should().Be(DiscoveryChannelBanStatus.Conflict);
        _savedPodcasts.Should().BeEmpty();
        _updates.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Banning a result that only has an episode URL resolves the channel id, creates the removed podcast from it, and stores the id on the result, because older reports were saved before show ids were recorded.")]
    public async Task missing_show_id_is_resolved_from_the_episode_url()
    {
        // Arrange
        var showName = _fixture.CreateTitle();
        var channelId = _fixture.CreateYouTubeChannelId();
        var videoId = _fixture.CreateYouTubeId();
        var result = new DiscoveryResult
        {
            Id = _fixture.CreateGuid(),
            ShowName = showName,
            Urls = new DiscoveryResultUrls { YouTube = new Uri($"https://www.youtube.com/watch?v={videoId}") }
        };
        _documents.Add(Document([result]));
        _mocker.GetMock<IPodcastService>()
            .Setup(s => s.GetEpisodeUrlOwner(result.Urls.YouTube!, It.IsAny<IndexingContext>()))
            .ReturnsAsync(new EpisodeUrlOwner(null, new EpisodeUrlOwnerIds(YouTubeChannelId: channelId)));
        var sut = _mocker.CreateInstance<DiscoveryChannelBanService>();

        // Act
        var outcome = await sut.BanAsync(result.Id, CancellationToken.None);

        // Assert
        outcome.Status.Should().Be(DiscoveryChannelBanStatus.Accepted);
        result.YouTubeChannelId.Should().Be(channelId);
        _savedPodcasts.Should().ContainSingle().Which.YouTubeChannelId.Should().Be(channelId);
        _savedPodcasts[0].Removed.Should().BeTrue();
        _savedPodcasts[0].Name.Should().Be(showName);
    }

    private DiscoveryResultsDocument Document(IEnumerable<DiscoveryResult> results) =>
        new(DomainTestFixture.UtcDaysAgo(1), results) { SearchSince = _fixture.Create<string>() };

    private static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> items)
    {
        foreach (var item in items.ToList())
        {
            yield return item;
            await Task.Yield();
        }
    }
}

public class PostDiscoveryChannelBanHandlerRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();

    [Fact(DisplayName =
        "Banning a discovery channel acknowledges with an empty 202, because the client loads the podcast with GET rather than binding a body from the command.")]
    public async Task ban_acknowledges_without_a_podcast_body()
    {
        // Arrange
        var resultId = _fixture.CreateGuid();
        _mocker.GetMock<IDiscoveryChannelBanService>()
            .Setup(s => s.BanAsync(resultId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoveryChannelBanResult(DiscoveryChannelBanStatus.Accepted));
        var sut = _mocker.CreateInstance<PostDiscoveryChannelBanHandler>();
        var (req, _) = HttpTestHelpers.CreateRequestResponse("POST");
        var ctx = new HandlerContext(req.Object, new ClientPrincipal());

        // Act
        var response = await sut.Handle(ctx, resultId, CancellationToken.None);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, leaveOpen: true);
        (await reader.ReadToEndAsync()).Should().BeEmpty();
    }
}
