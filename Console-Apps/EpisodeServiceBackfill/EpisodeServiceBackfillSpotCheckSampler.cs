using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;

namespace EpisodeServiceBackfill;

/// <summary>
/// Thread-safe reservoir sample (Algorithm R) of catalog patches for post-run Cosmos spot-checks.
/// </summary>
public sealed class EpisodeServiceBackfillSpotCheckSampler
{
    private readonly int _capacity;
    private readonly Random _random;
    private readonly object _gate = new();
    private readonly List<EpisodeServiceBackfillSpotCheckSample> _reservoir = [];
    private int _seen;

    public EpisodeServiceBackfillSpotCheckSampler(int capacity, Random? random = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        _capacity = capacity;
        _random = random ?? new Random();
    }

    public int Capacity => _capacity;

    public int Seen
    {
        get
        {
            lock (_gate)
            {
                return _seen;
            }
        }
    }

    public void Offer(EpisodeServiceCatalogPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        if (_capacity == 0)
        {
            return;
        }

        var sample = EpisodeServiceBackfillSpotCheckSample.FromPatch(patch);
        lock (_gate)
        {
            _seen++;
            if (_reservoir.Count < _capacity)
            {
                _reservoir.Add(sample);
                return;
            }

            var j = _random.Next(_seen);
            if (j < _capacity)
            {
                _reservoir[j] = sample;
            }
        }
    }

    public IReadOnlyList<EpisodeServiceBackfillSpotCheckSample> Snapshot()
    {
        lock (_gate)
        {
            return [.._reservoir];
        }
    }
}

public sealed record EpisodeServiceBackfillSpotCheckSample(
    Guid EpisodeId,
    Guid PodcastId,
    Dictionary<string, EpisodeServiceLink>? Services,
    EpisodeIds? Ids)
{
    public static EpisodeServiceBackfillSpotCheckSample FromPatch(EpisodeServiceCatalogPatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        return new EpisodeServiceBackfillSpotCheckSample(
            patch.EpisodeId,
            patch.PodcastId,
            CloneServices(patch.Services),
            CloneIds(patch.Ids));
    }

    private static Dictionary<string, EpisodeServiceLink>? CloneServices(
        Dictionary<string, EpisodeServiceLink>? services)
    {
        if (services is not { Count: > 0 })
        {
            return null;
        }

        return services.ToDictionary(
            x => x.Key,
            x => new EpisodeServiceLink { Url = x.Value.Url, Image = x.Value.Image },
            StringComparer.Ordinal);
    }

    private static EpisodeIds? CloneIds(EpisodeIds? ids)
    {
        if (ids is null || ids.IsEmpty)
        {
            return null;
        }

        return new EpisodeIds
        {
            Spotify = ids.Spotify,
            Apple = ids.Apple,
            YouTube = ids.YouTube
        };
    }
}
