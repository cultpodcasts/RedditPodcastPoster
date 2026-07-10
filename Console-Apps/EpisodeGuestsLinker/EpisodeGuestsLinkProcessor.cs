using Microsoft.Extensions.Logging;
using RedditPodcastPoster.People;
using RedditPodcastPoster.Persistence.Abstractions;

namespace EpisodeGuestsLinker;

public class EpisodeGuestsLinkProcessor(
    IPersonRepository personRepository,
    IEpisodeRepository episodeRepository,
    ILogger<EpisodeGuestsLinkProcessor> logger)
{
    public async Task<int> Run(EpisodeGuestsLinkRequest request)
    {
        logger.LogInformation(
            "Mode: {Mode}. Loading People for handle → name map…",
            request.Apply ? "APPLY" : "DRY-RUN");

        var people = await personRepository.GetAll().ToListAsync();
        var conflictCount = 0;
        var handleToName = EpisodeGuestHandleLinker.BuildHandleToNameMap(
            people,
            (handle, first, second) =>
            {
                conflictCount++;
                logger.LogWarning(
                    "Handle conflict for {Handle}: keeping '{First}', ignoring '{Second}'.",
                    handle, first, second);
            });

        logger.LogInformation(
            "People: {PeopleCount}. Handle map entries: {HandleCount}. Conflicts: {Conflicts}.",
            people.Count,
            handleToName.Count,
            conflictCount);

        var episodesScanned = 0;
        var episodesWithHandles = 0;
        var episodesWouldUpdate = 0;
        var episodesUpdated = 0;
        var episodesSkippedNoNewGuests = 0;
        var failures = 0;
        var guestsAddedTotal = 0;
        var samples = new List<string>();
        var sampleLimit = Math.Max(0, request.Sample);

        await foreach (var episode in episodeRepository.GetAllBy(e =>
                           e.TwitterHandles != null || e.BlueskyHandles != null))
        {
            episodesScanned++;
            var hasHandles =
                (episode.TwitterHandles is { Length: > 0 }) ||
                (episode.BlueskyHandles is { Length: > 0 });
            if (!hasHandles)
            {
                continue;
            }

            episodesWithHandles++;
            var match = EpisodeGuestHandleLinker.Match(episode, handleToName);
            if (!match.HasAdditions)
            {
                episodesSkippedNoNewGuests++;
                continue;
            }

            episodesWouldUpdate++;
            guestsAddedTotal += match.GuestsToAdd.Length;

            if (samples.Count < sampleLimit)
            {
                var existingGuests = episode.Guests is { Length: > 0 }
                    ? string.Join(", ", episode.Guests)
                    : "(none)";
                var title = string.IsNullOrWhiteSpace(episode.Title) ? "(no title)" : episode.Title;
                samples.Add(
                    $"episodeId={episode.Id} | title={title} | matched=[{string.Join(", ", match.MatchedHandles)}] | add=[{string.Join(", ", match.GuestsToAdd)}] | existing=[{existingGuests}]");
            }

            if (!request.Apply)
            {
                continue;
            }

            try
            {
                await episodeRepository.PatchGuests(
                    episode.PodcastId,
                    episode.Id,
                    match.ResultingGuests);
                episodesUpdated++;
            }
            catch (Exception ex)
            {
                failures++;
                logger.LogError(
                    ex,
                    "Failed to patch guests for episode {EpisodeId} (podcast {PodcastId}).",
                    episode.Id,
                    episode.PodcastId);
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== Episode guests linker summary ===");
        Console.WriteLine($"Mode:                 {(request.Apply ? "APPLY" : "DRY-RUN")}");
        Console.WriteLine($"People loaded:        {people.Count}");
        Console.WriteLine($"Handle map size:      {handleToName.Count}");
        Console.WriteLine($"Handle conflicts:     {conflictCount}");
        Console.WriteLine($"Episodes scanned:     {episodesScanned} (twitterHandles or blueskyHandles present)");
        Console.WriteLine($"Episodes w/ handles:  {episodesWithHandles}");
        Console.WriteLine($"Would update:         {episodesWouldUpdate}");
        Console.WriteLine($"Skipped (no new):     {episodesSkippedNoNewGuests}");
        Console.WriteLine($"Guest names to add:   {guestsAddedTotal} (sum across episodes)");

        if (samples.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"Samples ({samples.Count}):");
            foreach (var sample in samples)
            {
                Console.WriteLine($"  {sample}");
            }
        }

        if (request.Apply)
        {
            Console.WriteLine();
            Console.WriteLine($"Patched:              {episodesUpdated}");
            Console.WriteLine($"Failures:             {failures}");
            logger.LogInformation(
                "Apply complete. Updated={Updated}, Failures={Failures}",
                episodesUpdated,
                failures);
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine(
                "No writes performed. Re-run with --apply to surgically patch /guests only (handles preserved).");
        }

        return failures > 0 ? 2 : 0;
    }
}
