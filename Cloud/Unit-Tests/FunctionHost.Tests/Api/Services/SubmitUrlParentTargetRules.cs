using System.Text;
using System.Text.Json;
using Azure.Core.Serialization;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.SubmitUrl;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Catalogue;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.PodcastServices.Abstractions.Models;
using RedditPodcastPoster.UrlSubmission.Categorisation;
using RedditPodcastPoster.UrlSubmission.Models;
using RedditPodcastPoster.UrlSubmission.Submitters;
using Xunit;

namespace FunctionHost.Tests.Api.Services;

/// <summary>
/// The website hub's Submit URL contract (website #530):
/// <c>POST /submit { url, parentKind: "NewsOrganisation" | "TvShow", parentId, parentName }</c> with no podcast fields.
/// Files by parentId, never creates a podcast; 404 for a missing parent; 400 for an unknown kind or parent and podcast
/// fields together.
/// </summary>
public class SubmitUrlParentTargetRules
{
    private static readonly JsonObjectSerializer IsolatedSerializer = new(new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private SubmitOptions? _capturedOptions;

    public SubmitUrlParentTargetRules()
    {
        _mocker.GetMock<IUrlSubmitter>()
            .Setup(s => s.Submit(It.IsAny<Uri>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()))
            .ReturnsAsync((Uri _, IndexingContext _, SubmitOptions options) =>
            {
                _capturedOptions = options;
                return new SubmitResult(SubmitResultState.Created, SubmitResultState.None,
                    ContentKind: SubmitClassification.NewsReport, PlayableId: Guid.NewGuid());
            });
    }

    private Uri YouTubeUrl() => new($"https://www.youtube.com/watch?v={_fixture.CreateYouTubeId()}");

    private Task<SubmitUrlResult> Submit(SubmitUrlRequest request) =>
        _mocker.CreateInstance<SubmitUrlService>().SubmitAsync(request, CancellationToken.None);

    private static async Task<SubmitUrlRequest> BindAsync(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var bound = (global::Api.Dtos.SubmitUrlRequest)(await IsolatedSerializer.DeserializeAsync(
            stream, typeof(global::Api.Dtos.SubmitUrlRequest), CancellationToken.None))!;
        return bound.ToModel();
    }

    [Theory(DisplayName =
        "The website body { url, parentKind, parentId, parentName } binds and is filed under that parent by id with no podcast fields, for both kinds and any casing of the kind.")]
    [InlineData("NewsOrganisation", CatalogueParentKind.NewsOrganisation)]
    [InlineData("TvShow", CatalogueParentKind.TvShow)]
    [InlineData("newsOrganisation", CatalogueParentKind.NewsOrganisation)]
    [InlineData("tvshow", CatalogueParentKind.TvShow)]
    public async Task website_body_targets_parent(string parentKind, CatalogueParentKind expected)
    {
        // Arrange
        var url = YouTubeUrl();
        var parentId = _fixture.CreateGuid();
        var parentName = _fixture.CreateTitle();
        var request = await BindAsync(JsonSerializer.Serialize(new { url, parentKind, parentId, parentName }));

        // Act
        var result = await Submit(request);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.Ok);
        _capturedOptions!.Parent.Should().Be(new CatalogueParentTarget(expected, parentId, parentName));
        _capturedOptions.PodcastId.Should().BeNull();
        _capturedOptions.PodcastName.Should().BeNull();
        _capturedOptions.CreatePodcast.Should().BeFalse();
        _mocker.GetMock<IPodcastRepository>().VerifyNoOtherCalls();
    }

    [Fact(DisplayName =
        "Parent and podcast fields together are a 400 and nothing is submitted, because the caller must pick one target.")]
    public async Task parent_and_podcast_fields_are_bad_request()
    {
        // Arrange
        var request = new SubmitUrlRequest
        {
            Url = YouTubeUrl(), ParentKind = "NewsOrganisation", ParentId = _fixture.CreateGuid(),
            PodcastName = _fixture.CreateTitle()
        };

        // Act
        var result = await Submit(request);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.BadRequest);
        result.Message.Should().Contain("not both");
        _mocker.GetMock<IUrlSubmitter>().Verify(
            s => s.Submit(It.IsAny<Uri>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()), Times.Never);
    }

    [Theory(DisplayName =
        "An unknown or numeric parentKind, or a parent with no parentId, is a 400 and nothing is submitted.")]
    [InlineData("Podcast", true)]
    [InlineData("1", true)]
    [InlineData("Film", true)]
    [InlineData("TvShow", false)]
    public async Task invalid_parent_target_is_bad_request(string parentKind, bool withId)
    {
        // Arrange
        var request = new SubmitUrlRequest
        {
            Url = YouTubeUrl(), ParentKind = parentKind, ParentId = withId ? _fixture.CreateGuid() : null,
            ParentName = _fixture.CreateTitle()
        };

        // Act
        var result = await Submit(request);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.BadRequest);
        result.Message.Should().NotBeNullOrWhiteSpace();
        _mocker.GetMock<IUrlSubmitter>().Verify(
            s => s.Submit(It.IsAny<Uri>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()), Times.Never);
    }

    [Fact(DisplayName =
        "A parent target with a URL that is not Spotify, YouTube or Apple Podcasts is a 400, because only those can be filed under a news organisation or TV show.")]
    public async Task non_platform_url_with_parent_is_bad_request()
    {
        // Arrange
        var request = new SubmitUrlRequest
        {
            Url = new Uri($"https://example.com/{_fixture.CreateYouTubeId()}"), ParentKind = "TvShow",
            ParentId = _fixture.CreateGuid()
        };

        // Act
        var result = await Submit(request);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.BadRequest);
    }

    [Fact(DisplayName =
        "A parentId that is not a row of parentKind is ParentNotFound with a message naming the kind (the handler maps it to 404).")]
    public async Task missing_parent_is_parent_not_found()
    {
        // Arrange
        var parentId = _fixture.CreateGuid();
        _mocker.GetMock<IUrlSubmitter>()
            .Setup(s => s.Submit(It.IsAny<Uri>(), It.IsAny<IndexingContext>(), It.IsAny<SubmitOptions>()))
            .ThrowsAsync(new SubmitParentNotFoundException(CatalogueParentKind.NewsOrganisation, parentId));
        var request = new SubmitUrlRequest
            { Url = YouTubeUrl(), ParentKind = "NewsOrganisation", ParentId = parentId };

        // Act
        var result = await Submit(request);

        // Assert
        result.Status.Should().Be(SubmitUrlStatus.ParentNotFound);
        result.Message.Should().Contain("NewsOrganisation");
    }

    [Fact(DisplayName =
        "SubmitUrlService requires the playable search indexer and the Api's search registration provides it, so a URL filed under a parent is always indexed.")]
    public void playable_search_is_required_and_registered()
    {
        // Arrange
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();

        // Act
        RedditPodcastPoster.EntitySearchIndexer.Extensions.ServiceCollectionExtensions.AddEpisodeSearchIndexerService(services);
        var parameter = typeof(SubmitUrlService).GetConstructors().Single().GetParameters()
            .Single(p => p.ParameterType == typeof(RedditPodcastPoster.EntitySearchIndexer.Services.IPlayableSearchDocumentSwap));

        // Assert
        parameter.IsOptional.Should().BeFalse();
        services.Should().Contain(d =>
            d.ServiceType == typeof(RedditPodcastPoster.EntitySearchIndexer.Services.IPlayableSearchDocumentSwap));
    }
}
