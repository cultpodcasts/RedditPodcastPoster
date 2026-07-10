using FluentAssertions;
using RedditPodcastPoster.People.Factories;
using Xunit;

namespace PeopleMigrator.Tests;

public class PeopleFromGuestHandlesBuilderTests
{
    [Fact]
    public void Build_deduplicates_same_handle_across_episodes()
    {
        var registry = new PersonMigrationRegistry(new PersonFactory());
        var episodes = new[]
        {
            new GuestHandleEpisode(["@alice"], ["@alice.bsky.social"]),
            new GuestHandleEpisode(["@alice"], null),
        };

        var result = PeopleFromGuestHandlesBuilder.Build(episodes, registry);

        result.EpisodesScanned.Should().Be(2);
        result.PendingPeople.Should().ContainSingle();
        result.PendingPeople.Values.Single().TwitterHandle.Should().Be("@alice");
        result.PendingPeople.Values.Single().BlueskyHandle.Should().Be("@alice.bsky.social");
    }

    [Fact]
    public void Build_links_aligned_index_pairs()
    {
        var registry = new PersonMigrationRegistry(new PersonFactory());
        var episodes = new[]
        {
            new GuestHandleEpisode(["@bob"], ["@bob.bsky.social"]),
        };

        var result = PeopleFromGuestHandlesBuilder.Build(episodes, registry);

        result.PendingPeople.Should().ContainSingle();
        var person = result.PendingPeople.Values.Single();
        person.TwitterHandle.Should().Be("@bob");
        person.BlueskyHandle.Should().Be("@bob.bsky.social");
    }

    [Fact]
    public void Build_enriches_names_from_episode_backup()
    {
        var backupPath = Path.Combine(Path.GetTempPath(), $"people-seeder-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(backupPath);

        try
        {
            var episodeId = Guid.NewGuid();
            var episodeJson =
                $$"""
                  {
                    "id": "{{episodeId}}",
                    "title": "Epstein came to my office, says Tina Brown",
                    "description": "👤 Guest: Tina Brown\n🎙️ Host: Sky News",
                    "twitterHandles": ["@TinaBrownLM"],
                    "blueskyHandles": ["@tinabrownlm.bsky.social"]
                  }
                  """;
            File.WriteAllText(Path.Combine(backupPath, $"{episodeId}.json"), episodeJson);

            var registry = new PersonMigrationRegistry(new PersonFactory());
            var episodes = new[]
            {
                new GuestHandleEpisode(["@TinaBrownLM"], ["@tinabrownlm.bsky.social"], episodeId)
            };
            var loader = new EpisodeBackupLoader(backupPath);

            var result = PeopleFromGuestHandlesBuilder.Build(episodes, registry, backupLoader: loader);

            result.PendingPeople.Should().ContainSingle();
            var person = result.PendingPeople.Values.Single();
            person.Name.Should().Be("Tina Brown");
            result.Registry.GetMetadata(person).DescriptionExtractedName.Should().Be("Tina Brown");
            result.Registry.GetMetadata(person).SourceEpisodeIds.Should().Contain(episodeId);
        }
        finally
        {
            Directory.Delete(backupPath, true);
        }
    }

    [Fact]
    public void Build_skips_episodes_without_handles()
    {
        var registry = new PersonMigrationRegistry(new PersonFactory());
        var episodes = new[]
        {
            new GuestHandleEpisode(null, null),
            new GuestHandleEpisode([], []),
            new GuestHandleEpisode(["@onlyone"], null)
        };

        var result = PeopleFromGuestHandlesBuilder.Build(episodes, registry);

        result.EpisodesScanned.Should().Be(1);
        result.PendingPeople.Should().ContainSingle();
    }
}
