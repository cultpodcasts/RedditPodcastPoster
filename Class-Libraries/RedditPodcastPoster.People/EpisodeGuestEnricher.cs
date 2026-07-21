using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.People;
using RedditPodcastPoster.People.Models;

namespace RedditPodcastPoster.People;

public class EpisodeGuestEnricher(
    IPersonService personService,
    ILogger<EpisodeGuestEnricher> logger)
    : IEpisodeGuestEnricher
{
    public async Task<EnrichGuestsResult> EnrichGuests(
        Episode episode,
        GuestEnrichmentOptions? options = null)
    {
        options ??= GuestEnrichmentOptions.Default;

        var matches = await personService.MatchEpisode(
            episode,
            withDescription: !options.TitleOnly);

        var existing = episode.Guests?.ToHashSet(StringComparer.OrdinalIgnoreCase)
                       ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var additions = new List<PersonMatch>();
        var skipped = new List<PersonMatch>();

        foreach (var match in matches)
        {
            if (existing.Contains(match.Person.Name))
            {
                continue;
            }

            if (!IsHighConfidence(match, options))
            {
                skipped.Add(match);
                continue;
            }

            additions.Add(match);
            existing.Add(match.Person.Name);
        }

        if (additions.Count > 0)
        {
            var addedNames = additions.Select(x => x.Person.Name).ToArray();
            episode.Guests = (episode.Guests ?? [])
                .Concat(addedNames)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            logger.LogInformation(
                "{Method}: added {Count} guest(s) [{Guests}] to '{EpisodeTitle}' ({EpisodeId}); skipped {Skipped} low-confidence.",
                nameof(EnrichGuests),
                additions.Count,
                string.Join(", ", addedNames),
                episode.Title,
                episode.Id,
                skipped.Count);
        }
        else if (skipped.Count > 0)
        {
            logger.LogDebug(
                "{Method}: no high-confidence guests for '{EpisodeTitle}' ({EpisodeId}); skipped {Skipped} low-confidence.",
                nameof(EnrichGuests),
                episode.Title,
                episode.Id,
                skipped.Count);
        }

        return new EnrichGuestsResult(additions.ToArray(), skipped.ToArray());
    }

    private static bool IsHighConfidence(PersonMatch match, GuestEnrichmentOptions options)
    {
        var qualifying = match.MatchResults
            .Where(r => !string.IsNullOrWhiteSpace(r.Term) &&
                        r.Term.Trim().Length >= options.MinTermLength);

        if (options.MinMatchCount is { } minCount)
        {
            qualifying = qualifying.Where(r => r.Matches >= minCount);
        }

        // Title-only matching is enforced via MatchEpisode(withDescription: false).
        // RequireTitleMatch with TitleOnly=false is not separately verified in Phase 1.
        _ = options.RequireTitleMatch;

        return qualifying.Any();
    }
}
