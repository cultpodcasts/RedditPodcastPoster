using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Cloudflare;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;

namespace RedditPodcastPoster.UrlShortening;

public class ShortnerService(
    IKVClient kvClient,
    IOptions<ShortnerOptions> shortnerOptions,
    ILogger<ShortnerService> logger) : IShortnerService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ShortnerOptions _shortnerOptions = shortnerOptions.Value;

    public async Task<WriteResult> Write(IEnumerable<PodcastEpisode> podcastEpisodes)
    {
        logger.LogInformation("{WriteName}. Writing to KV. Bulk write: {Count} episodes.", nameof(Write), podcastEpisodes.Count());
        var items = podcastEpisodes.Select(x =>
            new ShortUrlRecord(
                x.Podcast.PodcastNameInSafeUrlForm(),
                x.Episode.Id,
                x.Episode.Id.ToBase64(),
                x.Episode.Title,
                DateOnly.FromDateTime(x.Episode.Release),
                x.Episode.Length));
        var kvRecords = items.Select(item => new KVRecord
        {
            Key = item.Base64EpisodeKey,
            Value = $"{item.PodcastName}/{item.EpisodeId}",
            Metadata = new MetaData
            { EpisodeTitle = item.EpisodeTitle, ReleaseDate = item.ReleaseDate, Duration = item.Duration }
        }).ToArray();
        return await kvClient.Write(kvRecords, _shortnerOptions.KVShortnerNamespaceId);
    }

    public async Task<WriteResult> Write(PodcastEpisode podcastEpisode, bool isDryRun = false)
    {
        logger.LogInformation(
            "{WriteName}. Writing to KV. Individual write. Episode-id '{EpisodeId}'.", nameof(Write), podcastEpisode.Episode.Id);
        var item = new ShortUrlRecord(
            podcastEpisode.Podcast.PodcastNameInSafeUrlForm(),
            podcastEpisode.Episode.Id,
            podcastEpisode.Episode.Id.ToBase64(),
            podcastEpisode.Episode.Title,
            DateOnly.FromDateTime(podcastEpisode.Episode.Release),
            podcastEpisode.Episode.Length);
        var kvRecord = new KVRecord
        {
            Key = item.Base64EpisodeKey,
            Value = $"{item.PodcastName}/{item.EpisodeId}",
            Metadata = new MetaData { 
                EpisodeTitle = item.EpisodeTitle, 
                ReleaseDate = item.ReleaseDate, 
                Duration = item.Duration 
            }
        };

        if (!isDryRun)
        {
            var result = await kvClient.Write(kvRecord, _shortnerOptions.KVShortnerNamespaceId);
            if (result.Success)
            {
                var url = new Uri($"{_shortnerOptions.ShortnerUrl}{podcastEpisode.Episode.Id.ToBase64()}");
                result = result with { Url = url };
            }
            return result;
        }
        logger.LogInformation(JsonSerializer.Serialize(kvRecord));
        return new WriteResult(true);
    }

    public async Task<KVRecord?> Read(string requestKey)
    {
        return await kvClient.ReadWithMetaData(requestKey, _shortnerOptions.KVShortnerNamespaceId);
    }
}