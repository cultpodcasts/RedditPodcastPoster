using System.Globalization;
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
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.TvShows;

public class TvShowEpisodeFieldUpdateRules
{
    private readonly DomainTestFixture _fixture = new();
    private DateOnly _release;
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
        var episode = new TvShowEpisode(_fixture.CreateTitle()) { Id = _fixture.CreateGuid(), Length = _fixture.CreateDuration() };
        episode.SetTvShowProperties(show);
        _release = DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(30));
        episode.SetRelease(CatalogueRelease.FromDate(_release));
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
        var imdbId = _fixture.CreateImdbId();
        var episode = CreateEpisode(e => { e.SearchTerms = "keep"; e.ImdbId = imdbId; });
        var release = DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(1));
        var duration = _fixture.CreateDuration() + TimeSpan.FromSeconds(30);

        // Act
        var result = await Update(episode, new TvShowEpisodeChangeRequest
        {
            Title = " Synthetic Episode Retitled ",
            Description = "Synthetic description",
            Release = release.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Duration = duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture),
            Explicit = true,
            Ignored = true,
            HashTag = " #Tag ",
            Language = "es"
        });

        // Assert
        result.Status.Should().Be(TvShowEpisodeUpdateStatus.Accepted);
        _saved!.Title.Should().Be("Synthetic Episode Retitled");
        _saved.Description.Should().Be("Synthetic description");
        _saved.Release!.Date.Should().Be(release);
        _saved.Length.Should().Be(duration);
        _saved.Explicit.Should().BeTrue();
        _saved.Ignored.Should().BeTrue();
        _saved.HashTag.Should().Be("#Tag");
        _saved.Language.Should().Be("es");
        _saved.SearchTerms.Should().Be("keep");
        _saved.ImdbId.Should().Be(imdbId);
        _search.VerifyDelete(episode.Id, Times.Never());
    }

    [Fact(DisplayName =
        "PATCH TV episode services adds a link under the key its URL resolves to and removes a key sent with no URL or image, because playables share the film services patch.")]
    public async Task services_add_and_remove()
    {
        // Arrange
        var oldKey = "internetArchive";
        var episode = CreateEpisode(e => e.Services = new Dictionary<string, ServiceLink>
        {
            [oldKey] = new() { Url = new Uri($"https://archive.org/details/synthetic-{_fixture.CreateGuid():N}") }
        });
        var youTube = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}");

        // Act
        var result = await Update(episode, new TvShowEpisodeChangeRequest
        {
            Services = new Dictionary<string, ServiceLink>
            {
                ["anything"] = new() { Url = youTube },
                [oldKey] = new() { Url = new Uri("", UriKind.RelativeOrAbsolute) }
            }
        });

        // Assert
        result.Status.Should().Be(TvShowEpisodeUpdateStatus.Accepted);
        _saved!.Services.Should().ContainKey("youtube");
        _saved.Services!["youtube"].Url.Should().Be(youTube);
        _saved.Services.Should().NotContainKey(oldKey);
    }

    [Fact(DisplayName =
        "PATCH TV episode services with a YouTube URL syncs compact YouTubeId from the watch URL, " +
        "and clearing the YouTube service clears YouTubeId, so search can project youtubeId without nested ids.")]
    public async Task services_youtube_syncs_youtube_id()
    {
        // Arrange
        var videoId = _fixture.CreateYouTubeId();
        var youTube = _fixture.DefaultYouTubeUrl(videoId);
        var episode = CreateEpisode(e => e.YouTubeId = null);

        // Act
        var addResult = await Update(episode, new TvShowEpisodeChangeRequest
        {
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = youTube }
            }
        });

        // Assert
        addResult.Status.Should().Be(TvShowEpisodeUpdateStatus.Accepted);
        _saved!.YouTubeId.Should().Be(videoId);

        // Arrange
        _saved = null;

        // Act
        var clearResult = await Update(episode, new TvShowEpisodeChangeRequest
        {
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = new Uri("", UriKind.RelativeOrAbsolute) }
            }
        });

        // Assert
        clearResult.Status.Should().Be(TvShowEpisodeUpdateStatus.Accepted);
        _saved!.YouTubeId.Should().BeNull();
        (_saved.Services?.ContainsKey(ServiceKeys.YouTube) ?? false).Should().BeFalse();
    }

    [Fact(DisplayName =
        "PATCH TV episode rejects a non-absolute services URL without saving, because invalid links must not wipe good ones.")]
    public async Task services_reject_relative_url()
    {
        // Arrange
        var episode = CreateEpisode(e => e.Services = new Dictionary<string, ServiceLink>
        {
            ["youtube"] = new() { Url = new Uri($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}") }
        });

        // Act
        var result = await Update(episode, new TvShowEpisodeChangeRequest
        {
            Services = new Dictionary<string, ServiceLink>
            {
                ["youtube"] = new() { Url = new Uri("/relative", UriKind.Relative) }
            }
        });

        // Assert
        result.Status.Should().Be(TvShowEpisodeUpdateStatus.BadRequest);
        _saved.Should().BeNull();
        episode.Services.Should().ContainKey("youtube");
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
        dto.Release.Should().Be(_release.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        dto.Duration.Should().Be(episode.Length.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture));
        dto.Subjects.Should().Equal("Synthetic Cult");
        dto.Guests.Should().Equal("Synthetic Survivor");
    }
}
