using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TitleCasing;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Text.Models;

namespace MigrateLanguageIgnoredSubjects;

public class MigrateLanguageIgnoredSubjectsProcessor(
    IPodcastRepository podcastRepository,
    ILanguageTitleCasingRulesRepository titleCasingRulesRepository,
    ILogger<MigrateLanguageIgnoredSubjectsProcessor> logger)
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<int> Run(MigrateLanguageIgnoredSubjectsRequest request)
    {
        if (request.ApplySeed && request.ApplyClear)
        {
            logger.LogError("Pass only one of --apply-seed or --apply-clear (or neither for dry-run).");
            return 1;
        }

        if (request.ApplyClear)
        {
            return await ApplyClearAsync(request.AuditPath);
        }

        var plan = await BuildPlanAsync();
        LogPlan(plan);

        if (!request.ApplySeed)
        {
            logger.LogInformation(
                "Dry-run only. Pass --apply-seed to write TitleCasingRules and audit file '{AuditPath}'. Then --apply-clear to strip podcasts.",
                request.AuditPath);
            return 0;
        }

        return await ApplySeedAsync(plan, request.AuditPath);
    }

    private async Task<MigrationPlan> BuildPlanAsync()
    {
        var podcasts = new List<Podcast>();
        await foreach (var podcast in podcastRepository.GetAll())
        {
            if (string.IsNullOrWhiteSpace(podcast.Language) ||
                LowerCaseTerms.IsEnglish(podcast.Language) ||
                podcast.IgnoredSubjects is not { Length: > 0 })
            {
                continue;
            }

            podcasts.Add(podcast);
        }

        var byLanguage = podcasts
            .GroupBy(p => TitleCasingRulesDocument.NormaliseLanguage(p.Language!))
            .Where(g => !string.IsNullOrEmpty(g.Key) &&
                        g.Key != TitleCasingRulesDocument.UniversalLanguageKey &&
                        !string.Equals(g.Key, "en", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                g => g.Key,
                g => g.ToList(),
                StringComparer.OrdinalIgnoreCase);

        var languageSeeds = new List<LanguageSeed>();
        var podcastActions = new List<PodcastClearAction>();

        foreach (var (language, group) in byLanguage.OrderBy(x => x.Key))
        {
            var union = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var podcast in group)
            {
                foreach (var subject in podcast.IgnoredSubjects ?? [])
                {
                    if (!string.IsNullOrWhiteSpace(subject))
                    {
                        union.Add(subject.Trim());
                    }
                }
            }

            var orderedUnion = union.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
            languageSeeds.Add(new LanguageSeed(language, orderedUnion));

            foreach (var podcast in group)
            {
                var before = podcast.IgnoredSubjects ?? [];
                var toRemove = before
                    .Where(s => union.Contains(s.Trim()))
                    .ToArray();
                var after = before
                    .Where(s => !union.Contains(s.Trim()))
                    .ToArray();

                podcastActions.Add(new PodcastClearAction(
                    podcast.Id,
                    podcast.Name,
                    language,
                    before,
                    toRemove,
                    after.Length == 0 ? null : after));
            }
        }

        return new MigrationPlan(languageSeeds, podcastActions);
    }

    private void LogPlan(MigrationPlan plan)
    {
        foreach (var seed in plan.LanguageSeeds)
        {
            logger.LogInformation(
                "Language '{Language}': would seed ignoredSubjects=[{Subjects}] ({Count})",
                seed.Language,
                string.Join(", ", seed.IgnoredSubjects),
                seed.IgnoredSubjects.Length);
        }

        foreach (var action in plan.PodcastActions)
        {
            logger.LogInformation(
                "Podcast {PodcastId} '{PodcastName}' lang={Language}: before=[{Before}] remove=[{Remove}] after=[{After}]",
                action.PodcastId,
                action.PodcastName,
                action.Language,
                string.Join(", ", action.Before),
                string.Join(", ", action.ToRemove),
                action.After is null ? "" : string.Join(", ", action.After));
        }

        logger.LogInformation(
            "Plan totals: {LanguageCount} languages, {PodcastCount} podcasts.",
            plan.LanguageSeeds.Count,
            plan.PodcastActions.Count);
    }

    private async Task<int> ApplySeedAsync(MigrationPlan plan, string auditPath)
    {
        foreach (var seed in plan.LanguageSeeds)
        {
            var existing = await titleCasingRulesRepository.Get(seed.Language);
            NonEnglishTitleCasingRulesDocument document;
            if (existing is NonEnglishTitleCasingRulesDocument nonEnglish)
            {
                document = nonEnglish;
            }
            else if (existing is null)
            {
                document = new NonEnglishTitleCasingRulesDocument(seed.Language);
            }
            else
            {
                logger.LogError(
                    "Language '{Language}' TitleCasingRules document is {Type}; cannot seed ignored subjects.",
                    seed.Language,
                    existing.GetType().Name);
                return 1;
            }

            var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in document.IgnoredSubjects ?? [])
            {
                if (!string.IsNullOrWhiteSpace(item))
                {
                    merged.Add(item.Trim());
                }
            }

            foreach (var item in seed.IgnoredSubjects)
            {
                merged.Add(item);
            }

            document.IgnoredSubjects = merged.Count == 0
                ? null
                : merged.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

            await titleCasingRulesRepository.Save(document);
            logger.LogInformation(
                "Saved TitleCasingRules ignoredSubjects for '{Language}': [{Subjects}]",
                seed.Language,
                string.Join(", ", document.IgnoredSubjects ?? []));
        }

        var audit = new MigrationAudit(
            DateTimeOffset.UtcNow,
            plan.LanguageSeeds,
            plan.PodcastActions);
        await File.WriteAllTextAsync(auditPath, JsonSerializer.Serialize(audit, AuditJsonOptions));
        logger.LogInformation(
            "Wrote audit/undo file '{AuditPath}'. Review it, then run --apply-clear --audit-path \"{AuditPath}\".",
            Path.GetFullPath(auditPath),
            Path.GetFullPath(auditPath));
        return 0;
    }

    private async Task<int> ApplyClearAsync(string auditPath)
    {
        if (!File.Exists(auditPath))
        {
            logger.LogError("Audit file '{AuditPath}' not found. Run --apply-seed first.", auditPath);
            return 1;
        }

        var audit = JsonSerializer.Deserialize<MigrationAudit>(
            await File.ReadAllTextAsync(auditPath),
            AuditJsonOptions);
        if (audit is null)
        {
            logger.LogError("Could not parse audit file '{AuditPath}'.", auditPath);
            return 1;
        }

        foreach (var seed in audit.LanguageSeeds)
        {
            var document = await titleCasingRulesRepository.Get(seed.Language);
            if (document is not NonEnglishTitleCasingRulesDocument nonEnglish)
            {
                logger.LogError(
                    "Refusing clear: language '{Language}' TitleCasingRules missing or wrong type.",
                    seed.Language);
                return 1;
            }

            var existing = new HashSet<string>(
                nonEnglish.IgnoredSubjects ?? [],
                StringComparer.OrdinalIgnoreCase);
            var missing = seed.IgnoredSubjects
                .Where(s => !existing.Contains(s))
                .ToArray();
            if (missing.Length > 0)
            {
                logger.LogError(
                    "Refusing clear: language '{Language}' TitleCasingRules missing expected subjects [{Missing}].",
                    seed.Language,
                    string.Join(", ", missing));
                return 1;
            }
        }

        foreach (var action in audit.PodcastActions)
        {
            var podcast = await podcastRepository.GetPodcast(action.PodcastId);
            if (podcast is null)
            {
                logger.LogWarning(
                    "Podcast {PodcastId} not found; skip clear.",
                    action.PodcastId);
                continue;
            }

            logger.LogInformation(
                "CLEAR podcast {PodcastId} '{PodcastName}': restoring undo would set ignoredSubjects=[{Before}]; applying after=[{After}]",
                podcast.Id,
                podcast.Name,
                string.Join(", ", action.Before),
                action.After is null ? "" : string.Join(", ", action.After));

            podcast.IgnoredSubjects = action.After is { Length: > 0 } ? action.After.ToArray() : null;
            await podcastRepository.Save(podcast);
        }

        logger.LogInformation(
            "Phase 2 complete. Undo: re-apply each podcast ignoredSubjects from audit 'before' arrays in '{AuditPath}'.",
            Path.GetFullPath(auditPath));
        return 0;
    }

    private sealed record MigrationPlan(
        IReadOnlyList<LanguageSeed> LanguageSeeds,
        IReadOnlyList<PodcastClearAction> PodcastActions);

    public sealed record MigrationAudit(
        DateTimeOffset GeneratedAtUtc,
        IReadOnlyList<LanguageSeed> LanguageSeeds,
        IReadOnlyList<PodcastClearAction> PodcastActions);

    public sealed record LanguageSeed(string Language, string[] IgnoredSubjects);

    public sealed record PodcastClearAction(
        Guid PodcastId,
        string PodcastName,
        string Language,
        string[] Before,
        string[] ToRemove,
        string[]? After);
}
