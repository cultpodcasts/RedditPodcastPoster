using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Catalogue.Authority.Imdb;

public sealed class ImdbSuggestionClient(
    HttpClient httpClient,
    ILogger<ImdbSuggestionClient> logger) : IImdbSuggestionClient
{
    public static readonly Uri BaseAddress = new("https://v3.sg.media-imdb.com/");

    public async Task<IReadOnlyList<ImdbSuggestion>> SuggestAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        var trimmed = query?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return [];
        }

        var path = SuggestionPath(trimmed);
        try
        {
            using var response = await httpClient.GetAsync(path, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "IMDb suggestion returned {Status}. query={Query}",
                    (int)response.StatusCode,
                    trimmed);
                return [];
            }

            var body = await response.Content
                .ReadFromJsonAsync<SuggestionResponse>(cancellationToken)
                .ConfigureAwait(false);
            return Map(body);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or NotSupportedException ||
            (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            logger.LogWarning(exception, "IMDb suggestion failed. query={Query}", trimmed);
            return [];
        }
    }

    /// <summary>
    /// IMDb buckets suggestions by the first letter of the query; any bucket answers, so
    /// a non-letter start uses <c>x</c>.
    /// </summary>
    public static string SuggestionPath(string query)
    {
        var first = char.ToLowerInvariant(query[0]);
        var bucket = first is >= 'a' and <= 'z' ? first : 'x';
        return $"suggestion/{bucket}/{Uri.EscapeDataString(query)}.json";
    }

    internal static IReadOnlyList<ImdbSuggestion> Map(SuggestionResponse? body) =>
        (body?.Results ?? [])
            .Where(row => row.Id is { } id &&
                          id.StartsWith("tt", StringComparison.Ordinal) &&
                          !string.IsNullOrWhiteSpace(row.Label))
            .Select(row => new ImdbSuggestion(
                row.Id!,
                row.Label!.Trim(),
                row.TypeId,
                row.Year is > 0 ? row.Year : null,
                Uri.TryCreate(row.Image?.ImageUrl, UriKind.Absolute, out var image) ? image : null,
                string.IsNullOrWhiteSpace(row.Cast) ? null : row.Cast.Trim()))
            .ToList();

    internal sealed class SuggestionResponse
    {
        [JsonPropertyName("d")]
        public List<SuggestionRow>? Results { get; set; }
    }

    internal sealed class SuggestionRow
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("l")]
        public string? Label { get; set; }

        [JsonPropertyName("qid")]
        public string? TypeId { get; set; }

        [JsonPropertyName("y")]
        public int? Year { get; set; }

        [JsonPropertyName("s")]
        public string? Cast { get; set; }

        [JsonPropertyName("i")]
        public SuggestionImage? Image { get; set; }
    }

    internal sealed class SuggestionImage
    {
        [JsonPropertyName("imageUrl")]
        public string? ImageUrl { get; set; }
    }
}
