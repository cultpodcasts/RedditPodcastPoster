using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Resolvers;
using Api.Services.Public;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.News;
using Xunit;

namespace FunctionHost.Tests.Api.Services;

public class PublicEpisodeGetServiceTests
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private PodcastEpisodeResolverResponse _podcastResolution =
        new(null, null, PodcastEpisodeResolveState.EpisodeNotFound);
    private CataloguePlayableResolution? _catalogueResolution;

    public PublicEpisodeGetServiceTests()
    {
        _mocker.Use(NullLogger<PublicEpisodeGetService>.Instance);
        _mocker.GetMock<IPodcastEpisodeResolver>()
            .Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(() => _podcastResolution);
        _mocker.GetMock<ICataloguePlayableResolver>()
            .Setup(r => r.Resolve(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _catalogueResolution);
    }

    private Task<PublicEpisodeGetResult> Get(Guid id) =>
        _mocker.CreateInstance<PublicEpisodeGetService>()
            .GetAsync(new PodcastEpisodeRequestWrapper(id), CancellationToken.None);

    private CataloguePlayableResolution CreateNewsReportResolution(bool removed)
    {
        var organisation = new NewsOrganisation(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        var report = new NewsReport(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        report.SetNewsOrganisationProperties(organisation);
        return new CataloguePlayableResolution(
            nameof(NewsReport),
            CatalogueParentKindMapper.ToPostProjection(report, organisation),
            organisation.Id,
            organisation.Name,
            organisation.Name,
            removed);
    }

    [Fact(DisplayName =
        "Plain English rule: when no podcast episode and no news report, TV episode or film has the id, then the public get is not found, because an unknown id is a normal 404.")]
    public async Task get_reports_unknown_id_as_not_found()
    {
        // Arrange
        var id = _fixture.CreateGuid();

        // Act
        var result = await Get(id);

        // Assert
        result.Status.Should().Be(PublicEpisodeGetStatus.NotFound);
    }

    [Fact(DisplayName =
        "Plain English rule: when the id is a podcast episode, then the public get returns it with content kind Episode and the podcast as parent, because podcast responses keep their shape and only gain kind fields.")]
    public async Task get_returns_podcast_episode_with_episode_kind()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Name = _fixture.CreateTitle());
        var episode = _fixture.CreateStoredEpisode(podcast);
        _podcastResolution = new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved);

        // Act
        var result = await Get(episode.Id);

        // Assert
        result.Status.Should().Be(PublicEpisodeGetStatus.Ok);
        result.Episode.Should().BeSameAs(episode);
        result.Podcast.Should().BeSameAs(podcast);
        result.Kind!.ContentKind.Should().Be("Episode");
        result.Kind.ParentId.Should().Be(podcast.Id);
        result.Kind.Slug.Should().Be(podcast.Name);
        result.Kind.Path.Should().StartWith("/podcast/");
        _mocker.GetMock<ICataloguePlayableResolver>()
            .Verify(r => r.Resolve(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact(DisplayName =
        "Plain English rule: when the podcast is removed, then the public get is not found, because removed podcasts are hidden from signed-in users.")]
    public async Task get_reports_removed_podcast_as_not_found()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast(p => p.Removed = true);
        var episode = _fixture.CreateStoredEpisode(podcast);
        _podcastResolution = new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved);

        // Act
        var result = await Get(episode.Id);

        // Assert
        result.Status.Should().Be(PublicEpisodeGetStatus.NotFound);
    }

    [Fact(DisplayName =
        "Plain English rule: when the id is not a podcast episode but is a news report, then the public get returns the report with its kind, parent and site path, because a bookmark made before the catalogue migration keeps the id.")]
    public async Task get_returns_catalogue_playable_by_id()
    {
        // Arrange
        _catalogueResolution = CreateNewsReportResolution(removed: false);

        // Act
        var result = await Get(_catalogueResolution.Projection.Episode.Id);

        // Assert
        result.Status.Should().Be(PublicEpisodeGetStatus.Ok);
        result.Episode!.Id.Should().Be(_catalogueResolution.Projection.Episode.Id);
        result.Podcast!.Name.Should().Be(_catalogueResolution.ParentName);
        result.Kind!.ContentKind.Should().Be("NewsReport");
        result.Kind.ParentName.Should().Be(_catalogueResolution.ParentName);
        result.Kind.Path.Should().StartWith("/news/");
    }

    [Fact(DisplayName =
        "Plain English rule: when the news report, TV episode or film (or its parent) is removed, then the public get is not found, because removed items stay hidden as removed episodes do.")]
    public async Task get_reports_removed_catalogue_playable_as_not_found()
    {
        // Arrange
        _catalogueResolution = CreateNewsReportResolution(removed: true);

        // Act
        var result = await Get(_catalogueResolution.Projection.Episode.Id);

        // Assert
        result.Status.Should().Be(PublicEpisodeGetStatus.NotFound);
    }
}
