using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Podcasts;

/// <summary>
/// Single authority helpers for <see cref="StreamingService"/> wire keys and catalog metadata.
/// </summary>
public static class StreamingServiceWire
{
    private static readonly ConcurrentDictionary<StreamingService, string> KeyCache = new();
    private static readonly ConcurrentDictionary<string, StreamingService> ParseCache =
        new(StringComparer.Ordinal);
    private static readonly Lazy<IReadOnlyList<StreamingService>> AllLazy = new(LoadAll);
    private static readonly Lazy<IReadOnlyDictionary<StreamingService, StreamingServiceInfoAttribute>> InfoLazy =
        new(LoadInfo);

    /// <summary>Enum members in declaration (search-encode) order — includes submit-retired.</summary>
    public static IReadOnlyList<StreamingService> All => AllLazy.Value;

    /// <summary>Wire keys in declaration order — includes submit-retired (Cosmos / image coalesce).</summary>
    public static string[] AllKeys => All.Select(ToKey).ToArray();

    /// <summary>
    /// Enum members eligible for streaming submit/prepare/scrape (excludes
    /// <see cref="StreamingServiceSubmitRetiredAttribute"/>).
    /// </summary>
    public static IReadOnlyList<StreamingService> SubmitEligible =>
        All.Where(s => !IsSubmitRetired(s)).ToArray();

    /// <summary>
    /// Submit-contract wire keys — must equal Api <c>streamingServiceKeys</c> and
    /// registered <see cref="StreamingServiceCatalog.SearchEncodedKeys"/>.
    /// </summary>
    public static string[] SubmitEligibleKeys => SubmitEligible.Select(ToKey).ToArray();

    /// <summary>
    /// Cover-art preference: full enum order (incl. retired) with BBC iPlayer before Sounds.
    /// Historical episode URLs (e.g. Hulu) still need coalesce slots even when submit-retired.
    /// </summary>
    public static string[] ImageCoalesceKeys
    {
        get
        {
            var keys = AllKeys;
            var sounds = Array.IndexOf(keys, ToKey(StreamingService.BbcSounds));
            var iplayer = Array.IndexOf(keys, ToKey(StreamingService.BbcIplayer));
            if (sounds >= 0 && iplayer >= 0 && iplayer > sounds)
            {
                (keys[sounds], keys[iplayer]) = (keys[iplayer], keys[sounds]);
            }

            return keys;
        }
    }

    public static bool IsSubmitRetired(StreamingService service)
    {
        var member = typeof(StreamingService).GetField(service.ToString())
                     ?? throw new InvalidOperationException($"Missing field for {service}.");
        return member.GetCustomAttribute<StreamingServiceSubmitRetiredAttribute>() is not null;
    }

    public static string ToKey(StreamingService service) =>
        KeyCache.GetOrAdd(service, static s =>
        {
            var member = typeof(StreamingService).GetField(s.ToString())
                         ?? throw new InvalidOperationException($"Missing field for {s}.");
            return member.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                   ?? throw new InvalidOperationException(
                       $"StreamingService.{s} is missing [{nameof(JsonPropertyNameAttribute)}].");
        });

    public static bool TryParse(string? key, out StreamingService service)
    {
        service = default;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        if (ParseCache.TryGetValue(key, out service))
        {
            return true;
        }

        foreach (var candidate in All)
        {
            if (string.Equals(ToKey(candidate), key, StringComparison.Ordinal))
            {
                ParseCache[key] = candidate;
                service = candidate;
                return true;
            }
        }

        if (Enum.TryParse(key, ignoreCase: true, out StreamingService parsed) &&
            Enum.IsDefined(parsed))
        {
            ParseCache[key] = parsed;
            service = parsed;
            return true;
        }

        return false;
    }

    public static StreamingService Parse(string key) =>
        TryParse(key, out var service)
            ? service
            : throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown streaming service wire key.");

    public static StreamingServiceInfoAttribute GetInfo(StreamingService service)
    {
        if (!InfoLazy.Value.TryGetValue(service, out var info))
        {
            throw new InvalidOperationException(
                $"StreamingService.{service} is missing [{nameof(StreamingServiceInfoAttribute)}].");
        }

        return info;
    }

    public static ServiceCatalog.Descriptor ToDescriptor(StreamingService service)
    {
        var info = GetInfo(service);
        return new ServiceCatalog.Descriptor(
            ToKey(service),
            info.DisplayName,
            info.Icon,
            ReconstructableFromIndexIds: false,
            info.WideImage,
            info.Hosts);
    }

    public static bool IsBbc(StreamingService service) =>
        service is StreamingService.BbcSounds or StreamingService.BbcIplayer;

    private static IReadOnlyList<StreamingService> LoadAll() =>
        Enum.GetValues<StreamingService>();

    private static IReadOnlyDictionary<StreamingService, StreamingServiceInfoAttribute> LoadInfo()
    {
        var map = new Dictionary<StreamingService, StreamingServiceInfoAttribute>();
        foreach (var service in All)
        {
            var member = typeof(StreamingService).GetField(service.ToString())!;
            var info = member.GetCustomAttribute<StreamingServiceInfoAttribute>()
                       ?? throw new InvalidOperationException(
                           $"StreamingService.{service} is missing [{nameof(StreamingServiceInfoAttribute)}].");
            map[service] = info;
        }

        return map;
    }
}
