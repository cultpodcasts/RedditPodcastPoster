using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Cosmos;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace UnusableFileKeyAudit;

/// <summary>
/// Read-only scan of podcast / TV show / news organisation / film file keys that fail
/// <see cref="FileKeyFactory.IsUsableFileKey"/>. Reports counts (and optional samples) so a later consent-gated
/// repair can be sized; never writes Cosmos.
/// </summary>
public class UnusableFileKeyAuditProcessor(
    IPodcastRepository podcastRepository,
    ITvShowRepository tvShowRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    IFilmRepository filmRepository,
    ILogger<UnusableFileKeyAuditProcessor> logger)
{
    public async Task<UnusableFileKeyAuditResult> Run(UnusableFileKeyAuditRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var kinds = ParseKinds(request.Kind);
        var result = new UnusableFileKeyAuditResult();

        foreach (var kind in kinds)
        {
            var tallies = kind switch
            {
                FileKeyKind.Podcast => await Scan(
                    kind, podcastRepository.GetAll(), request.List, request.SampleLimit),
                FileKeyKind.TvShow => await Scan(
                    kind, tvShowRepository.GetAll(), request.List, request.SampleLimit),
                FileKeyKind.NewsOrganisation => await Scan(
                    kind, newsOrganisationRepository.GetAll(), request.List, request.SampleLimit),
                FileKeyKind.Film => await Scan(
                    kind, filmRepository.GetAll(), request.List, request.SampleLimit),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
            result.ByKind[kind] = tallies;
        }

        Report(result);
        return result;
    }

    private async Task<KindTally> Scan<TEntity>(
        FileKeyKind kind,
        IAsyncEnumerable<TEntity> entities,
        bool list,
        int sampleLimit) where TEntity : IPublicEntity
    {
        var prefix = FileKeyFactory.PrefixFor(kind);
        var tally = new KindTally(kind);
        await foreach (var entity in entities)
        {
            tally.Scanned++;
            if (FileKeyFactory.IsUsableFileKey(entity.FileKey, prefix))
            {
                tally.Usable++;
                continue;
            }

            tally.Unusable++;
            if (!list)
            {
                continue;
            }

            if (sampleLimit > 0 && tally.Samples.Count >= sampleLimit)
            {
                tally.SamplesTruncated = true;
                continue;
            }

            tally.Samples.Add($"{entity.Id} | {entity.Name} | fileKey={DescribeKey(entity.FileKey)}");
        }

        return tally;
    }

    private static string DescribeKey(string? fileKey) =>
        fileKey is null ? "<null>" : $"\"{fileKey}\"";

    private static IReadOnlyList<FileKeyKind> ParseKinds(string? raw)
    {
        var value = (raw ?? "all").Trim().ToLowerInvariant();
        return value switch
        {
            "all" =>
            [
                FileKeyKind.Podcast, FileKeyKind.TvShow, FileKeyKind.NewsOrganisation, FileKeyKind.Film
            ],
            "podcast" => [FileKeyKind.Podcast],
            "tvshow" or "tv" => [FileKeyKind.TvShow],
            "news" or "newsorganisation" => [FileKeyKind.NewsOrganisation],
            "film" => [FileKeyKind.Film],
            _ => throw new ArgumentException(
                $"Unknown --kind '{raw}'. Use all, podcast, tvshow, news, or film.", nameof(raw))
        };
    }

    private void Report(UnusableFileKeyAuditResult result)
    {
        var totalUnusable = result.ByKind.Values.Sum(t => t.Unusable);
        foreach (var tally in result.ByKind.Values)
        {
            logger.LogInformation(
                "UnusableFileKeyAudit: {kind}: scanned {scanned}; usable {usable}; unusable {unusable}.",
                tally.Kind,
                tally.Scanned,
                tally.Usable,
                tally.Unusable);
            foreach (var sample in tally.Samples)
            {
                logger.LogInformation("UnusableFileKeyAudit sample ({kind}): {row}", tally.Kind, sample);
            }

            if (tally.SamplesTruncated)
            {
                logger.LogInformation(
                    "UnusableFileKeyAudit: {kind}: further unusable rows omitted (sample-limit).",
                    tally.Kind);
            }
        }

        logger.LogInformation(
            "UnusableFileKeyAudit complete: total unusable {totalUnusable} across {kinds} kind(s). " +
            "Read-only — nothing written. If total is 0, skip the repair backfill; otherwise size a consent-gated " +
            "RepairIfUnusable pass (#1167).",
            totalUnusable,
            result.ByKind.Count);
    }
}

public sealed class UnusableFileKeyAuditResult
{
    public Dictionary<FileKeyKind, KindTally> ByKind { get; } = new();

    public int TotalUnusable => ByKind.Values.Sum(t => t.Unusable);

    public int ExitCode => 0;
}

public sealed class KindTally(FileKeyKind kind)
{
    public FileKeyKind Kind { get; } = kind;
    public int Scanned { get; set; }
    public int Usable { get; set; }
    public int Unusable { get; set; }
    public List<string> Samples { get; } = [];
    public bool SamplesTruncated { get; set; }
}
