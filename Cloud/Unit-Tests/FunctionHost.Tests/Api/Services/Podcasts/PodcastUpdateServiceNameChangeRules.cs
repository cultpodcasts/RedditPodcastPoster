using Api.Models;
using Api.Services.Podcasts;
using Azure.Search.Documents;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Persistence.Abstractions.FileKeys;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services.Podcasts;

public class PodcastUpdateServiceNameChangeRules
{
    private const string LatinName = "Synthetic Evening News";
    private const string OtherLatinName = "Synthetic Night News";
    private const string KoreanName = "오늘의 뉴스";

    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly InMemoryPodcastRepository _podcasts = new();

    public PodcastUpdateServiceNameChangeRules()
    {
        _mocker.Use<IPodcastRepository>(_podcasts);
        _mocker.Use(CreateUninitializedSearchClient());
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.GetByPodcastId(It.IsAny<Guid>()))
            .Returns(() => AsyncEnumerable.Empty<Episode>());
        _mocker.GetMock<ILogger<PodcastChangeApplier>>();
        _mocker.GetMock<ILogger<PodcastUpdateService>>();
        _mocker.Use<IFileKeyAllocator>(_mocker.CreateInstance<FileKeyAllocator>());
    }

    [Fact(DisplayName =
        "An edit that renames a podcast to a Korean-only name keeps its existing usable fileKey instead of storing an empty or underscore key.")]
    public async Task rename_to_non_latin_keeps_usable_key()
    {
        // Arrange
        var podcast = SeedPodcast(LatinName, FileKeyFactory.GetFileKey(LatinName));

        // Act
        var result = await Rename(podcast.Id, KoreanName);

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.Accepted);
        var stored = _podcasts.GetStored(podcast.Id);
        stored.Name.Should().Be(KoreanName);
        stored.FileKey.Should().Be(FileKeyFactory.GetFileKey(LatinName));
    }

    [Fact(DisplayName =
        "An edit that renames a podcast with an empty fileKey to a Korean-only name stores a key built from the podcast id.")]
    public async Task rename_to_non_latin_with_empty_key_gets_id_key()
    {
        // Arrange
        var podcast = SeedPodcast(LatinName, string.Empty);

        // Act
        var result = await Rename(podcast.Id, KoreanName);

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.Accepted);
        _podcasts.GetStored(podcast.Id).FileKey.Should().Be(FileKeyFactory.GetIdSlug(podcast.Id));
    }

    [Fact(DisplayName =
        "An edit that renames a podcast to a Latin name re-keys it from the new name, as it always has.")]
    public async Task rename_to_latin_rekeys()
    {
        // Arrange
        var podcast = SeedPodcast(LatinName, FileKeyFactory.GetFileKey(LatinName));

        // Act
        var result = await Rename(podcast.Id, OtherLatinName);

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.Accepted);
        _podcasts.GetStored(podcast.Id).FileKey.Should().Be(FileKeyFactory.GetFileKey(OtherLatinName));
    }

    [Fact(DisplayName =
        "An edit that renames a podcast to a name whose key another podcast already holds (any case) gets a numbered key instead of a duplicate.")]
    public async Task rename_to_taken_key_gets_suffix()
    {
        // Arrange
        SeedPodcast(_fixture.CreateTitle(), FileKeyFactory.GetFileKey(OtherLatinName).ToUpperInvariant());
        var podcast = SeedPodcast(LatinName, FileKeyFactory.GetFileKey(LatinName));

        // Act
        var result = await Rename(podcast.Id, OtherLatinName);

        // Assert
        result.Status.Should().Be(PodcastUpdateStatus.Accepted);
        _podcasts.GetStored(podcast.Id).FileKey.Should().Be(FileKeyFactory.GetFileKey(OtherLatinName) + "_2");
    }

    private RedditPodcastPoster.Models.Podcasts.Podcast SeedPodcast(string name, string fileKey)
    {
        var podcast = _fixture.CreatePodcast(p =>
        {
            p.Name = name;
            p.FileKey = fileKey;
        });
        _podcasts.Seed(podcast);
        return podcast;
    }

    private Task<PodcastUpdateResult> Rename(Guid podcastId, string name) =>
        _mocker.CreateInstance<PodcastUpdateService>().UpdateAsync(
            new PodcastChangeRequestWrapper(podcastId, new PodcastChangeRequest { Name = name }, AllowNameChange: true),
            CancellationToken.None);

#pragma warning disable SYSLIB0050
    private static SearchClient CreateUninitializedSearchClient() =>
        (SearchClient)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(SearchClient));
#pragma warning restore SYSLIB0050
}
