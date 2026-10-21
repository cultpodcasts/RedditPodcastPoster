using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.TvShows;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.TvShows;

public class TvShowCascadeRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();
    private readonly List<TvShowEpisode> _episodes = [];
    private readonly List<TvShowEpisode[]> _saveBatches = [];
    private TvShow? _saved;

    public TvShowCascadeRules()
    {
        _mocker.Use(NullLogger<TvShowService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.Save(It.IsAny<TvShow>()))
            .Callback<TvShow>(show => _saved = show)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetByTvShowId(It.IsAny<Guid>()))
            .Returns(() => _episodes.ToAsyncEnumerable());
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<IEnumerable<TvShowEpisode>>()))
            .Callback<IEnumerable<TvShowEpisode>>(episodes => _saveBatches.Add(episodes.ToArray()))
            .Returns(Task.CompletedTask);
    }

    private TvShow CreateOrganisation(bool? removed = null)
    {
        var show = new TvShow("Synthetic Docuseries") { Id = _fixture.CreateGuid(), Removed = removed };
        _mocker.GetMock<ITvShowRepository>()
            .Setup(r => r.GetTvShow(show.Id))
            .ReturnsAsync(show);
        return show;
    }

    private TvShowEpisode AddReport(TvShow show, bool removed = false)
    {
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Removed = removed };
        episode.SetTvShowProperties(show);
        _episodes.Add(episode);
        return episode;
    }

    private Task<TvShowUpdateResult> Update(TvShow show, TvShowChangeRequest change) =>
        _mocker.CreateInstance<TvShowService>()
            .UpdateAsync(new TvShowChangeRequestWrapper(show.Id, change), CancellationToken.None);

    [Fact(DisplayName =
        "PATCH TV show removed=true sets parentRemoved on every episode and deletes all their search documents, because that is how a podcast removal cascades.")]
    public async Task remove_cascades_to_episodes()
    {
        // Arrange
        var show = CreateOrganisation();
        var live = AddReport(show);
        var alreadyRemoved = AddReport(show, removed: true);

        // Act
        var result = await Update(show, new TvShowChangeRequest { Removed = true });

        // Assert
        result.Status.Should().Be(TvShowUpdateStatus.Accepted);
        _saved!.IsRemoved().Should().BeTrue();
        live.ParentRemoved.Should().BeTrue();
        alreadyRemoved.ParentRemoved.Should().BeTrue();
        live.Removed.Should().BeFalse();
        alreadyRemoved.Removed.Should().BeTrue();
        _saveBatches.SelectMany(b => b).Should().BeEquivalentTo([live, alreadyRemoved]);
        _search.DeletedKeys.Should().BeEquivalentTo([live.Id.ToString(), alreadyRemoved.Id.ToString()]);
    }

    [Fact(DisplayName =
        "PATCH TV show removed=false clears parentRemoved and saves the episodes but keeps individually removed ones removed and deletes nothing, because the indexer re-adds live episodes on its next run.")]
    public async Task unremove_clears_parent_removed_only()
    {
        // Arrange
        var show = CreateOrganisation(removed: true);
        var live = AddReport(show);
        var individuallyRemoved = AddReport(show, removed: true);

        // Act
        await Update(show, new TvShowChangeRequest { Removed = false });

        // Assert
        _saved!.IsRemoved().Should().BeFalse();
        live.ParentRemoved.Should().BeFalse();
        individuallyRemoved.ParentRemoved.Should().BeFalse();
        individuallyRemoved.Removed.Should().BeTrue();
        _saveBatches.SelectMany(b => b).Should().BeEquivalentTo([live, individuallyRemoved]);
        _search.DeleteBatches.Should().BeEmpty();
    }

    [Fact(DisplayName =
        "Retrying PATCH TV show removed=true saves no unchanged episodes but deletes the search documents again, because a retry must finish a partial cascade without rewriting everything.")]
    public async Task remove_retry_is_idempotent()
    {
        // Arrange
        var show = CreateOrganisation(removed: true);
        var episode = AddReport(show);

        // Act
        await Update(show, new TvShowChangeRequest { Removed = true });

        // Assert
        episode.ParentRemoved.Should().BeTrue();
        _saveBatches.Should().BeEmpty();
        _search.DeletedKeys.Should().Equal(episode.Id.ToString());
    }

    [Fact(DisplayName =
        "PATCH TV show removed=true over 1001 episodes saves in batches of 100 and deletes in batches of 1000, because a large show must not exceed the store or Azure Search batch limits.")]
    public async Task remove_batches_large_shows()
    {
        // Arrange
        var show = CreateOrganisation();
        for (var i = 0; i < 1001; i++)
        {
            AddReport(show);
        }

        // Act
        await Update(show, new TvShowChangeRequest { Removed = true });

        // Assert
        _saveBatches.Should().HaveCount(11);
        _saveBatches.Max(b => b.Length).Should().Be(100);
        _search.DeleteBatches.Select(b => b.Length).Should().Equal(1000, 1);
        _search.DeletedKeys.Should().OnlyHaveUniqueItems().And.HaveCount(1001);
    }

    [Fact(DisplayName =
        "PATCH TV show sets publisher (trimmed) and description and GET returns them, because both are now curator-editable.")]
    public async Task update_sets_publisher_and_description()
    {
        // Arrange
        var show = CreateOrganisation();

        // Act
        await Update(show, new TvShowChangeRequest
        {
            PublisherName = " Synthetic Studios ",
            Description = "Synthetic docuseries"
        });
        var dto = _saved!.ToDto();

        // Assert
        _saved!.PublisherName.Should().Be("Synthetic Studios");
        _saved.Description.Should().Be("Synthetic docuseries");
        dto.PublisherName.Should().Be("Synthetic Studios");
        dto.Description.Should().Be("Synthetic docuseries");
        _mocker.GetMock<ITvShowEpisodeRepository>().Verify(r => r.GetByTvShowId(It.IsAny<Guid>()), Times.Never);
    }
}
