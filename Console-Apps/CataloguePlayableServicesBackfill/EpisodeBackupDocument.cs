using System.Text.Json;
using System.Text.Json.Serialization;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Podcasts;
using RedditPodcastPoster.Models.Services;

namespace CataloguePlayableServicesBackfill;

/// <summary>
/// Read-only episode backup JSON shape: catalog <c>services</c> plus leftover <c>urls</c>/<c>images</c>.
/// Never persist this type.
/// </summary>
public sealed class EpisodeBackupDocument
{
    [JsonPropertyName("services")]
    public Dictionary<string, ServiceLink>? Services { get; set; }

    [JsonPropertyName("urls")]
    public ServiceUrls? Urls { get; set; }

    [JsonPropertyName("images")]
    public EpisodeImages? Images { get; set; }

    public static bool TryParse(string json, out EpisodeBackupDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            document = JsonSerializer.Deserialize<EpisodeBackupDocument>(json, BackupDocumentJsonOptions.Instance);
        }
        catch (JsonException)
        {
            return false;
        }

        return document is not null;
    }
}
