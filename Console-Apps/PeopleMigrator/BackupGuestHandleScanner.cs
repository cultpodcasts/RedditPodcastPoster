using System.Collections.Concurrent;
using System.Text.Json;

namespace PeopleMigrator;

/// <summary>
/// Scans a CosmosDbDownloader episode backup folder for twitterHandles / blueskyHandles.
/// </summary>
internal static class BackupGuestHandleScanner
{
    public static async Task<IReadOnlyList<GuestHandleEpisode>> ScanAsync(
        string backupPath,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(backupPath))
        {
            throw new DirectoryNotFoundException($"Backup directory not found: {backupPath}");
        }

        var result = new ConcurrentBag<GuestHandleEpisode>();
        var files = Directory.EnumerateFiles(backupPath, "*.json").ToArray();

        await Parallel.ForEachAsync(
            files,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Environment.ProcessorCount
            },
            async (file, token) =>
            {
                if (!FileContainsUtf8(file, "\"twitterHandles\"") &&
                    !FileContainsUtf8(file, "\"blueskyHandles\""))
                {
                    return;
                }

                var text = await File.ReadAllTextAsync(file, token);
                using var document = JsonDocument.Parse(text);
                var root = document.RootElement;

                var twitterHandles = ReadStringArray(root, "twitterHandles");
                var blueskyHandles = ReadStringArray(root, "blueskyHandles");
                if (twitterHandles is not { Length: > 0 } && blueskyHandles is not { Length: > 0 })
                {
                    return;
                }

                var episodeId = Guid.TryParse(Path.GetFileNameWithoutExtension(file), out var parsed)
                    ? parsed
                    : (Guid?)null;

                result.Add(new GuestHandleEpisode(twitterHandles, blueskyHandles, episodeId));
            });

        return result.ToList();
    }

    private static bool FileContainsUtf8(string path, string needle)
    {
        using var stream = File.OpenRead(path);
        var buffer = new byte[8192];
        var needleBytes = System.Text.Encoding.UTF8.GetBytes(needle);
        var matched = 0;

        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                matched = buffer[i] == needleBytes[matched] ? matched + 1 : 0;
                if (matched == needleBytes.Length)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string[]? ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var values = new List<string>();
        foreach (var element in property.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }
        }

        return values.Count == 0 ? null : values.ToArray();
    }
}
