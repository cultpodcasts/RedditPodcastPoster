using RedditPodcastPoster.Models.TitleCasing;

namespace Api.Services.TitleCasingRules;

/// <summary>
/// Pure business rules for admin title-casing delta mutations
/// (POST/DELETE lower-case terms and known terms). Clients send only the changed term.
/// </summary>
public static class TitleCasingRulesMutationRules
{
    public static TitleCasingRulesStringListMutationResult TryAddLowerCaseTerm(
        IReadOnlyList<string> existing,
        string? term)
    {
        var trimmed = term?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return TitleCasingRulesStringListMutationResult.Fail(
                "Lower-case term is required.");
        }

        if (existing.Any(t => t.Equals(trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return TitleCasingRulesStringListMutationResult.Ok(OrderTerms(existing));
        }

        var next = existing
            .Append(trimmed)
            .ToList();
        return TitleCasingRulesStringListMutationResult.Ok(OrderTerms(next));
    }

    public static TitleCasingRulesStringListMutationResult TryRemoveLowerCaseTerm(
        IReadOnlyList<string> existing,
        string? term)
    {
        var trimmed = term?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return TitleCasingRulesStringListMutationResult.Fail(
                "Lower-case term is required.");
        }

        var remaining = existing
            .Where(t => !t.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (remaining.Count == existing.Count)
        {
            return TitleCasingRulesStringListMutationResult.Fail(
                $"Lower-case term '{trimmed}' is not in the list.");
        }

        return TitleCasingRulesStringListMutationResult.Ok(OrderTerms(remaining));
    }

    public static TitleCasingRulesKnownTermsMutationResult TryUpsertKnownTerm(
        IReadOnlyList<KnownTermEntry> existing,
        string? literal,
        string? pattern,
        string? options)
    {
        var trimmedLiteral = literal?.Trim();
        if (string.IsNullOrEmpty(trimmedLiteral))
        {
            return TitleCasingRulesKnownTermsMutationResult.Fail(
                "Known term literal is required.");
        }

        var trimmedPattern = pattern?.Trim();
        if (string.IsNullOrEmpty(trimmedPattern))
        {
            return TitleCasingRulesKnownTermsMutationResult.Fail(
                "Known term pattern is required.");
        }

        var resolvedOptions = string.IsNullOrWhiteSpace(options)
            ? nameof(System.Text.RegularExpressions.RegexOptions.IgnoreCase) + ", " +
              nameof(System.Text.RegularExpressions.RegexOptions.Compiled)
            : options.Trim();

        KnownTermEntry entry;
        try
        {
            entry = new KnownTermEntry
            {
                Literal = trimmedLiteral,
                Pattern = trimmedPattern,
                Options = resolvedOptions
            };
            _ = entry.ToRegex();
        }
        catch (Exception ex)
        {
            return TitleCasingRulesKnownTermsMutationResult.Fail(
                $"Invalid regex pattern for known term '{trimmedLiteral}': {ex.Message}");
        }

        var next = existing
            .Where(t => !t.Literal.Equals(trimmedLiteral, StringComparison.OrdinalIgnoreCase))
            .Select(Clone)
            .Append(entry)
            .ToList();

        return TitleCasingRulesKnownTermsMutationResult.Ok(next);
    }

    public static TitleCasingRulesKnownTermsMutationResult TryRemoveKnownTerm(
        IReadOnlyList<KnownTermEntry> existing,
        string? literal)
    {
        var trimmedLiteral = literal?.Trim();
        if (string.IsNullOrEmpty(trimmedLiteral))
        {
            return TitleCasingRulesKnownTermsMutationResult.Fail(
                "Known term literal is required.");
        }

        var remaining = existing
            .Where(t => !t.Literal.Equals(trimmedLiteral, StringComparison.OrdinalIgnoreCase))
            .Select(Clone)
            .ToList();

        if (remaining.Count == existing.Count)
        {
            return TitleCasingRulesKnownTermsMutationResult.Fail(
                $"Known term '{trimmedLiteral}' is not in the list.");
        }

        return TitleCasingRulesKnownTermsMutationResult.Ok(remaining);
    }

    private static IReadOnlyList<string> OrderTerms(IEnumerable<string> terms) =>
        terms
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static KnownTermEntry Clone(KnownTermEntry term) =>
        new()
        {
            Literal = term.Literal,
            Pattern = term.Pattern,
            Options = term.Options
        };
}

public sealed record TitleCasingRulesStringListMutationResult(
    bool IsValid,
    IReadOnlyList<string> Terms,
    string? Error)
{
    public static TitleCasingRulesStringListMutationResult Ok(IReadOnlyList<string> terms) =>
        new(true, terms, null);

    public static TitleCasingRulesStringListMutationResult Fail(string error) =>
        new(false, [], error);
}

public sealed record TitleCasingRulesKnownTermsMutationResult(
    bool IsValid,
    IReadOnlyList<KnownTermEntry> Terms,
    string? Error)
{
    public static TitleCasingRulesKnownTermsMutationResult Ok(IReadOnlyList<KnownTermEntry> terms) =>
        new(true, terms, null);

    public static TitleCasingRulesKnownTermsMutationResult Fail(string error) =>
        new(false, [], error);
}
