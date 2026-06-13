using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify.Resolvers;
using RedditPodcastPoster.PodcastServices.YouTube.Resolvers;

namespace DiscoveryTrainingExport;

public class DiscoveryTrainingExportProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task Run(DiscoveryTrainingExportRequest request)
    {
        var exportPath = Path.GetFullPath(request.ExportPath);
        var outputPath = Path.GetFullPath(request.OutputPath ?? Path.Combine(exportPath, "analysis"));
        Directory.CreateDirectory(outputPath);

        var episodeFolder = Path.Combine(exportPath, "episode");
        var discoveryFolder = Path.Combine(exportPath, "discovery");
        if (!Directory.Exists(episodeFolder))
        {
            throw new DirectoryNotFoundException($"Missing episode folder: '{episodeFolder}'.");
        }

        if (!Directory.Exists(discoveryFolder))
        {
            throw new DirectoryNotFoundException($"Missing discovery folder: '{discoveryFolder}'.");
        }

        Console.WriteLine("Loading episodes...");
        var episodeIndex = LoadEpisodeIndex(episodeFolder);
        Console.WriteLine(
            $"Indexed {episodeIndex.TotalEpisodes:N0} episodes " +
            $"({episodeIndex.SpotifyKeys:N0} spotify, {episodeIndex.AppleKeys:N0} apple, {episodeIndex.YouTubeKeys:N0} youtube keys).");

        Console.WriteLine("Processing discovery documents...");
        var rows = new List<DiscoveryTrainingRow>();
        var documentCount = 0;
        foreach (var discoveryFile in Directory.EnumerateFiles(discoveryFolder, "*.json"))
        {
            documentCount++;
            await using var stream = File.OpenRead(discoveryFile);
            var document = await JsonSerializer.DeserializeAsync<DiscoveryResultsDocument>(stream, JsonOptions)
                           ?? throw new InvalidOperationException($"Failed to deserialize '{discoveryFile}'.");

            foreach (var result in document.DiscoveryResults)
            {
                rows.Add(BuildRow(document, result, episodeIndex));
            }
        }

        Console.WriteLine($"Flattened {rows.Count:N0} discovery results from {documentCount:N0} documents.");

        var resultsCsv = Path.Combine(outputPath, "discovery-results.csv");
        var joinsCsv = Path.Combine(outputPath, "discovery-episode-joins.csv");
        var summaryPath = Path.Combine(outputPath, "summary.txt");

        await WriteResultsCsv(resultsCsv, rows);
        await WriteJoinsCsv(joinsCsv, rows);
        await WriteSummary(summaryPath, rows, documentCount);

        Console.WriteLine($"Wrote {resultsCsv}");
        Console.WriteLine($"Wrote {joinsCsv}");
        Console.WriteLine($"Wrote {summaryPath}");
    }

    private static DiscoveryTrainingRow BuildRow(
        DiscoveryResultsDocument document,
        DiscoveryResult result,
        EpisodeIndex episodeIndex)
    {
        var join = episodeIndex.TryJoin(result);
        var discoverySubjects = result.Subjects?.ToArray() ?? [];
        var episodeSubjects = join.Episode?.Subjects.ToArray() ?? [];
        var addedSubjects = episodeSubjects.Except(discoverySubjects, StringComparer.OrdinalIgnoreCase).ToArray();
        var removedSubjects = discoverySubjects.Except(episodeSubjects, StringComparer.OrdinalIgnoreCase).ToArray();

        return new DiscoveryTrainingRow
        {
            DocumentId = document.Id,
            DocumentState = document.State.ToString(),
            DiscoveryBegan = document.DiscoveryBegan,
            SearchSince = document.SearchSince,
            IncludeYouTube = document.IncludeYouTube,
            IncludeListenNotes = document.IncludeListenNotes,
            IncludeTaddy = document.IncludeTaddy,
            ExcludeSpotify = document.ExcludeSpotify,
            ResultId = result.Id,
            State = result.State?.ToString() ?? string.Empty,
            EpisodeName = result.EpisodeName,
            ShowName = result.ShowName,
            Released = result.Released,
            Duration = result.Length?.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture),
            SpotifyUrl = result.Urls.Spotify?.ToString(),
            AppleUrl = result.Urls.Apple?.ToString(),
            YouTubeUrl = result.Urls.YouTube?.ToString(),
            SpotifyEpisodeId = ExtractSpotifyEpisodeId(result.Urls.Spotify),
            AppleEpisodeId = ExtractAppleEpisodeId(result.Urls.Apple),
            YouTubeVideoId = ExtractYouTubeVideoId(result.Urls.YouTube),
            Sources = string.Join('|', result.Sources.Select(x => x.ToString())),
            DiscoverySubjects = string.Join('|', discoverySubjects),
            MatchingPodcastIds = string.Join('|', result.MatchingPodcastIds),
            JoinMethod = join.Method,
            JoinMatchCount = join.MatchCount,
            EpisodeId = join.Episode?.Id,
            EpisodePodcastId = join.Episode?.PodcastId == Guid.Empty ? null : join.Episode?.PodcastId,
            EpisodeTitle = join.Episode?.Title,
            EpisodeSubjects = string.Join('|', episodeSubjects),
            SubjectsAdded = string.Join('|', addedSubjects),
            SubjectsRemoved = string.Join('|', removedSubjects),
            SubjectsExactMatch = join.Episode != null &&
                                 discoverySubjects.SequenceEqual(episodeSubjects, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static EpisodeIndex LoadEpisodeIndex(string episodeFolder)
    {
        var files = Directory.EnumerateFiles(episodeFolder, "*.json").ToArray();
        var entries = new ConcurrentBag<(ServiceKey Service, string Key, EpisodeRef Episode)>();
        var processed = 0;

        Parallel.ForEach(files, file =>
        {
            var count = Interlocked.Increment(ref processed);
            if (count % 10000 == 0)
            {
                Console.WriteLine($"  ...{count:N0} episodes");
            }

            using var document = JsonDocument.Parse(File.ReadAllBytes(file));
            var root = document.RootElement;
            if (!root.TryGetProperty("id", out var idElement) ||
                !Guid.TryParse(idElement.GetString(), out var episodeId))
            {
                return;
            }

            root.TryGetProperty("podcastId", out var podcastIdElement);
            Guid.TryParse(podcastIdElement.GetString(), out var podcastId);
            var title = root.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : null;
            var spotifyId = root.TryGetProperty("spotifyId", out var spotifyElement)
                ? spotifyElement.GetString()
                : null;
            long? appleId = root.TryGetProperty("appleId", out var appleElement) &&
                            appleElement.ValueKind == JsonValueKind.Number
                ? appleElement.GetInt64()
                : null;
            var youTubeId = root.TryGetProperty("youTubeId", out var youTubeElement)
                ? youTubeElement.GetString()
                : null;
            var subjects = root.TryGetProperty("subjects", out var subjectsElement) &&
                           subjectsElement.ValueKind == JsonValueKind.Array
                ? subjectsElement.EnumerateArray()
                    .Select(x => x.GetString())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!)
                    .ToList()
                : [];

            var episodeRef = new EpisodeRef(episodeId, podcastId, title, subjects);

            if (!string.IsNullOrWhiteSpace(spotifyId))
            {
                entries.Add((ServiceKey.Spotify, spotifyId, episodeRef));
            }

            if (appleId.HasValue)
            {
                entries.Add((ServiceKey.Apple, appleId.Value.ToString(CultureInfo.InvariantCulture), episodeRef));
            }

            if (!string.IsNullOrWhiteSpace(youTubeId))
            {
                entries.Add((ServiceKey.YouTube, youTubeId, episodeRef));
            }
        });

        var spotify = new Dictionary<string, List<EpisodeRef>>(StringComparer.OrdinalIgnoreCase);
        var apple = new Dictionary<long, List<EpisodeRef>>();
        var youtube = new Dictionary<string, List<EpisodeRef>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (service, key, episode) in entries)
        {
            switch (service)
            {
                case ServiceKey.Spotify:
                    Add(spotify, key, episode);
                    break;
                case ServiceKey.Apple:
                    Add(apple, long.Parse(key, CultureInfo.InvariantCulture), episode);
                    break;
                case ServiceKey.YouTube:
                    Add(youtube, key, episode);
                    break;
            }
        }

        return new EpisodeIndex(spotify, apple, youtube, files.Length);
    }

    private static void Add<TKey>(Dictionary<TKey, List<EpisodeRef>> index, TKey key, EpisodeRef episode)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var list))
        {
            list = [];
            index[key] = list;
        }

        list.Add(episode);
    }

    private static string? ExtractSpotifyEpisodeId(Uri? url)
    {
        if (url == null)
        {
            return null;
        }

        var id = SpotifyIdResolver.GetEpisodeId(url);
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }

    private static long? ExtractAppleEpisodeId(Uri? url)
    {
        if (url == null)
        {
            return null;
        }

        return AppleIdResolver.GetEpisodeId(url);
    }

    private static string? ExtractYouTubeVideoId(Uri? url)
    {
        if (url == null)
        {
            return null;
        }

        return YouTubeIdResolver.Extract(url);
    }

    private static async Task WriteResultsCsv(string path, IReadOnlyList<DiscoveryTrainingRow> rows)
    {
        var headers = new[]
        {
            "documentId", "documentState", "discoveryBegan", "searchSince", "includeYouTube", "includeListenNotes",
            "includeTaddy", "excludeSpotify", "resultId", "state", "episodeName", "showName", "released", "duration",
            "spotifyUrl", "appleUrl", "youTubeUrl", "spotifyEpisodeId", "appleEpisodeId", "youTubeVideoId", "sources",
            "discoverySubjects", "matchingPodcastIds", "joinMethod", "joinMatchCount", "episodeId", "episodePodcastId",
            "episodeTitle", "episodeSubjects", "subjectsAdded", "subjectsRemoved", "subjectsExactMatch"
        };

        await using var writer = new StreamWriter(path, false, Encoding.UTF8);
        await writer.WriteLineAsync(string.Join(',', headers));

        foreach (var row in rows)
        {
            await writer.WriteLineAsync(string.Join(',',
                Csv(row.DocumentId),
                Csv(row.DocumentState),
                Csv(row.DiscoveryBegan),
                Csv(row.SearchSince),
                Csv(row.IncludeYouTube),
                Csv(row.IncludeListenNotes),
                Csv(row.IncludeTaddy),
                Csv(row.ExcludeSpotify),
                Csv(row.ResultId),
                Csv(row.State),
                Csv(row.EpisodeName),
                Csv(row.ShowName),
                Csv(row.Released),
                Csv(row.Duration),
                Csv(row.SpotifyUrl),
                Csv(row.AppleUrl),
                Csv(row.YouTubeUrl),
                Csv(row.SpotifyEpisodeId),
                Csv(row.AppleEpisodeId),
                Csv(row.YouTubeVideoId),
                Csv(row.Sources),
                Csv(row.DiscoverySubjects),
                Csv(row.MatchingPodcastIds),
                Csv(row.JoinMethod),
                Csv(row.JoinMatchCount),
                Csv(row.EpisodeId),
                Csv(row.EpisodePodcastId),
                Csv(row.EpisodeTitle),
                Csv(row.EpisodeSubjects),
                Csv(row.SubjectsAdded),
                Csv(row.SubjectsRemoved),
                Csv(row.SubjectsExactMatch)));
        }
    }

    private static async Task WriteJoinsCsv(string path, IReadOnlyList<DiscoveryTrainingRow> rows)
    {
        var joined = rows
            .Where(x => string.Equals(x.State, nameof(DiscoveryResultState.Accepted), StringComparison.OrdinalIgnoreCase)
                        && x.EpisodeId != null)
            .ToList();

        var headers = new[]
        {
            "resultId", "episodeId", "episodePodcastId", "joinMethod", "joinMatchCount", "showName", "episodeName",
            "episodeTitle", "discoverySubjects", "episodeSubjects", "subjectsAdded", "subjectsRemoved",
            "subjectsExactMatch", "spotifyEpisodeId", "appleEpisodeId", "youTubeVideoId"
        };

        await using var writer = new StreamWriter(path, false, Encoding.UTF8);
        await writer.WriteLineAsync(string.Join(',', headers));

        foreach (var row in joined)
        {
            await writer.WriteLineAsync(string.Join(',',
                Csv(row.ResultId),
                Csv(row.EpisodeId),
                Csv(row.EpisodePodcastId),
                Csv(row.JoinMethod),
                Csv(row.JoinMatchCount),
                Csv(row.ShowName),
                Csv(row.EpisodeName),
                Csv(row.EpisodeTitle),
                Csv(row.DiscoverySubjects),
                Csv(row.EpisodeSubjects),
                Csv(row.SubjectsAdded),
                Csv(row.SubjectsRemoved),
                Csv(row.SubjectsExactMatch),
                Csv(row.SpotifyEpisodeId),
                Csv(row.AppleEpisodeId),
                Csv(row.YouTubeVideoId)));
        }
    }

    private static async Task WriteSummary(string path, IReadOnlyList<DiscoveryTrainingRow> rows, int documentCount)
    {
        var byState = rows
            .GroupBy(x => string.IsNullOrWhiteSpace(x.State) ? "(none)" : x.State, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Count())
            .ToList();

        var accepted = rows.Where(x =>
            string.Equals(x.State, nameof(DiscoveryResultState.Accepted), StringComparison.OrdinalIgnoreCase)).ToList();
        var acceptedJoined = accepted.Where(x => x.EpisodeId != null).ToList();
        var acceptedWithSubjectDelta = acceptedJoined.Where(x => !x.SubjectsExactMatch).ToList();

        var joinMethods = acceptedJoined
            .GroupBy(x => x.JoinMethod, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Count());

        var topAdded = acceptedWithSubjectDelta
            .SelectMany(x => x.SubjectsAdded.Split('|', StringSplitOptions.RemoveEmptyEntries))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Count())
            .Take(20);

        var topRemoved = acceptedWithSubjectDelta
            .SelectMany(x => x.SubjectsRemoved.Split('|', StringSplitOptions.RemoveEmptyEntries))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Count())
            .Take(20);

        var sb = new StringBuilder();
        sb.AppendLine("Discovery training export summary");
        sb.AppendLine($"Generated: {DateTime.UtcNow:O}");
        sb.AppendLine();
        sb.AppendLine($"Documents: {documentCount:N0}");
        sb.AppendLine($"Discovery results: {rows.Count:N0}");
        sb.AppendLine();
        sb.AppendLine("Results by state:");
        foreach (var group in byState)
        {
            sb.AppendLine($"  {group.Key}: {group.Count():N0}");
        }

        sb.AppendLine();
        sb.AppendLine("Accepted → episode join:");
        sb.AppendLine($"  Accepted: {accepted.Count:N0}");
        sb.AppendLine($"  Joined to episode: {acceptedJoined.Count:N0} ({Pct(acceptedJoined.Count, accepted.Count)})");
        sb.AppendLine($"  Join ambiguous (>1 episode): {acceptedJoined.Count(x => x.JoinMatchCount > 1):N0}");
        sb.AppendLine($"  Subjects exact match: {acceptedJoined.Count(x => x.SubjectsExactMatch):N0} ({Pct(acceptedJoined.Count(x => x.SubjectsExactMatch), acceptedJoined.Count)})");
        sb.AppendLine($"  Subjects differ (curator/matcher edits): {acceptedWithSubjectDelta.Count:N0}");

        sb.AppendLine();
        sb.AppendLine("Join method (accepted with episode):");
        foreach (var group in joinMethods)
        {
            sb.AppendLine($"  {group.Key}: {group.Count():N0}");
        }

        sb.AppendLine();
        sb.AppendLine("Top subjects added (accepted, joined, subjects differ):");
        foreach (var group in topAdded)
        {
            sb.AppendLine($"  {group.Key}: {group.Count():N0}");
        }

        sb.AppendLine();
        sb.AppendLine("Top subjects removed (accepted, joined, subjects differ):");
        foreach (var group in topRemoved)
        {
            sb.AppendLine($"  {group.Key}: {group.Count():N0}");
        }

        await File.WriteAllTextAsync(path, sb.ToString());
    }

    private static string Pct(int part, int whole) =>
        whole == 0 ? "n/a" : $"{100.0 * part / whole:F1}%";

    private static string Csv(object? value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        var text = value switch
        {
            DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };

        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
        {
            return $"\"{text.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return text;
    }

    private enum ServiceKey
    {
        Spotify,
        Apple,
        YouTube
    }

    private sealed record EpisodeJoinResult(string Method, int MatchCount, EpisodeRef? Episode);

    private sealed record EpisodeRef(Guid Id, Guid PodcastId, string? Title, IReadOnlyList<string> Subjects);

    private sealed class EpisodeIndex(
        Dictionary<string, List<EpisodeRef>> spotify,
        Dictionary<long, List<EpisodeRef>> apple,
        Dictionary<string, List<EpisodeRef>> youtube,
        int totalEpisodes)
    {
        public int TotalEpisodes { get; } = totalEpisodes;
        public int SpotifyKeys { get; } = spotify.Count;
        public int AppleKeys { get; } = apple.Count;
        public int YouTubeKeys { get; } = youtube.Count;

        public EpisodeJoinResult TryJoin(DiscoveryResult result)
        {
            if (result.Urls.Spotify != null)
            {
                var id = SpotifyIdResolver.GetEpisodeId(result.Urls.Spotify);
                if (!string.IsNullOrWhiteSpace(id) && spotify.TryGetValue(id, out var matches))
                {
                    return new EpisodeJoinResult("spotify", matches.Count, matches[0]);
                }
            }

            if (result.Urls.Apple != null)
            {
                var id = AppleIdResolver.GetEpisodeId(result.Urls.Apple);
                if (id.HasValue && apple.TryGetValue(id.Value, out var matches))
                {
                    return new EpisodeJoinResult("apple", matches.Count, matches[0]);
                }
            }

            if (result.Urls.YouTube != null)
            {
                var id = YouTubeIdResolver.Extract(result.Urls.YouTube);
                if (!string.IsNullOrWhiteSpace(id) && youtube.TryGetValue(id, out var matches))
                {
                    return new EpisodeJoinResult("youtube", matches.Count, matches[0]);
                }
            }

            return new EpisodeJoinResult("none", 0, null);
        }
    }

    private sealed class DiscoveryTrainingRow
    {
        public Guid DocumentId { get; init; }
        public string DocumentState { get; init; } = string.Empty;
        public DateTime DiscoveryBegan { get; init; }
        public string SearchSince { get; init; } = string.Empty;
        public bool IncludeYouTube { get; init; }
        public bool IncludeListenNotes { get; init; }
        public bool IncludeTaddy { get; init; }
        public bool ExcludeSpotify { get; init; }
        public Guid ResultId { get; init; }
        public string State { get; init; } = string.Empty;
        public string? EpisodeName { get; init; }
        public string? ShowName { get; init; }
        public DateTime Released { get; init; }
        public string? Duration { get; init; }
        public string? SpotifyUrl { get; init; }
        public string? AppleUrl { get; init; }
        public string? YouTubeUrl { get; init; }
        public string? SpotifyEpisodeId { get; init; }
        public long? AppleEpisodeId { get; init; }
        public string? YouTubeVideoId { get; init; }
        public string Sources { get; init; } = string.Empty;
        public string DiscoverySubjects { get; init; } = string.Empty;
        public string MatchingPodcastIds { get; init; } = string.Empty;
        public string JoinMethod { get; init; } = "none";
        public int JoinMatchCount { get; init; }
        public Guid? EpisodeId { get; init; }
        public Guid? EpisodePodcastId { get; init; }
        public string? EpisodeTitle { get; init; }
        public string EpisodeSubjects { get; init; } = string.Empty;
        public string SubjectsAdded { get; init; } = string.Empty;
        public string SubjectsRemoved { get; init; } = string.Empty;
        public bool SubjectsExactMatch { get; init; }
    }
}
