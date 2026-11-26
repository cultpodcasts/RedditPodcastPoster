using System.Text.RegularExpressions;
using Api.Dtos.Mapping;
using Api.Models;
using Api.Resolvers;
using Api.Services.Episodes;
using Azure.Search.Documents;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.EntitySearchIndexer.Models;
using RedditPodcastPoster.EntitySearchIndexer.Services;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;
using RedditPodcastPoster.Models.YouTube;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Search.Models;
using RedditPodcastPoster.Text.Sanitisers;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Services;
using Xunit;
using Episode = RedditPodcastPoster.Models.Episodes.Episode;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using Subject = RedditPodcastPoster.Models.Subjects.Subject;

namespace FunctionHost.Tests.Api.Services;

public class EpisodeYouTubeAlternateUpdateRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private PodcastEpisodeResolverResponse _resolved =
        new(null, null, PodcastEpisodeResolveState.PodcastNotFound);
    private Episode? _saved;

    public EpisodeYouTubeAlternateUpdateRules()
    {
        _mocker.Use(new EpisodeChangeApplier(NullLogger<EpisodeChangeApplier>.Instance));
        _mocker.Use(new EpisodeSearchIndexCleanup(
            CreateUninitializedSearchClient(),
            NullLogger<EpisodeSearchIndexCleanup>.Instance));
        _mocker.Use(NullLogger<EpisodeUpdateService>.Instance);
        _mocker.GetMock<IPodcastEpisodeResolver>()
            .Setup(r => r.ResolvePodcast(It.IsAny<PodcastEpisodeResolverRequest>(), It.IsAny<string>()))
            .ReturnsAsync(() => _resolved);
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.Save(It.IsAny<Episode>()))
            .Callback<Episode>(episode => _saved = episode)
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.TryReplace(It.IsAny<Episode>(), It.IsAny<string>()))
            .Callback<Episode, string>((episode, _) => _saved = episode)
            .ReturnsAsync(true);
        _mocker.GetMock<IEpisodeSearchIndexerService>()
            .Setup(s => s.IndexEpisode(It.IsAny<Podcast>(), It.IsAny<Episode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EntitySearchIndexerResponse { IndexerState = IndexerState.Executed });
        _mocker.GetMock<IEpisodeSearchIndexerService>()
            .Setup(s => s.IndexEpisode(
                It.IsAny<Podcast>(),
                It.IsAny<Episode>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(new EntitySearchIndexerResponse { IndexerState = IndexerState.Executed });
        _mocker.GetMock<IUrlMembershipLookup>()
            .Setup(lookup => lookup.FindYouTubeHolderExcluding(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((CatalogueServiceUrlOwner?)null);
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitiseTitle(It.IsAny<string>(), It.IsAny<Regex?>(), It.IsAny<string[]>(),
                It.IsAny<string[]>(), It.IsAny<string?>()))
            .ReturnsAsync((string title, Regex? _, string[] _, string[] _, string? _) => title);
        _mocker.GetMock<ITextSanitiser>()
            .Setup(x => x.SanitiseDescription(It.IsAny<string>(), It.IsAny<Regex?>()))
            .Returns((string description, Regex? _) => description);
    }

    private (Episode Episode, Podcast Podcast) Resolved(Action<Episode>? customise = null)
    {
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        customise?.Invoke(episode);
        _resolved = new PodcastEpisodeResolverResponse(episode, podcast, PodcastEpisodeResolveState.Resolved);
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.GetEpisodeWithETag(episode.PodcastId, episode.Id))
            .ReturnsAsync(() => new ETagged<Episode>(episode, "\"synthetic-etag\""));
        return (episode, podcast);
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

    private static EpisodeChangeRequest SeenYouTube(Episode episode, EpisodeChangeRequest change)
    {
        change.ExpectedAdditionalYouTubeIds = episode.AdditionalYouTube?.Select(item => item.YouTubeId).ToArray() ?? [];
        return change;
    }

    private Task<EpisodeUpdateResult> Update(Episode episode, Podcast podcast, EpisodeChangeRequest change) =>
        _mocker.CreateInstance<EpisodeUpdateService>().UpdateAsync(
            new EpisodeChangeRequestWrapper(podcast.Id, episode.Id, change),
            CancellationToken.None);

    [Fact(DisplayName =
        "PATCH episode additionalYouTube adds a YouTube URL as an alternate, keeps listed existing ids, and a keep-list that omits an id removes it, because podcast episodes share the film additional-YouTube edit (RPP #1526).")]
    public async Task additional_youtube_add_keep_and_remove()
    {
        // Arrange
        var existing = CreateAlternate();
        var (episode, podcast) = Resolved(e => e.AdditionalYouTube = [existing]);
        var addedId = _fixture.CreateYouTubeId();
        var channelName = _fixture.CreateTitle();

        // Act
        var added = await Update(episode, podcast, SeenYouTube(episode, new EpisodeChangeRequest
        {
            AdditionalYouTube =
            [
                new YouTubeAlternateChange(existing.YouTubeId, null, null, null),
                new YouTubeAlternateChange(null, _fixture.DefaultYouTubeUrl(addedId).ToString(), _fixture.CreateTitle(), channelName)
            ]
        }));

        // Assert
        added.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        _saved!.AdditionalYouTube.Should().HaveCount(2);
        _saved.AdditionalYouTube![0].YouTubeId.Should().Be(existing.YouTubeId);
        _saved.AdditionalYouTube[1].YouTubeId.Should().Be(addedId);
        _saved.AdditionalYouTube[1].ChannelName.Should().Be(channelName);

        // Arrange
        episode.AdditionalYouTube = _saved.AdditionalYouTube;

        // Act
        var removed = await Update(episode, podcast, SeenYouTube(episode, new EpisodeChangeRequest
        {
            AdditionalYouTube = [new YouTubeAlternateChange(existing.YouTubeId, null, null, null)]
        }));

        // Assert
        removed.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        _saved.AdditionalYouTube.Should().ContainSingle().Which.YouTubeId.Should().Be(existing.YouTubeId);
    }

    [Fact(DisplayName =
        "PATCH episode promoteYouTubeId swaps that additional upload into services.youtube and ids.youtube, and the previous primary becomes the first additional upload (RPP #1526).")]
    public async Task additional_youtube_promote_moves_ids_youtube()
    {
        // Arrange
        var (episode, podcast) = Resolved();
        var primaryId = episode.Ids!.YouTube;
        var alternate = CreateAlternate();
        episode.AdditionalYouTube = [alternate];

        // Act
        var result = await Update(episode, podcast, SeenYouTube(episode, new EpisodeChangeRequest
        {
            AdditionalYouTube = [new YouTubeAlternateChange(alternate.YouTubeId, null, null, null)],
            PromoteYouTubeId = alternate.YouTubeId
        }));

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        _saved!.Ids.YouTube.Should().Be(alternate.YouTubeId);
        _saved.Services!["youtube"].Url.Should().Be(alternate.Url);
        _saved.AdditionalYouTube.Should().ContainSingle().Which.YouTubeId.Should().Be(primaryId);
    }

    [Fact(DisplayName =
        "PATCH episode additionalYouTube is a conflict naming the ids the client did not list when the keep-list omits them, and nothing is saved (RPP #1526).")]
    public async Task additional_youtube_keep_list_without_expected_ids_is_conflict()
    {
        // Arrange
        var existing = CreateAlternate();
        var (episode, podcast) = Resolved(e => e.AdditionalYouTube = [existing]);

        // Act
        var result = await Update(episode, podcast, new EpisodeChangeRequest
        {
            AdditionalYouTube = [new YouTubeAlternateChange(existing.YouTubeId, null, null, null)]
        });

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Conflict);
        result.UnexpectedAdditionalYouTubeIds.Should().Equal(existing.YouTubeId);
        result.Message.Should().Contain("episode");
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH episode additionalYouTube cannot add the episode's primary YouTube id, because that id is already the playable link (RPP #1526).")]
    public async Task additional_youtube_rejects_primary_id()
    {
        // Arrange
        var (episode, podcast) = Resolved();
        var primaryId = episode.Ids!.YouTube!;

        // Act
        var result = await Update(episode, podcast, SeenYouTube(episode, new EpisodeChangeRequest
        {
            AdditionalYouTube =
            [
                new YouTubeAlternateChange(null, _fixture.DefaultYouTubeUrl(primaryId).ToString(), null, null)
            ]
        }));

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.BadRequest);
        result.Message.Should().Contain(primaryId);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH episode promoteYouTubeId keeps the old primary as additionalYouTube[0] and moves ids.youtube when services.youtube is missing or cannot be parsed (RPP #1526).")]
    public async Task additional_youtube_promote_keeps_old_primary_when_url_is_missing_or_unparseable()
    {
        // Arrange
        var primaryId = _fixture.CreateYouTubeId();
        var alternate = CreateAlternate();
        var (episode, podcast) = Resolved(e =>
        {
            EpisodeServicePresence.SetYouTubeIdentity(e, primaryId);
            e.Services = null;
            e.AdditionalYouTube = [alternate];
        });

        // Act
        var missingUrl = await Update(episode, podcast, SeenYouTube(episode, new EpisodeChangeRequest
        {
            PromoteYouTubeId = alternate.YouTubeId
        }));
        var kept = _saved!.AdditionalYouTube!.ToList();
        var movedId = _saved.Ids!.YouTube;

        var (unparseable, unparseablePodcast) = Resolved(e =>
        {
            EpisodeServicePresence.SetYouTubeIdentity(e, primaryId);
            e.Services = new Dictionary<string, ServiceLink>
            {
                [ServiceKeys.YouTube] = new() { Url = new Uri("https://example.com/not-youtube") }
            };
            e.AdditionalYouTube = [alternate];
        });
        var unparseableResult = await Update(unparseable, unparseablePodcast, SeenYouTube(unparseable, new EpisodeChangeRequest
        {
            PromoteYouTubeId = alternate.YouTubeId
        }));

        // Assert
        missingUrl.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        movedId.Should().Be(alternate.YouTubeId);
        kept[0].YouTubeId.Should().Be(primaryId);
        kept[0].Url.Should().Be(_fixture.DefaultYouTubeUrl(primaryId));
        unparseableResult.Status.Should().Be(EpisodeUpdateStatus.Accepted);
        _saved!.Ids.YouTube.Should().Be(alternate.YouTubeId);
        _saved.AdditionalYouTube!.First().YouTubeId.Should().Be(primaryId);
        _saved.AdditionalYouTube!.First().Url.Should().Be(_fixture.DefaultYouTubeUrl(primaryId));
    }

    [Fact(DisplayName =
        "PATCH episode additionalYouTube is a bad request when it lists the YouTube id that services.youtube is being changed to in the same request, because the resulting primary cannot also be an alternate (RPP #1526).")]
    public async Task additional_youtube_rejects_id_that_becomes_the_primary_in_the_same_patch()
    {
        // Arrange
        var newPrimary = _fixture.CreateYouTubeId();
        var (episode, podcast) = Resolved();

        // Act
        var result = await Update(episode, podcast, new EpisodeChangeRequest
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
        result.Status.Should().Be(EpisodeUpdateStatus.BadRequest);
        result.Message.Should().Contain(newPrimary);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "PATCH episode additionalYouTube is a conflict naming the holder when the added YouTube id already belongs to another item (RPP #1526).")]
    public async Task additional_youtube_add_is_conflict_when_another_item_already_holds_the_id()
    {
        // Arrange
        var heldId = _fixture.CreateYouTubeId();
        var holderId = _fixture.CreateGuid();
        var holderName = _fixture.CreateTitle();
        var (episode, podcast) = Resolved();
        _mocker.GetMock<IUrlMembershipLookup>()
            .Setup(lookup => lookup.FindYouTubeHolderExcluding(
                heldId,
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogueServiceUrlOwner("Film", holderName, holderId));

        // Act
        var result = await Update(episode, podcast, new EpisodeChangeRequest
        {
            AdditionalYouTube =
            [
                new YouTubeAlternateChange(null, _fixture.DefaultYouTubeUrl(heldId).ToString(), null, null)
            ]
        });

        // Assert
        result.Status.Should().Be(EpisodeUpdateStatus.Conflict);
        result.Message.Should().Contain(heldId);
        result.Message.Should().Contain("Film");
        result.Message.Should().Contain(holderId.ToString());
        result.Message.Should().Contain(holderName);
        _saved.Should().BeNull();
    }

    [Fact(DisplayName =
        "Curator GET of a podcast episode includes additionalYouTube, because the editor reloads the list it will PATCH (RPP #1526).")]
    public async Task curator_get_includes_additional_youtube()
    {
        // Arrange
        var alternate = CreateAlternate();
        var (episode, podcast) = Resolved(e => e.AdditionalYouTube = [alternate]);
        var sut = _mocker.CreateInstance<EpisodeDtoMapper>();

        // Act
        var dto = await sut.ToDto(episode, podcast, Array.Empty<Subject>());

        // Assert
        dto.AdditionalYouTube.Should().ContainSingle().Which.YouTubeId.Should().Be(alternate.YouTubeId);
        dto.AdditionalYouTube![0].Url.Should().Be(alternate.Url);
    }

#pragma warning disable SYSLIB0050
    private static SearchClient CreateUninitializedSearchClient() =>
        (SearchClient)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SearchClient));
#pragma warning restore SYSLIB0050
}
