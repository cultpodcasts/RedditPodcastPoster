namespace PeopleMigrator;

/// <summary>
/// Promotes cleaner full names to canonical and demotes titles or first-name-only forms to aliases.
/// </summary>
internal static class CanonicalNamePromoter
{
    private static readonly HashSet<string> TitlePrefixTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "acting", "ag", "atty", "attorney", "congressman", "congresswoman", "dame", "democratic",
        "dr", "doctor", "gov", "governor", "hon", "honorable", "lady", "lord", "miss", "mr", "mrs",
        "ms", "nys", "prof", "professor", "rep", "representative", "sen", "senator", "sir"
    };

    internal sealed record PromotionResult(
        string CanonicalName,
        string[] Aliases,
        bool WasPromoted,
        string? Reason);

    internal sealed record BatchPromotionResult(
        int PeopleCount,
        int PromotedCount,
        IReadOnlyList<string> PromotionExamples);

    public static PromotionResult Promote(
        string canonicalName,
        IEnumerable<string>? aliases,
        string? twitterHandle = null,
        string? blueskyHandle = null)
    {
        var aliasList = (aliases ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();

        var promotedFromAlias = TryPromoteFromAlias(canonicalName, aliasList);
        if (promotedFromAlias != null)
        {
            return Finalize(canonicalName, aliasList, promotedFromAlias, promotedFromAlias.Reason, twitterHandle, blueskyHandle);
        }

        var stripped = TryStripTitlesFromCanonical(canonicalName);
        if (stripped != null)
        {
            return Finalize(canonicalName, aliasList, stripped, "stripped title from canonical", twitterHandle, blueskyHandle);
        }

        var normalized = TryStripNormalizedCanonical(canonicalName);
        if (normalized != null)
        {
            return Finalize(canonicalName, aliasList, normalized, "stripped noise from canonical", twitterHandle, blueskyHandle);
        }

        return Finalize(
            canonicalName,
            aliasList,
            canonicalName,
            null,
            twitterHandle,
            blueskyHandle,
            filterOnly: true);
    }

    public static void ApplyToPerson(RedditPodcastPoster.Models.Person person)
    {
        var result = Promote(
            person.Name,
            person.Aliases,
            person.TwitterHandle,
            person.BlueskyHandle);

        person.Name = result.CanonicalName;
        person.Aliases = result.Aliases.Length == 0 ? null : result.Aliases;
    }

    /// <summary>
    /// Promotes one alias to canonical and demotes the previous canonical to aliases when allowed.
    /// </summary>
    public static PromotionResult SwapCanonicalWithAlias(
        string canonicalName,
        IEnumerable<string>? aliases,
        string aliasToPromote,
        string? twitterHandle = null,
        string? blueskyHandle = null)
    {
        if (string.IsNullOrWhiteSpace(aliasToPromote))
        {
            throw new ArgumentException("Alias is required.", nameof(aliasToPromote));
        }

        var trimmedAlias = aliasToPromote.Trim();
        var aliasList = (aliases ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();

        if (!aliasList.Any(a => PersonAliasFilter.IsSameName(a, trimmedAlias)))
        {
            throw new InvalidOperationException($"Alias not found: {trimmedAlias}");
        }

        var remaining = aliasList
            .Where(a => !PersonAliasFilter.IsSameName(a, trimmedAlias))
            .ToList();

        return Finalize(
            canonicalName,
            remaining,
            trimmedAlias,
            "swapped with canonical",
            twitterHandle,
            blueskyHandle);
    }

    internal static BatchPromotionResult PromoteSeedEntries(
        IEnumerable<PeopleSeedJsonWriter.PeopleSeedEntry> people)
    {
        var promotedCount = 0;
        var examples = new List<string>();

        foreach (var person in people)
        {
            var before = person.Name;
            var result = Promote(
                person.Name,
                person.Aliases,
                person.TwitterHandle,
                person.BlueskyHandle);

            person.Name = result.CanonicalName;
            person.Aliases = result.Aliases;

            if (!result.WasPromoted)
            {
                continue;
            }

            promotedCount++;
            examples.Add($"{before} → {person.Name} ({result.Reason})");
        }

        return new BatchPromotionResult(people.Count(), promotedCount, examples);
    }

    private sealed record CandidatePromotion(string Name, string Reason);

    private static CandidatePromotion? TryPromoteFromAlias(string canonicalName, IReadOnlyList<string> aliases)
    {
        var firstNamePromotion = TryPromoteFirstNameOnly(canonicalName, aliases);
        if (firstNamePromotion != null)
        {
            return firstNamePromotion;
        }

        if (!StartsWithTitlePrefix(canonicalName))
        {
            return null;
        }

        var best = aliases
            .Where(alias => !StartsWithTitlePrefix(alias))
            .Where(alias => PersonAliasFilter.WouldMatchCanonical(canonicalName, alias))
            .Where(alias => !AliasNoiseFilter.IsNoiseAlias(alias, canonicalName))
            .OrderBy(alias => PersonAliasFilter.ExtractCoreTokens(alias).Count)
            .ThenBy(alias => alias, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        return best == null
            ? null
            : new CandidatePromotion(best, "promoted cleaner alias");
    }

    private static CandidatePromotion? TryPromoteFirstNameOnly(string canonicalName, IReadOnlyList<string> aliases)
    {
        var canonicalCore = PersonAliasFilter.ExtractCoreTokens(canonicalName);
        if (canonicalCore.Count != 1)
        {
            return null;
        }

        var firstName = canonicalCore[0];
        string? bestAlias = null;
        var bestTokenCount = 0;

        foreach (var alias in aliases)
        {
            var aliasCore = PersonAliasFilter.ExtractCoreTokens(alias);
            if (aliasCore.Count < 2 ||
                !aliasCore[0].Equals(firstName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (AliasNoiseFilter.IsNoiseAlias(alias, canonicalName))
            {
                continue;
            }

            if (aliasCore.Count > bestTokenCount)
            {
                bestAlias = alias;
                bestTokenCount = aliasCore.Count;
            }
        }

        return bestAlias == null
            ? null
            : new CandidatePromotion(bestAlias, "promoted full-name alias over first-name-only canonical");
    }

    private static string? TryStripTitlesFromCanonical(string canonicalName)
    {
        if (!StartsWithTitlePrefix(canonicalName))
        {
            return null;
        }

        var cleaned = BuildNameWithoutTitles(canonicalName);
        if (string.IsNullOrWhiteSpace(cleaned) ||
            PersonAliasFilter.IsSameName(cleaned, canonicalName))
        {
            return null;
        }

        return PersonAliasFilter.ExtractCoreTokens(cleaned).Count >= 2
            ? cleaned
            : null;
    }

    private static string? TryStripNormalizedCanonical(string canonicalName)
    {
        var cleaned = EpisodeGuestNameExtractor.NormalizePersonName(canonicalName);
        if (string.IsNullOrWhiteSpace(cleaned) ||
            PersonAliasFilter.IsSameName(cleaned, canonicalName))
        {
            return null;
        }

        return PersonAliasFilter.ExtractCoreTokens(cleaned).Count >= 1
            ? cleaned
            : null;
    }

    private static PromotionResult Finalize(
        string oldCanonical,
        IReadOnlyList<string> oldAliases,
        CandidatePromotion promotion,
        string? reasonOverride,
        string? twitterHandle,
        string? blueskyHandle)
    {
        return Finalize(
            oldCanonical,
            oldAliases,
            promotion.Name,
            reasonOverride ?? promotion.Reason,
            twitterHandle,
            blueskyHandle);
    }

    private static PromotionResult Finalize(
        string oldCanonical,
        IReadOnlyList<string> oldAliases,
        string newCanonical,
        string? reason,
        string? twitterHandle,
        string? blueskyHandle,
        bool filterOnly = false)
    {
        var mergedAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in oldAliases)
        {
            if (!PersonAliasFilter.IsSameName(alias, newCanonical))
            {
                mergedAliases.Add(alias);
            }
        }

        if (!filterOnly &&
            !PersonAliasFilter.IsSameName(oldCanonical, newCanonical) &&
            !PersonAliasFilter.WouldMatchCanonical(newCanonical, oldCanonical) &&
            !AliasNoiseFilter.IsNoiseAlias(oldCanonical, newCanonical))
        {
            mergedAliases.Add(oldCanonical);
        }

        var filtered = PersonAliasFilter.FilterAliases(
            mergedAliases,
            newCanonical,
            twitterHandle,
            blueskyHandle);

        var wasPromoted = !filterOnly && !PersonAliasFilter.IsSameName(oldCanonical, newCanonical);
        return new PromotionResult(newCanonical, filtered, wasPromoted, wasPromoted ? reason : null);
    }

    internal static bool StartsWithTitlePrefix(string name)
    {
        var tokens = SplitRawNameTokens(name);
        return tokens.Count > 0 && IsTitlePrefixToken(tokens[0]);
    }

    internal static string BuildNameWithoutTitles(string name)
    {
        var tokens = SplitRawNameTokens(name);
        while (tokens.Count > 0 && IsTitlePrefixToken(tokens[0]))
        {
            tokens.RemoveAt(0);
        }

        tokens = StripParentheticalSuffixes(tokens);
        tokens = StripTrailingCredentialTokens(tokens);
        tokens = tokens
            .Where(token => token.Length > 1 || !char.IsLetter(token[0]))
            .ToList();

        return string.Join(' ', tokens);
    }

    private static List<string> StripParentheticalSuffixes(IReadOnlyList<string> tokens)
    {
        return tokens
            .Where(token => !token.StartsWith('(') && !token.EndsWith(')'))
            .ToList();
    }

    private static List<string> StripTrailingCredentialTokens(IReadOnlyList<string> tokens)
    {
        var result = tokens.ToList();
        while (result.Count > 0)
        {
            var last = result[^1].Trim('.');
            if (PersonAliasFilter.ExtractCoreTokens(last).Count == 0 &&
                last.Length <= 4 &&
                char.IsUpper(last[0]))
            {
                result.RemoveAt(result.Count - 1);
                continue;
            }

            break;
        }

        return result;
    }

    private static bool IsTitlePrefixToken(string token)
    {
        return TitlePrefixTokens.Contains(token.Trim('.'));
    }

    private static List<string> SplitRawNameTokens(string name)
    {
        return name
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();
    }
}
