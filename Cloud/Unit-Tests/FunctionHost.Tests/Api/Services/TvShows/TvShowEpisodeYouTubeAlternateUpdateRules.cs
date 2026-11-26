using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Catalogue;
using Api.Services.TvShows;
using FunctionHost.Tests.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Models.YouTube;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using Xunit;

namespace FunctionHost.Tests.Api.Services.TvShows;

public class TvShowEpisodeYouTubeAlternateUpdateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly SearchCleanupMock _search = new();
    private TvShowEpisode? _saved;

    public TvShowEpisodeYouTubeAlternateUpdateRules()
    {
        _mocker.Use(NullLogger<TvShowEpisodeService>.Instance);
        _mocker.Use(NullLogger<CataloguePlayableSearchSync>.Instance);
        _mocker.Use(_search.Cleanup);
        _mocker.GetMock<IPlayableSearchDocumentSwap>()
            .Setup(s => s.UploadAsync(It.IsAny<IReadOnlyList<EpisodeSearchRecord>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _mocker.Use(_mocker.CreateInstance<CataloguePlayableSearchSync>());
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<TvShowEpisode>()))
            .Callback<TvShowEpisode>(episode => _saved = episode)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.TryReplace(It.IsAny<TvShowEpisode>(), It.IsAny<string>()))
            .Callback<TvShowEpisode, string>((episode, _) => _saved = episode)
            .ReturnsAsync(true);
        _mocker.GetMock<IUrlMembershipLookup>()
            .Setup(lookup => lookup.FindYouTubeHolderExcluding(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CatalogueServiceUrlOwner?)null);
    }

    private TvShowEpisode CreateEpisode(Action<TvShowEpisode>? customise = null)
    {
        var show = new TvShow(_fixture.CreateTitle()) { Id = _fixture.CreateGuid() };
        var episode = new TvShowEpisode(_fixture.CreateTitle())
        {
            Id = _fixture.CreateGuid(),
            Length = _fixture.CreateDuration()
        };
        episode.SetTvShowProperties(show);
        episode.SetRelease(CatalogueRelease.FromDate(DateOnly.FromDateTime(DomainTestFixture.UtcDateDaysAgo(12))));
        customise?.Invoke(episode);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<System.Linq.Expressions.Expression<Func<TvShowEpisode, bool>>>()))
            .ReturnsAsync(episode);
        _mocker.GetMock<ITvShowEpisodeRepository>()
            .Setup(r => r.GetTvShowEpisodeWithETag(episode.TvShowId, episode.Id))
            .ReturnsAsync(() => new ETagged<TvShowEpisode>(episode, "\"synthetic-etag\""));
        return episode;
    }

    private YouTubeAlternate CreateAlternate()
    {
        var id = _fixture.CreateYouTubeId();
        return new YouTubeAlternate
        {
            YouTubeId = id,
            Url = _fixture.DefaultYouTubeUrl(id),
            ChannelName = _fixture.CreateTitle(),
            AddedAt = DateTime.UtcNow
        };
    }

    private static TvShowEpisodeChangeRequest SeenYouTube(TvShowEpisode episode, TvShowEpisodeChangeRequest change)
    {
        change.ExpectedAdditionalYouTubeIds = episode.AdditionalYouTube?.Select(item => item.YouTubeId).ToArray() ?? [];
        return change;
    }

    private Task<TvShowEpisodeUpdateResult> Update(TvShowEpisode episode, TvShowEpisodeChangeRequest change) =>
        _mocker.CreateInstance<TvShowEpisodeService>()
            .UpdateAsync(new TvShowEpisodeChangeRequestWrapper(episode.Id, change), CancellationToken.None);

    [Fact(DisplayName =
        "PATCH TV episode additionalYouTube adds a YouTube URL as an alternate, keeps listed existing ids, and a keep-list that omits an id removes it, because TV episodes share the film additional-YouTube edit (RPP #1526).")]
    public async Task additional_youtube_add_keep_and_remove()
    {
        // Arrange
        var existing = CreateAlternate();
        var episode = CreateEpisode(e => e.AdditionalYouTube = [existing]);
        var addedId = _fixture.CreateYouTubeId();
        var channelName = _fixture.CreateTitle();

        // Act
        var added = await Update(episode, SeenYouTube(episode, new TvShowEpisodeChangeRequest
        {
            AdditionalYouTube =
            [
                new YouTubeAlternateChange(existing.YouTubeId, null, null, null),
                new YouTubeAlternateChange(null, _fixture.DefaultYouTubeUrl(addedId).ToString(), _fixture.CreateTitle(), channelName)
            ]
        }));

        // Assert
        added.Status.Should().Be(TvShowEpisodeUpdateStatus.Accepted);
        _saved!.AdditionalYouTube.Should().HaveCount(2);
        _saved.AdditionalYouTube![0].YouTubeId.Should().Be(existing.YouTubeId);
        _saved.AdditionalYouTube[1].YouTubeId.Should().Be(addedId);
        _saved.AdditionalYouTube[1].ChannelName.Should().Be(channelName);

        // Arrange
        episode.AdditionalYouTube = _saved.AdditionalYouTube;

        // Act
        var removed = await Update(episode, SeenYouTube(episode, new TvShowEpisodeChangeRequest
        {
            AdditionalYouTube = [new YouTubeAlternateChange(existing.YouTubeId, null, null, null)]
        }));

        // Assert
        removed.Status.Should().Be(TvShowEpisodeUpdateStatus.Accepted);
        _saved.AdditionalYouTube.Should().ContainSingle().Which.YouTubeId.Should().Be(existing.YouTubeId);
    }

    [Fact(DisplayName =
        "PATCH TV episode promoteYouTubeId swaps that additional upload into the primary YouTube link and compact youTubeId, and the previous primary becomes the first additional upload (RPP #1526).")]
    public async Task additional_youtube_promote_swaps_into_primary()
    {
        // Arrange
        var primaryId = _fixture.CreateYouTubeId();
        var alternate = CreateAlternate();
        var episode = CreateEpisode(e =>
        {
            e.YouTubeId = primaryId;
            e.Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(primaryId) }
            };
            e.AdditionalYouTube = [alternate];
        });

        // Act
        var result = await Update(episode, SeenYouTube(episode, new TvShowEpisodeChangeRequest
        {
            AdditionalYouTube = [new YouTubeAlternateChange(alternate.YouTubeId, null, null, null)],
            PromoteYouTubeId = alternate.YouTubeId
        }));

        // Assert
        result.Status.Should().Be(TvShowEpisodeUpdateStatus.Accepted);
        _saved!.YouTubeId.Should().Be(alternate.YouTubeId);
        _saved.Services!["youtube"].Url.Should().Be(alternate.Url);
        _saved.AdditionalYouTube.Should().ContainSingle().Which.YouTubeId.Should().Be(primaryId);
    }

    [Fact(DisplayName =
        "PATCH TV episode additionalYouTube is a conflict naming the ids the client did not list when the keep-list omits them, and nothing is saved (RPP #1526).")]
    public async Task additional_youtube_keep_list_without_expected_ids_is_conflict()
    {
        // Arrange
        var existing = CreateAlternate();
        var episode = CreateEpisode(e => e.AdditionalYouTube = [existing]);

        // Act
        var result = await Update(episode, new TvShowEpisodeChangeRequest
        {
            AdditionalYouTube = [new YouTubeAlternateChange(existing.YouTubeId, null, null, null)]
        });

        // Assert
        result.Status.Should().Be(TvShowEpisodeUpdateStatus.Conflict);
        result.UnexpectedAdditionalYouTubeIds.Should().Equal(existing.YouTubeId);
        result.Message.Should().Contain("TV episode");
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH TV episode additionalYouTube cannot add the episode's primary YouTube id, because that id is already the playable link (RPP #1526).")]
    public async Task additional_youtube_rejects_primary_id()
    {
        // Arrange
        var primaryId = _fixture.CreateYouTubeId();
        var episode = CreateEpisode(e =>
        {
            e.YouTubeId = primaryId;
            e.Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(primaryId) }
            };
        });

        // Act
        var result = await Update(episode, SeenYouTube(episode, new TvShowEpisodeChangeRequest
        {
            AdditionalYouTube =
            [
                new YouTubeAlternateChange(null, _fixture.DefaultYouTubeUrl(primaryId).ToString(), null, null)
            ]
        }));

        // Assert
        result.Status.Should().Be(TvShowEpisodeUpdateStatus.BadRequest);
        result.Message.Should().Contain(primaryId);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH TV episode promoteYouTubeId keeps the old primary as additionalYouTube[0] when the episode has youTubeId but no services.youtube url, and when the stored url cannot be parsed (RPP #1526).")]
    public async Task additional_youtube_promote_keeps_old_primary_when_url_is_missing_or_unparseable()
    {
        // Arrange
        var primaryId = _fixture.CreateYouTubeId();
        var alternate = CreateAlternate();
        var episode = CreateEpisode(e =>
        {
            e.YouTubeId = primaryId;
            e.Services = null;
            e.AdditionalYouTube = [alternate];
        });

        // Act
        var missingUrl = await Update(episode, SeenYouTube(episode, new TvShowEpisodeChangeRequest
        {
            PromoteYouTubeId = alternate.YouTubeId
        }));
        var kept = _saved!.AdditionalYouTube!.ToList();

        var unparseable = CreateEpisode(e =>
        {
            e.YouTubeId = primaryId;
            e.Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = new Uri("https://example.com/not-youtube") }
            };
            e.AdditionalYouTube = [alternate];
        });
        var unparseableResult = await Update(unparseable, SeenYouTube(unparseable, new TvShowEpisodeChangeRequest
        {
            PromoteYouTubeId = alternate.YouTubeId
        }));

        // Assert
        missingUrl.Status.Should().Be(TvShowEpisodeUpdateStatus.Accepted);
        kept[0].YouTubeId.Should().Be(primaryId);
        kept[0].Url.Should().Be(_fixture.DefaultYouTubeUrl(primaryId));
        unparseableResult.Status.Should().Be(TvShowEpisodeUpdateStatus.Accepted);
        _saved!.AdditionalYouTube!.First().YouTubeId.Should().Be(primaryId);
        _saved.AdditionalYouTube!.First().Url.Should().Be(_fixture.DefaultYouTubeUrl(primaryId));
    }

    [Fact(DisplayName =
        "PATCH TV episode additionalYouTube is a bad request when it lists the YouTube id that services.youtube is being changed to in the same request, because the resulting primary cannot also be an alternate (RPP #1526).")]
    public async Task additional_youtube_rejects_id_that_becomes_the_primary_in_the_same_patch()
    {
        // Arrange
        var newPrimary = _fixture.CreateYouTubeId();
        var episode = CreateEpisode();

        // Act
        var result = await Update(episode, new TvShowEpisodeChangeRequest
        {
            Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = _fixture.DefaultYouTubeUrl(newPrimary) }
            },
            AdditionalYouTube =
            [
                new YouTubeAlternateChange(null, _fixture.DefaultYouTubeUrl(newPrimary).ToString(), null, null)
            ]
        });

        // Assert
        result.Status.Should().Be(TvShowEpisodeUpdateStatus.BadRequest);
        result.Message.Should().Contain(newPrimary);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH TV episode additionalYouTube is a conflict naming the holder when the added YouTube id already belongs to another item (RPP #1526).")]
    public async Task additional_youtube_add_is_conflict_when_another_item_already_holds_the_id()
    {
        // Arrange
        var heldId = _fixture.CreateYouTubeId();
        var holderId = _fixture.CreateGuid();
        var holderName = _fixture.CreateTitle();
        var episode = CreateEpisode();
        _mocker.GetMock<IUrlMembershipLookup>()
            .Setup(lookup => lookup.FindYouTubeHolderExcluding(
                heldId,
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueServiceUrlOwner("NewsReport", holderName, holderId));

        // Act
        var result = await Update(episode, new TvShowEpisodeChangeRequest
        {
            AdditionalYouTube =
            [
                new YouTubeAlternateChange(null, _fixture.DefaultYouTubeUrl(heldId).ToString(), null, null)
            ]
        });

        // Assert
        result.Status.Should().Be(TvShowEpisodeUpdateStatus.Conflict);
        result.Message.Should().Contain(heldId);
        result.Message.Should().Contain("NewsReport");
        result.Message.Should().Contain(holderId.ToString());
        result.Message.Should().Contain(holderName);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "Curator GET of a TV episode includes additionalYouTube, because the editor reloads the list it will PATCH (RPP #1526).")]
    public void curator_get_includes_additional_youtube()
    {
        // Arrange
        var alternate = CreateAlternate();
        var episode = CreateEpisode(e => e.AdditionalYouTube = [alternate]);

        // Act
        var dto = episode.ToDto();

        // Assert
        dto.AdditionalYouTube.Should().ContainSingle().Which.YouTubeId.Should().Be(alternate.YouTubeId);
        dto.AdditionalYouTube![0].Url.Should().Be(alternate.Url);
    }
}
