using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.People;

/// <summary>
/// Links guest handles to Person canonical names.
/// Expands space-delimited handles, normalizes '@', and matches case-insensitively.
/// Never removes existing guests.
/// </summary>
/// <remarks>
/// Episode-level <c>twitterHandles</c> / <c>blueskyHandles</c> are retired from the model.
/// Pass handle arrays explicitly (e.g. from backup JSON). The EpisodeGuestsLinker console
/// app can no longer discover handles via the Episode model. Subsequent Save() upserts may
/// drop orphan handle JSON from Cosmos — accepted as part of retirement.
/// </remarks>
public static class EpisodeGuestHandleLinker
{
    /// <summary>
    /// Builds handle (without leading '@', case-insensitive) → Person.Name.
    /// First person wins on conflicting handles; <paramref name="onConflict"/> is notified.
    /// </summary>
    public static IReadOnlyDictionary<string, string> BuildHandleToNameMap(
        IEnumerable<Person> people,
        Action<string, string, string>? onConflict = null)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var person in people)
        {
            if (string.IsNullOrWhiteSpace(person.Name))
            {
                continue;
            }

            var handles = SocialHandleDeduplicator.Deduplicate(
                [person.TwitterHandle, person.BlueskyHandle]);
            foreach (var handle in handles)
            {
                var key = ToComparisonKey(handle);
                if (map.TryGetValue(key, out var existingName) &&
                    !string.Equals(existingName, person.Name.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    onConflict?.Invoke(handle, existingName, person.Name.Trim());
                    continue;
                }

                map[key] = person.Name.Trim();
            }
        }

        return map;
    }

    /// <summary>
    /// Resolves guests to add from the given twitter/bluesky handles against the person handle map.
    /// Does not mutate <paramref name="episode"/>.
    /// </summary>
    public static EpisodeGuestLinkMatch Match(
        Episode episode,
        IEnumerable<string>? twitterHandles,
        IEnumerable<string>? blueskyHandles,
        IReadOnlyDictionary<string, string> handleToName)
    {
        var episodeHandles = SocialHandleDeduplicator.Deduplicate(
            (twitterHandles ?? []).Concat(blueskyHandles ?? []));

        if (episodeHandles.Length == 0)
        {
            return EpisodeGuestLinkMatch.Empty;
        }

        var matchedHandles = new List<string>();
        var matchedNamesInOrder = new List<string>();
        var matchedNameKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var handle in episodeHandles)
        {
            if (!handleToName.TryGetValue(ToComparisonKey(handle), out var name))
            {
                continue;
            }

            matchedHandles.Add(handle);
            if (matchedNameKeys.Add(name))
            {
                matchedNamesInOrder.Add(name);
            }
        }

        if (matchedNamesInOrder.Count == 0)
        {
            return new EpisodeGuestLinkMatch([], matchedHandles.ToArray(), episode.Guests?.ToArray() ?? []);
        }

        var existing = episode.Guests ?? [];
        var seen = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        var guestsToAdd = new List<string>();
        foreach (var name in matchedNamesInOrder)
        {
            if (seen.Add(name))
            {
                guestsToAdd.Add(name);
            }
        }

        var resultingGuests = existing.Concat(guestsToAdd).ToArray();
        return new EpisodeGuestLinkMatch(
            guestsToAdd.ToArray(),
            matchedHandles.ToArray(),
            resultingGuests);
    }

    private static string ToComparisonKey(string handle) => handle.TrimStart('@');
}

public sealed record EpisodeGuestLinkMatch(
    string[] GuestsToAdd,
    string[] MatchedHandles,
    string[] ResultingGuests)
{
    public static EpisodeGuestLinkMatch Empty { get; } = new([], [], []);

    public bool HasAdditions => GuestsToAdd.Length > 0;
}
