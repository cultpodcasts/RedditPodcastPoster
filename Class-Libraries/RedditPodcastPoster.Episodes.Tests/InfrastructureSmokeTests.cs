using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;

namespace RedditPodcastPoster.Episodes.Tests;

public class InfrastructureSmokeTests
{
    private readonly DomainTestFixture _fixture = new();

    [Fact(DisplayName = "In-memory episode repository seeds episodes and records batch saves for test assertions.")]
    public async Task In_memory_episode_repository_records_saves()
    {
        // Arrange
        var recorder = new SaveCallRecorder();
        var repository = new InMemoryEpisodeRepository(recorder);
        var episode = _fixture.CreateEpisode(e => e.Title = "Smoke test episode");
        repository.Seed(episode);

        // Act
        episode.Title = "Updated title";
        await repository.Save([episode]);

        // Assert
        repository.SavedEpisodes.Should().ContainSingle();
        repository.SavedEpisodes.Single().Title.Should().Be("Updated title");
        repository.GetStored(episode.Id).Title.Should().Be("Updated title");
        recorder.EpisodeCalls.Should().ContainSingle();
        recorder.EpisodeCalls.Single().Kind.Should().Be(EpisodeSaveKind.Batch);
        recorder.EpisodeCalls.Single().EpisodeIds.Should().ContainSingle().Which.Should().Be(episode.Id);

        var filtered = repository.GetByPodcastId(
            episode.PodcastId,
            x => x.Title == "Updated title");
        var matches = await filtered.ToListAsync();
        matches.Should().ContainSingle();
    }
}
