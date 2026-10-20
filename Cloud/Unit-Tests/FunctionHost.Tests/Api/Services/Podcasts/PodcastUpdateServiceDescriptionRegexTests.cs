using System.Linq.Expressions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Services.Podcasts;
using Azure.Search.Documents;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.UrlShortening.Services;
using Xunit;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api.Services.Podcasts;

public class PodcastUpdateServiceDescriptionRegexTests
{
    private const string OriginalDescriptionRegex = "^old(?<body>.*)$";

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly Podcast _podcast;

    public PodcastUpdateServiceDescriptionRegexTests()
    {
        _podcast = _fixture.CreatePodcast(p => p.DescriptionRegex = OriginalDescriptionRegex);
        _mocker.Use(CreateUninitializedSearchClient());
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .ReturnsAsync(() => _podcast);
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.Save(It.IsAny<Podcast>()))
            .Returns(Task.CompletedTask);
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.GetByPodcastId(It.IsAny<Guid>()))
            .Returns(() => AsyncEnumerable.Empty<Episode>());
        _mocker.GetMock<IShortnerService>();
        _mocker.GetMock<ILogger<PodcastChangeApplier>>();
        _mocker.GetMock<ILogger<PodcastUpdateService>>();
    }

    [Theory(DisplayName =
        "A description-regex containing the {episode-title}/{podcast-name} tokens (or regex quantifier braces) is " +
        "accepted and stored verbatim, because tokens are resolved per episode when the regex is applied.")]
    [InlineData("^\\s*{episode-title}\\s*[-\u2013\u2014:|]?\\s*(?<body>.*)$")]
    [InlineData(@"^{podcast-name}\s*(?<body>.*)$")]
    [InlineData(@"^x{2,3}(?<body>.*)$")]
    public async Task token_description_regex_is_accepted(string pattern)
    {
        // Arrange
        var service = _mocker.CreateInstance<PodcastUpdateService>();

        // Act
        var result = await service.UpdateAsync(
            new PodcastChangeRequestWrapper(_podcast.Id, new PodcastChangeRequest { DescriptionRegex = pattern }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.Accepted);
        _podcast.DescriptionRegex.Should().Be(pattern);
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.Save(_podcast), Times.Once);
    }

    [Fact(DisplayName = "A blank description-regex is accepted and clears the podcast's description regex.")]
    public async Task blank_description_regex_clears_it()
    {
        // Arrange
        var service = _mocker.CreateInstance<PodcastUpdateService>();

        // Act
        var result = await service.UpdateAsync(
            new PodcastChangeRequestWrapper(_podcast.Id, new PodcastChangeRequest { DescriptionRegex = string.Empty }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.Accepted);
        _podcast.DescriptionRegex.Should().BeEmpty();
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.Save(_podcast), Times.Once);
    }

    [Theory(DisplayName =
        "An invalid description-regex (with or without tokens) is rejected with BadRequest and nothing is saved.")]
    [InlineData(@"^{episode-title}(?<body>.*$")]
    [InlineData(@"^(?<body>.*$")]
    public async Task invalid_description_regex_is_rejected(string pattern)
    {
        // Arrange
        var service = _mocker.CreateInstance<PodcastUpdateService>();

        // Act
        var result = await service.UpdateAsync(
            new PodcastChangeRequestWrapper(_podcast.Id, new PodcastChangeRequest { DescriptionRegex = pattern }),
            CancellationToken.None);

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.BadRequest);
        result.Error.Should().StartWith("Invalid description-regex");
        _podcast.DescriptionRegex.Should().Be(OriginalDescriptionRegex);
        _mocker.GetMock<IPodcastRepository>().Verify(r => r.Save(It.IsAny<Podcast>()), Times.Never);
    }

#pragma warning disable SYSLIB0050
    private static SearchClient CreateUninitializedSearchClient() =>
        (SearchClient)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SearchClient));
#pragma warning restore SYSLIB0050
}
