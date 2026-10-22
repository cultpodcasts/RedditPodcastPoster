using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.AutoMock;
using RedditPodcastPoster.Cloudflare.Clients;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlShortening.Configuration;
using RedditPodcastPoster.UrlShortening.Services;
using Xunit;

namespace Indexer.Tests.BusinessRules;

public class ShortnerRedirectRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<KVRecord> _written = [];

    public ShortnerRedirectRules()
    {
        _mocker.Use(Options.Create(new ShortnerOptions
        {
            ShortnerUrl = new Uri("https://short.example.test/"),
            KVShortnerNamespaceId = _fixture.CreateGuid().ToString("N")
        }));
        _mocker.GetMock<IKVClient>()
            .Setup(c => c.Write(It.IsAny<KVRecord>(), It.IsAny<string>()))
            .Callback<KVRecord, string>((record, _) => _written.Add(record))
            .ReturnsAsync(new WriteResult(true));
    }

    [Fact(DisplayName =
        "Redirect writes the episode key with the given site path as its value and keeps the episode share metadata, so an old link lands on the new page.")]
    public async Task redirect_writes_site_path_under_episode_key()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);
        var path = $"/film/{_fixture.CreateYouTubeId()}/{_fixture.CreateYouTubeId()}";

        // Act
        var result = await _mocker.CreateInstance<ShortnerService>()
            .Redirect(new PodcastEpisode(podcast, episode), path);

        // Assert
        result.Success.Should().BeTrue();
        var record = _written.Should().ContainSingle().Subject;
        record.Key.Should().Be(episode.Id.ToBase64());
        record.Value.Should().Be(path);
        record.Metadata!.EpisodeTitle.Should().Be(episode.Title);
    }

    [Fact(DisplayName =
        "Redirect refuses a path that is not root-relative, because the Shortner would resolve it under the podcast route.")]
    public async Task redirect_requires_root_relative_path()
    {
        // Arrange
        var podcast = _fixture.CreatePodcast();
        var episode = _fixture.CreateStoredEpisodeWithYouTubeOnly(podcast);

        // Act
        var act = () => _mocker.CreateInstance<ShortnerService>()
            .Redirect(new PodcastEpisode(podcast, episode), $"film/{_fixture.CreateYouTubeId()}");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
        _written.Should().BeEmpty();
    }
}
