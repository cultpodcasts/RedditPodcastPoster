using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.TvShows;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.TvShows;

public class TvShowEpisodeFieldUpdateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();
    private TvShowEpisode? _saved;

    public TvShowEpisodeFieldUpdateRules()
    {
        _mocker.Use(NullLogger<TvShowEpisodeService>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<TvShowEpisode>()))
            .Callback<TvShowEpisode>(episode => _saved = episode)
            .Returns(Task.CompletedTask);
    }

    private TvShowEpisode CreateEpisode(Action<TvShowEpisode>? customise = null)
    {
        var show = new TvShow("Synthetic Cult Docuseries") { Id = _fixture.CreateGuid() };
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Length = TimeSpan.FromMinutes(44) };
        episode.SetTvShowProperties(show);
        episode.SetRelease(CatalogueRelease.FromDate(new DateOnly(2026, 9, 1)));
        episode.Subjects = ["Synthetic Cult"];
        customise?.Invoke(episode);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync(episode);
        return episode;
    }

    private Task<TvShowEpisodeUpdateResult> Update(TvShowEpisode episode, TvShowEpisodeChangeRequest change) =>
        _mocker.CreateInstance<TvShowEpisodeService>()
            .UpdateAsync(new TvShowEpisodeChangeRequestWrapper(episode.Id, change), CancellationToken.None);

    [Fact(DisplayName =
        "PATCH TV episode sets the playable fields it is given with the news-report rules and keeps omitted ones, because TV episodes share the playable editor.")]
    public async Task update_sets_playable_fields()
    {
        // Arrange
        var imdb = new Uri("https://www.imdb.com/title/tt0000002/");
        var episode = CreateEpisode(e => { e.SearchTerms = "keep"; e.Imdb = imdb; });

        // Act
        var result = await Update(episode, new TvShowEpisodeChangeRequest
        {
            Title = " Synthetic Episode Retitled ",
            Description = "Synthetic description",
            Release = "2026-10-02",
            Duration = "0:45:30",
            Explicit = true,
            Ignored = true,
            HashTag = " #Tag ",
            Language = "es"
        });

        // Assert
        result.Status.Should().Be(TvShowEpisodeUpdateStatus.Accepted);
        _saved!.Title.Should().Be("Synthetic Episode Retitled");
        _saved.Description.Should().Be("Synthetic description");
        _saved.Release!.Date.Should().Be(new DateOnly(2026, 10, 2));
        _saved.Length.Should().Be(new TimeSpan(0, 45, 30));
        _saved.Explicit.Should().BeTrue();
        _saved.Ignored.Should().BeTrue();
        _saved.HashTag.Should().Be("#Tag");
        _saved.Language.Should().Be("es");
        _saved.SearchTerms.Should().Be("keep");
        _saved.Imdb.Should().Be(imdb);
        _search.VerifyDelete(episode.Id, Times.Never());
    }

    [Fact(DisplayName =
        "PATCH TV episode guests replace the list and [] clears to null, because guests follow the episode edit API.")]
    public async Task update_guests()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Guests = ["Synthetic Old"]);

        // Act
        await Update(episode, new TvShowEpisodeChangeRequest { Guests = [" Synthetic Survivor ", " "] });
        var afterSet = _saved!.Guests;
        await Update(episode, new TvShowEpisodeChangeRequest { Guests = [] });

        // Assert
        afterSet.Should().Equal("Synthetic Survivor");
        _saved.Guests.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH TV episode subjects replace the list and remember dropped ones, because the subject enricher must not re-add them.")]
    public async Task update_subjects()
    {
        // Arrange
        var episode = CreateEpisode();

        // Act
        await Update(episode, new TvShowEpisodeChangeRequest { Subjects = ["Synthetic New"] });

        // Assert
        _saved!.Subjects.Should().Equal("Synthetic New");
        _saved.RemovedSubjects.Should().Contain("Synthetic Cult");
    }

    [Fact(DisplayName =
        "PATCH TV episode with a release time is a 400 and nothing is saved, because TV episode releases are calendar dates.")]
    public async Task update_rejects_release_time()
    {
        // Arrange
        var episode = CreateEpisode();

        // Act
        var result = await Update(episode, new TvShowEpisodeChangeRequest { Title = "Synthetic X", Release = "2026-10-02T10:00:00" });

        // Assert
        result.Status.Should().Be(TvShowEpisodeUpdateStatus.BadRequest);
        _saved.Should().BeNull();
        episode.Title.Should().NotBe("Synthetic X");
    }

    [Fact(DisplayName =
        "PATCH TV episode removed=true deletes its search document, because the sibling pull indexer never deletes.")]
    public async Task update_removed_deletes_search_document()
    {
        // Arrange
        var episode = CreateEpisode();

        // Act
        await Update(episode, new TvShowEpisodeChangeRequest { Removed = true });

        // Assert
        _saved!.Removed.Should().BeTrue();
        _search.VerifyDelete(episode.Id, Times.Once());
    }

    [Fact(DisplayName =
        "GET TV episode DTO carries the playable fields with a date-only release and h:mm:ss duration, because the curator UI edits them.")]
    public void dto_has_playable_fields()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Guests = ["Synthetic Survivor"]);

        // Act
        var dto = episode.ToDto();

        // Assert
        dto.TvShowName.Should().Be("Synthetic Cult Docuseries");
        dto.Release.Should().Be("2026-09-01");
        dto.Duration.Should().Be("0:44:00");
        dto.Subjects.Should().Equal("Synthetic Cult");
        dto.Guests.Should().Equal("Synthetic Survivor");
    }
}
