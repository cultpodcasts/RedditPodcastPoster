using RedditPodcastPoster.Models;

namespace PeopleMigrator;

/// <summary>
/// Builds a deduplicated People register from episode guest handle fields.
/// Does not read or write episode documents.
/// </summary>
internal static class PeopleFromGuestHandlesBuilder
{
    public static PeopleBuildResult Build(
        IEnumerable<GuestHandleEpisode> episodes,
        PersonMigrationRegistry registry,
        IReadOnlySet<Guid>? existingPersonIds = null,
        EpisodeBackupLoader? backupLoader = null)
    {
        var episodesScanned = 0;

        foreach (var episode in episodes)
        {
            if (!HasGuestHandles(episode))
            {
                continue;
            }

            episodesScanned++;
            LinkAlignedPairs(episode, registry);
            ResolveEpisodeGuests(episode, registry);
            EnrichFromEpisodeDescription(episode, registry, backupLoader);
        }

        var pendingPeople = registry.EnumeratePersons()
            .Where(x => existingPersonIds == null || !existingPersonIds.Contains(x.Id))
            .ToDictionary(x => x.Id);

        return new PeopleBuildResult(episodesScanned, pendingPeople, registry);
    }

    public static AliasEnrichmentBuildResult EnrichAliases(
        IEnumerable<GuestHandleEpisode> episodes,
        PersonMigrationRegistry registry,
        EpisodeBackupLoader backupLoader)
    {
        var episodesTotal = 0;
        var episodesProcessed = 0;
        var aliasesAdded = 0;
        var peopleWithNewAliases = new HashSet<Guid>();
        var examples = new List<AliasEnrichmentExample>();

        foreach (var episode in episodes)
        {
            if (!HasGuestHandles(episode))
            {
                continue;
            }

            episodesTotal++;
            if (episode.EpisodeId is not Guid episodeId)
            {
                continue;
            }

            var snapshot = backupLoader.TryLoad(episodeId);
            if (snapshot == null)
            {
                continue;
            }

            episodesProcessed++;
            var twitters = PersonHandleNormalizer.ExpandHandles(episode.TwitterHandles).ToList();
            var blueskys = PersonHandleNormalizer.ExpandHandles(episode.BlueskyHandles).ToList();
            var episodeHandles = twitters.Concat(blueskys).ToList();

            for (var i = 0; i < twitters.Count; i++)
            {
                var twitter = twitters[i];
                var bluesky = i < blueskys.Count ? blueskys[i] : null;
                aliasesAdded += EnrichPersonAliases(
                    registry,
                    twitter,
                    bluesky,
                    snapshot.Value.Title,
                    snapshot.Value.Description,
                    episodeHandles,
                    episodeId,
                    peopleWithNewAliases,
                    examples);
            }

            for (var i = 0; i < blueskys.Count; i++)
            {
                if (i < twitters.Count)
                {
                    continue;
                }

                aliasesAdded += EnrichPersonAliases(
                    registry,
                    null,
                    blueskys[i],
                    snapshot.Value.Title,
                    snapshot.Value.Description,
                    episodeHandles,
                    episodeId,
                    peopleWithNewAliases,
                    examples);
            }
        }

        return new AliasEnrichmentBuildResult(
            episodesProcessed,
            episodesTotal,
            aliasesAdded,
            peopleWithNewAliases.Count,
            examples
                .OrderByDescending(x => x.AddedAliases.Count)
                .ThenBy(x => x.CanonicalName, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    private static int EnrichPersonAliases(
        PersonMigrationRegistry registry,
        string? twitter,
        string? bluesky,
        string? title,
        string? description,
        IReadOnlyList<string> episodeHandles,
        Guid episodeId,
        HashSet<Guid> peopleWithNewAliases,
        List<AliasEnrichmentExample> examples)
    {
        var person = registry.FindExistingPublic(twitter, bluesky);
        if (person == null || string.IsNullOrWhiteSpace(person.Name))
        {
            return 0;
        }

        var beforeAliases = person.Aliases?.ToArray() ?? [];
        var aliases = EpisodeAliasExtractor.ExtractAliases(
            person.Name,
            title,
            description,
            person.TwitterHandle ?? twitter,
            person.BlueskyHandle ?? bluesky,
            episodeHandles);

        var added = registry.ApplyAliasExtract(twitter, bluesky, aliases, episodeId);
        if (added <= 0)
        {
            return 0;
        }

        peopleWithNewAliases.Add(person.Id);
        var afterAliases = person.Aliases ?? [];
        var addedAliases = afterAliases
            .Except(beforeAliases, StringComparer.OrdinalIgnoreCase)
            .ToList();

        examples.Add(new AliasEnrichmentExample(
            person.Name,
            addedAliases,
            afterAliases));

        return added;
    }

    private static bool HasGuestHandles(GuestHandleEpisode episode)
    {
        return episode.TwitterHandles is { Length: > 0 } ||
               episode.BlueskyHandles is { Length: > 0 };
    }

    private static void LinkAlignedPairs(GuestHandleEpisode episode, PersonMigrationRegistry registry)
    {
        var twitters = PersonHandleNormalizer.ExpandHandles(episode.TwitterHandles).ToList();
        var blueskys = PersonHandleNormalizer.ExpandHandles(episode.BlueskyHandles).ToList();
        if (twitters.Count == 0 || blueskys.Count == 0 || twitters.Count != blueskys.Count)
        {
            return;
        }

        for (var i = 0; i < twitters.Count; i++)
        {
            var twitter = twitters[i];
            var bluesky = blueskys[i];
            if (string.IsNullOrWhiteSpace(twitter) && string.IsNullOrWhiteSpace(bluesky))
            {
                continue;
            }

            registry.LinkPair(twitter, bluesky);
        }
    }

    private static void ResolveEpisodeGuests(GuestHandleEpisode episode, PersonMigrationRegistry registry)
    {
        foreach (var twitter in PersonHandleNormalizer.ExpandHandles(episode.TwitterHandles))
        {
            registry.Resolve(twitter, null);
        }

        foreach (var bluesky in PersonHandleNormalizer.ExpandHandles(episode.BlueskyHandles))
        {
            registry.Resolve(null, bluesky);
        }
    }

    private static void EnrichFromEpisodeDescription(
        GuestHandleEpisode episode,
        PersonMigrationRegistry registry,
        EpisodeBackupLoader? backupLoader)
    {
        if (backupLoader == null || episode.EpisodeId is not Guid episodeId)
        {
            return;
        }

        var snapshot = backupLoader.TryLoad(episodeId);
        if (snapshot == null)
        {
            return;
        }

        var twitters = PersonHandleNormalizer.ExpandHandles(episode.TwitterHandles).ToList();
        var blueskys = PersonHandleNormalizer.ExpandHandles(episode.BlueskyHandles).ToList();
        var episodeHandles = twitters.Concat(blueskys).ToList();
        var extractions = EpisodeGuestNameExtractor.ExtractForEpisode(
            snapshot.Value.Title,
            snapshot.Value.Description,
            twitters,
            blueskys)
            .ToDictionary(
                x => PersonHandleNormalizer.NormalizeExactHandle(x.Handle)!,
                x => x,
                StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < twitters.Count; i++)
        {
            ProcessEpisodeHandle(
                registry,
                twitters[i],
                i < blueskys.Count ? blueskys[i] : null,
                snapshot.Value.Title,
                snapshot.Value.Description,
                episodeHandles,
                extractions,
                episodeId);
        }

        for (var i = 0; i < blueskys.Count; i++)
        {
            if (i < twitters.Count)
            {
                continue;
            }

            ProcessEpisodeHandle(
                registry,
                null,
                blueskys[i],
                snapshot.Value.Title,
                snapshot.Value.Description,
                episodeHandles,
                extractions,
                episodeId);
        }
    }

    private static void ProcessEpisodeHandle(
        PersonMigrationRegistry registry,
        string? twitter,
        string? bluesky,
        string? title,
        string? description,
        IReadOnlyList<string> episodeHandles,
        IReadOnlyDictionary<string, HandleNameExtraction> extractions,
        Guid episodeId)
    {
        var person = registry.FindExistingPublic(twitter, bluesky);
        if (person != null && HasResolvedCanonicalName(person))
        {
            var aliases = EpisodeAliasExtractor.ExtractAliases(
                person.Name,
                title,
                description,
                person.TwitterHandle ?? twitter,
                person.BlueskyHandle ?? bluesky,
                episodeHandles);

            registry.ApplyAliasExtract(twitter, bluesky, aliases, episodeId);
            return;
        }

        var handleKey = PersonHandleNormalizer.NormalizeExactHandle(twitter ?? bluesky);
        if (handleKey != null && extractions.TryGetValue(handleKey, out var extraction))
        {
            registry.ApplyDescriptionExtract(
                twitter,
                bluesky,
                extraction.DisplayName,
                extraction.Aliases,
                episodeId);
        }
    }

    private static bool HasResolvedCanonicalName(Person person)
    {
        if (string.IsNullOrWhiteSpace(person.Name) ||
            person.Name.Equals("Unknown guest", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var derivedName = PersonHandleNormalizer.DeriveDisplayName(person.TwitterHandle, person.BlueskyHandle);
        if (person.Name.Equals(derivedName, StringComparison.OrdinalIgnoreCase) && !person.Name.Contains(' '))
        {
            return false;
        }

        return true;
    }
}

internal sealed record PeopleBuildResult(
    int EpisodesScanned,
    Dictionary<Guid, Person> PendingPeople,
    PersonMigrationRegistry Registry);

internal sealed record AliasEnrichmentBuildResult(
    int EpisodesProcessed,
    int EpisodesTotal,
    int AliasesAdded,
    int PeopleWithNewAliases,
    IReadOnlyList<AliasEnrichmentExample> Examples);
