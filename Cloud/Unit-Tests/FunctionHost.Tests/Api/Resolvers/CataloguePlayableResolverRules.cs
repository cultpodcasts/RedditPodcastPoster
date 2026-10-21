using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Resolvers;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Resolvers;

public class CataloguePlayableResolverRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private NewsReport? _report;
    private NewsOrganisation? _organisation;
    private TvShowEpisode? _tvEpisode;
    private TvShow? _show;
    private Film? _film;

    public CataloguePlayableResolverRules()
    {
        _mocker.GetMock<INewsReportRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<NewsReport, bool>>>()))
            .ReturnsAsync(() => _report);
        _mocker.GetMock<INewsOrganisationRepository>()
            .Setup(r => r.GetNewsOrganisation(It.IsAny<Guid>()))
            .ReturnsAsync(() => _organisation);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync(() => _tvEpisode);
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShow(It.IsAny<Guid>()))
            .ReturnsAsync(() => _show);
        _mocker.GetMock<IFilmRepository>()
            .Setup(r => r.GetFilm(It.IsAny<Guid>()))
            .ReturnsAsync(() => _film);
    }

    private Task<CataloguePlayableResolution?> Resolve(Guid id) =>
        _mocker.CreateInstance<CataloguePlayableResolver>().Resolve(id, CancellationToken.None);

    [Fact(DisplayName =
        "Plain English rule: when a news report has the id, then it resolves as NewsReport with its organisation as parent and slug, because the news route is /news/{organisation}/{shortId}.")]
    public async Task resolves_news_report_with_organisation_parent()
    {
        // Arrange
        _organisation = new NewsOrganisation(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _report = new NewsReport(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _report.SetNewsOrganisationProperties(_organisation);

        // Act
        var resolution = await Resolve(_report.Id);

        // Assert
        resolution.Should().NotBeNull();
        resolution!.ContentKind.Should().Be("NewsReport");
        resolution.ParentId.Should().Be(_organisation.Id);
        resolution.ParentName.Should().Be(_organisation.Name);
        resolution.Slug.Should().Be(_organisation.Name);
        resolution.Removed.Should().BeFalse();
        resolution.Projection.Episode.Id.Should().Be(_report.Id);
        resolution.Projection.Episode.Title.Should().Be(_report.Title);
        resolution.Projection.Podcast.Name.Should().Be(_organisation.Name);
    }

    [Fact(DisplayName =
        "Plain English rule: when a TV episode's show document is missing, then it still resolves from the denormalised show id and name on the episode, because the item must not vanish from bookmarks over a missing parent read.")]
    public async Task resolves_tv_episode_from_denormalised_show_when_parent_missing()
    {
        // Arrange
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _tvEpisode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        _tvEpisode.SetTvShowProperties(show);

        // Act
        var resolution = await Resolve(_tvEpisode.Id);

        // Assert
        resolution!.ContentKind.Should().Be("TvShowEpisode");
        resolution.ParentId.Should().Be(show.Id);
        resolution.ParentName.Should().Be(show.Name);
        resolution.Removed.Should().BeFalse();
    }

    [Fact(DisplayName =
        "Plain English rule: when the TV show is removed, then the TV episode resolves as removed, because a removed parent hides its episodes.")]
    public async Task resolves_tv_episode_as_removed_when_show_removed()
    {
        // Arrange
        _show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Removed = true };
        _tvEpisode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), TvShowId = _show.Id };

        // Act
        var resolution = await Resolve(_tvEpisode.Id);

        // Assert
        resolution!.Removed.Should().BeTrue();
    }

    [Fact(DisplayName =
        "Plain English rule: when a film has the id, then it resolves as Film with no parent and its own name as slug, because a film is a one-off with route /film/{name}/{shortId}.")]
    public async Task resolves_film_without_parent()
    {
        // Arrange
        _film = new Film(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Removed = true };

        // Act
        var resolution = await Resolve(_film.Id);

        // Assert
        resolution!.ContentKind.Should().Be("Film");
        resolution.ParentId.Should().BeNull();
        resolution.ParentName.Should().BeNull();
        resolution.Slug.Should().Be(_film.Name);
        resolution.Removed.Should().BeTrue();
        resolution.Projection.Episode.Title.Should().Be(_film.Name);
    }

    [Fact(DisplayName =
        "Plain English rule: when no news report, TV episode or film has the id, then nothing resolves, because the caller reports a normal not found.")]
    public async Task resolves_nothing_for_unknown_id()
    {
        // Arrange
        var id = _fixture.CreateGuid();

        // Act
        var resolution = await Resolve(id);

        // Assert
        resolution.Should().BeNull();
    }
}
