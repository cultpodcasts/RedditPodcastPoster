using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Films;
using RedditPodcastPoster.Models.News;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Subjects;
using RedditPodcastPoster.Models.TvShows;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.HashTags;

namespace StoredHashtagAudit;

/// <summary>
/// Read-only scan of stored subject / publisher / playable hashtags that fail
/// <see cref="HashtagText.IsReproducedAsIs"/>. Reports counts (and optional samples) so a later
/// consent-gated cleanup can be sized; never writes Cosmos.
/// </summary>
public class StoredHashtagAuditProcessor(
    ISubjectRepository subjectRepository,
    IPodcastRepository podcastRepository,
    ITvShowRepository tvShowRepository,
    INewsOrganisationRepository newsOrganisationRepository,
    IFilmRepository filmRepository,
    IEpisodeRepository episodeRepository,
    ITvShowEpisodeRepository tvShowEpisodeRepository,
    INewsReportRepository newsReportRepository,
    ILogger<StoredHashtagAuditProcessor> logger)
{
    public async Task<StoredHashtagAuditResult> Run(StoredHashtagAuditRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var kinds = ParseKinds(request.Kind);
        var result = new StoredHashtagAuditResult();

        foreach (var kind in kinds)
        {
            var tally = kind switch
            {
                HashtagEntityKind.Subject => await Scan(
                    kind,
                    subjectRepository.GetAll(),
                    s => s.Id,
                    s => s.Name,
                    s => s.HashTag,
                    request.List,
                    request.SampleLimit),
                HashtagEntityKind.Podcast => await Scan(
                    kind,
                    podcastRepository.GetAll(),
                    p => p.Id,
                    p => p.Name,
                    p => p.HashTag,
                    request.List,
                    request.SampleLimit),
                HashtagEntityKind.TvShow => await Scan(
                    kind,
                    tvShowRepository.GetAll(),
                    t => t.Id,
                    t => t.Name,
                    t => t.HashTag,
                    request.List,
                    request.SampleLimit),
                HashtagEntityKind.NewsOrganisation => await Scan(
                    kind,
                    newsOrganisationRepository.GetAll(),
                    n => n.Id,
                    n => n.Name,
                    n => n.HashTag,
                    request.List,
                    request.SampleLimit),
                HashtagEntityKind.Film => await Scan(
                    kind,
                    filmRepository.GetAll(),
                    f => f.Id,
                    f => f.Name,
                    f => f.HashTag,
                    request.List,
                    request.SampleLimit),
                HashtagEntityKind.Episode => await Scan(
                    kind,
                    episodeRepository.GetAll(),
                    e => e.Id,
                    e => e.Title,
                    e => e.HashTag,
                    request.List,
                    request.SampleLimit),
                HashtagEntityKind.TvShowEpisode => await Scan(
                    kind,
                    tvShowEpisodeRepository.GetAll(),
                    e => e.Id,
                    e => e.Title,
                    e => e.HashTag,
                    request.List,
                    request.SampleLimit),
                HashtagEntityKind.NewsReport => await Scan(
                    kind,
                    newsReportRepository.GetAll(),
                    r => r.Id,
                    r => r.Title,
                    r => r.HashTag,
                    request.List,
                    request.SampleLimit),
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
            result.ByKind[kind] = tally;
        }

        Report(result);
        return result;
    }

    private async Task<KindTally> Scan<TEntity>(
        HashtagEntityKind kind,
        IAsyncEnumerable<TEntity> entities,
        Func<TEntity, Guid> id,
        Func<TEntity, string?> label,
        Func<TEntity, string?> hashTag,
        bool list,
        int sampleLimit)
    {
        var tally = new KindTally(kind);
        await foreach (var entity in entities)
        {
            tally.Scanned++;
            var stored = hashTag(entity);
            if (string.IsNullOrWhiteSpace(stored))
            {
                tally.Blank++;
                continue;
            }

            tally.WithHashTag++;
            // Posting splits first (HashTagExtensions.ToHashTags), then checks each token.
            var tokens = stored.ToHashTags()
                .Select(h => h.Tag)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToArray();
            var offenders = tokens.Where(t => !HashtagText.IsReproducedAsIs(t)).ToArray();
            if (offenders.Length == 0)
            {
                tally.Reproducible++;
                continue;
            }

            tally.NonReproducible++;
            if (!list)
            {
                continue;
            }

            if (sampleLimit > 0 && tally.Samples.Count >= sampleLimit)
            {
                tally.SamplesTruncated = true;
                continue;
            }

            var offendingDetail = string.Join(
                "; ",
                offenders.Select(o =>
                    $"token=\"{o.Trim()}\" toTagText={HashtagText.ToTagText(o) ?? "<null>"}"));
            tally.Samples.Add(
                $"{id(entity)} | {label(entity)} | stored=\"{stored.Trim()}\" | {offendingDetail}");
        }

        return tally;
    }

    private static IReadOnlyList<HashtagEntityKind> ParseKinds(string? raw)
    {
        var value = (raw ?? "all").Trim().ToLowerInvariant();
        return value switch
        {
            "all" =>
            [
                HashtagEntityKind.Subject,
                HashtagEntityKind.Podcast,
                HashtagEntityKind.TvShow,
                HashtagEntityKind.NewsOrganisation,
                HashtagEntityKind.Film,
                HashtagEntityKind.Episode,
                HashtagEntityKind.TvShowEpisode,
                HashtagEntityKind.NewsReport
            ],
            "subject" => [HashtagEntityKind.Subject],
            "podcast" => [HashtagEntityKind.Podcast],
            "tvshow" or "tv" => [HashtagEntityKind.TvShow],
            "news" or "newsorganisation" => [HashtagEntityKind.NewsOrganisation],
            "film" => [HashtagEntityKind.Film],
            "episode" => [HashtagEntityKind.Episode],
            "tvshowepisode" or "tvepisode" => [HashtagEntityKind.TvShowEpisode],
            "newsreport" => [HashtagEntityKind.NewsReport],
            _ => throw new ArgumentException(
                $"Unknown --kind '{raw}'. Use all, subject, podcast, tvshow, news, film, episode, tvshowepisode, or newsreport.",
                nameof(raw))
        };
    }

    private void Report(StoredHashtagAuditResult result)
    {
        var totalOffenders = result.TotalNonReproducible;
        foreach (var tally in result.ByKind.Values)
        {
            logger.LogInformation(
                "StoredHashtagAudit: {kind}: scanned {scanned}; blank {blank}; withHashTag {withHashTag}; " +
                "reproducible {reproducible}; nonReproducible {nonReproducible}.",
                tally.Kind,
                tally.Scanned,
                tally.Blank,
                tally.WithHashTag,
                tally.Reproducible,
                tally.NonReproducible);
            foreach (var sample in tally.Samples)
            {
                logger.LogInformation("StoredHashtagAudit sample ({kind}): {row}", tally.Kind, sample);
            }

            if (tally.SamplesTruncated)
            {
                logger.LogInformation(
                    "StoredHashtagAudit: {kind}: further non-reproducible rows omitted (sample-limit).",
                    tally.Kind);
            }
        }

        logger.LogInformation(
            "StoredHashtagAudit complete: total non-reproducible {totalOffenders} across {kinds} kind(s). " +
            "Read-only — nothing written. Posting already omits tags that fail IsReproducedAsIs; " +
            "if total is 0, no cleanup is needed (#1082).",
            totalOffenders,
            result.ByKind.Count);
    }
}

public sealed class StoredHashtagAuditResult
{
    public Dictionary<HashtagEntityKind, KindTally> ByKind { get; } = new();

    public int TotalNonReproducible => ByKind.Values.Sum(t => t.NonReproducible);

    public int ExitCode => 0;
}

public sealed class KindTally(HashtagEntityKind kind)
{
    public HashtagEntityKind Kind { get; } = kind;
    public int Scanned { get; set; }
    public int Blank { get; set; }
    public int WithHashTag { get; set; }
    public int Reproducible { get; set; }
    public int NonReproducible { get; set; }
    public List<string> Samples { get; } = [];
    public bool SamplesTruncated { get; set; }
}
