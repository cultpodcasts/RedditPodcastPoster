using FluentAssertions;
using RedditPodcastPoster.Episodes.TestSupport.Fakes;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Episodes.Tests;

public class InfrastructureSmokeTests
{
    [Fact(DisplayName = "In-memory episode repository seeds episodes and records batch saves for test assertions.")]
    public async Task In_memory_episode_repository_records_saves()
    {
        // Given a seeded episode repository with save-call recording
        var recorder = new SaveCallRecorder();
        var repository = new InMemoryEpisodeRepository(recorder);
        var episode = new Episode
        {
            Id = Guid.NewGuid(),
            PodcastId = Guid.NewGuid(),
            Title = "Smoke test episode"
        };
        repository.Seed(episode);

        // When a batch save updates the episode title
        episode.Title = "Updated title";
        await repository.Save([episode]);

        // Then the repository exposes the saved snapshot and records the batch call
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
