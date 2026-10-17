using System.Text.Json;

namespace CultPodcasts.DatabasePublisher.Consistency;

/// <summary>A file of the previous publish: its file key (name without extension) and the id inside it.</summary>
public sealed record BaselineFile(string FileKey, Guid? Id);

public static class PublicationBaseline
{
    /// <summary>
    /// Reads the top-level <c>*.json</c> files of a previous publish, the same files update.ps1 clears,
    /// taking only the <c>id</c> from each.
    /// </summary>
    public static async Task<IReadOnlyList<BaselineFile>> ReadAsync(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Baseline folder '{directory}' does not exist.");
        }

        var files = new List<BaselineFile>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                     .Order(StringComparer.Ordinal))
        {
            files.Add(new BaselineFile(Path.GetFileNameWithoutExtension(path), await ReadIdAsync(path)));
        }

        return files;
    }

    private static async Task<Guid?> ReadIdAsync(string path)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(stream);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty("id", out var id) &&
                   id.ValueKind == JsonValueKind.String &&
                   id.TryGetGuid(out var guid)
                ? guid
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
