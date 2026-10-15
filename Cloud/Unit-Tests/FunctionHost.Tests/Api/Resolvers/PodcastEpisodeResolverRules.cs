using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using Moq.AutoMock;
using Api.Models;
using Api.Resolvers;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;
using Episode = RedditPodcastPoster.Models.Episodes.Episode;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;

namespace FunctionHost.Tests.Api.Resolvers;

/// <summary>
/// Name-first: a unique podcast name is used as before. Only when several podcasts share the name is the episode found by id.
/// Real case: two podcasts are named "Conversations"; GET episode/Conversations/{episodeId} returned 404.
/// </summary>
public class PodcastEpisodeResolverRules
{
    private readonly DomainTestFixture _fixture = new();
    private readonly AutoMocker _mocker = new();
    private readonly List<Podcast> _podcasts = [];
    private readonly List<Episode> _episodes = [];

    public PodcastEpisodeResolverRules()
    {
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetAllBy(It.IsAny<Expression<Func<Podcast, bool>>>()))
            .Returns((Expression<Func<Podcast, bool>> selector) =>
                ToAsyncEnumerable(_podcasts.Where(selector.Compile())));
        _mocker.GetMock<IPodcastRepository>()
            .Setup(r => r.GetPodcast(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => _podcasts.SingleOrDefault(x => x.Id == id));
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.GetEpisode(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((Guid podcastId, Guid episodeId) =>
                _episodes.SingleOrDefault(x => x.PodcastId == podcastId && x.Id == episodeId));
        _mocker.GetMock<IEpisodeRepository>()
            .Setup(r => r.GetBy(It.IsAny<Expression<Func<Episode, bool>>>()))
            .ReturnsAsync((Expression<Func<Episode, bool>> selector) =>
                _episodes.FirstOrDefault(selector.Compile()));
    }

    [Fact(DisplayName =
        "Plain English rule: when two podcasts share the requested name and the episode belongs to one of them, then resolve that episode and its podcast, because the episode-id is unique and the owning podcast carries the route name.")]
    public async Task resolve_by_name_with_duplicate_podcast_names_returns_episode_and_owning_podcast()
    {
        // Arrange
        var name = _fixture.CreateTitle();
        var other = AddPodcast(name);
        var owner = AddPodcast(name);
        var episode = AddEpisode(owner);
        var sut = _mocker.CreateInstance<PodcastEpisodeResolver>();

        // Act
        var result = await sut.ResolvePodcast(new PodcastEpisodeResolverRequest(episode.Id, null, name), "test");

        // Assert
        result.State.Should().Be(PodcastEpisodeResolveState.Resolved);
        result.Episode.Should().BeSameAs(episode);
        result.Podcast.Should().BeSameAs(owner);
        result.Podcast!.Id.Should().NotBe(other.Id);
    }

    [Fact(DisplayName =
        "Plain English rule: when exactly one podcast has the requested name, then read the episode within that podcast and never search across podcasts by episode-id, because the name already narrows the partition.")]
    public async Task resolve_by_unique_name_is_name_first_without_cross_partition_search()
    {
        // Arrange
        var owner = AddPodcast(_fixture.CreateTitle());
        var episode = AddEpisode(owner);
        var sut = _mocker.CreateInstance<PodcastEpisodeResolver>();

        // Act
        var result = await sut.ResolvePodcast(new PodcastEpisodeResolverRequest(episode.Id, null, owner.Name), "test");

        // Assert
        result.Episode.Should().BeSameAs(episode);
        result.Podcast.Should().BeSameAs(owner);
        _mocker.GetMock<IEpisodeRepository>()
            .Verify(r => r.GetBy(It.IsAny<Expression<Func<Episode, bool>>>()), Times.Never);
    }

    [Fact(DisplayName =
        "Plain English rule: when exactly one podcast has the requested name but the episode is not in it, then the episode is not found without searching by episode-id, because the name-first path is unchanged.")]
    public async Task resolve_by_unique_name_with_episode_of_another_podcast_is_not_found()
    {
        // Arrange
        var named = AddPodcast(_fixture.CreateTitle());
        var other = AddPodcast(_fixture.CreateTitle());
        var episode = AddEpisode(other);
        var sut = _mocker.CreateInstance<PodcastEpisodeResolver>();

        // Act
        var result = await sut.ResolvePodcast(new PodcastEpisodeResolverRequest(episode.Id, null, named.Name), "test");

        // Assert
        result.Episode.Should().BeNull();
        _mocker.GetMock<IEpisodeRepository>()
            .Verify(r => r.GetBy(It.IsAny<Expression<Func<Episode, bool>>>()), Times.Never);
    }

    [Fact(DisplayName =
        "Plain English rule: when no podcast has the requested name, then the podcast is not found, because there is no podcast to look in.")]
    public async Task resolve_by_name_that_matches_no_podcast_is_podcast_not_found()
    {
        // Arrange
        var owner = AddPodcast(_fixture.CreateTitle());
        var episode = AddEpisode(owner);
        var sut = _mocker.CreateInstance<PodcastEpisodeResolver>();

        // Act
        var result = await sut.ResolvePodcast(
            new PodcastEpisodeResolverRequest(episode.Id, null, _fixture.CreateTitle()), "test");

        // Assert
        result.State.Should().Be(PodcastEpisodeResolveState.PodcastNotFound);
        result.Episode.Should().BeNull();
        result.Podcast.Should().BeNull();
    }

    [Fact(DisplayName =
        "Plain English rule: when the podcast-id is the episode's podcast, then resolve it with point reads and no cross-partition search, because the podcast-id route is unchanged.")]
    public async Task resolve_by_correct_podcast_id_does_not_search_across_podcasts()
    {
        // Arrange
        var owner = AddPodcast(_fixture.CreateTitle());
        var episode = AddEpisode(owner);
        var sut = _mocker.CreateInstance<PodcastEpisodeResolver>();

        // Act
        var result = await sut.ResolvePodcast(new PodcastEpisodeResolverRequest(episode.Id, owner.Id), "test");

        // Assert
        result.Episode.Should().BeSameAs(episode);
        result.Podcast.Should().BeSameAs(owner);
        _mocker.GetMock<IEpisodeRepository>()
            .Verify(r => r.GetBy(It.IsAny<Expression<Func<Episode, bool>>>()), Times.Never);
    }

    [Fact(DisplayName =
        "Plain English rule: when the podcast-id is not the episode's podcast, then the episode is not found, because the podcast-id route is unchanged.")]
    public async Task resolve_by_wrong_podcast_id_is_not_found()
    {
        // Arrange
        var hinted = AddPodcast(_fixture.CreateTitle());
        var owner = AddPodcast(_fixture.CreateTitle());
        var episode = AddEpisode(owner);
        var sut = _mocker.CreateInstance<PodcastEpisodeResolver>();

        // Act
        var result = await sut.ResolvePodcast(new PodcastEpisodeResolverRequest(episode.Id, hinted.Id), "test");

        // Assert
        result.Episode.Should().BeNull();
    }

    [Fact(DisplayName =
        "Plain English rule: when only the episode-id is supplied, then resolve the episode and its podcast, because the id is unique.")]
    public async Task resolve_by_episode_id_only_returns_episode_and_owning_podcast()
    {
        // Arrange
        var owner = AddPodcast(_fixture.CreateTitle());
        var episode = AddEpisode(owner);
        var sut = _mocker.CreateInstance<PodcastEpisodeResolver>();

        // Act
        var result = await sut.ResolvePodcast(new PodcastEpisodeResolverRequest(episode.Id), "test");

        // Assert
        result.Episode.Should().BeSameAs(episode);
        result.Podcast.Should().BeSameAs(owner);
    }

    private Podcast AddPodcast(string name)
    {
        var podcast = _fixture.CreatePodcast(p => p.Name = name);
        _podcasts.Add(podcast);
        return podcast;
    }

    private Episode AddEpisode(Podcast podcast)
    {
        var episode = _fixture.CreateStoredEpisode(podcast);
        _episodes.Add(episode);
        return episode;
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(IEnumerable<T> source)
    {
        foreach (var item in source)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}