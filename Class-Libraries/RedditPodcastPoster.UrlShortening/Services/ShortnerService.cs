using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Cloudflare.Clients;
using RedditPodcastPoster.Cloudflare.Models;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.Extensions;
using RedditPodcastPoster.UrlShortening.Configuration;
using RedditPodcastPoster.UrlShortening.Models;
using RedditPodcastPoster.UrlShortening.Extensions;

namespace RedditPodcastPoster.UrlShortening.Services;

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
        var toWrite = new List<KVRecord>();
        foreach (var podcastEpisode in podcastEpisodes)
        {
            var key = podcastEpisode.Episode.Id.ToBase64();
            var existing = await kvClient.ReadWithMetaData(key, _shortnerOptions.KVShortnerNamespaceId);
            if (existing != null)
            {
                logger.LogInformation(
                    "{WriteName}. Skipping existing shortner key '{Key}' (leave alone).", nameof(Write), key);
                continue;
            }

            toWrite.Add(CreateRecord(podcastEpisode));
        }

        if (toWrite.Count == 0)
        {
            return new WriteResult(true);
        }

        return await kvClient.Write(toWrite, _shortnerOptions.KVShortnerNamespaceId);
    }

    public async Task<WriteResult> Write(PodcastEpisode podcastEpisode, bool isDryRun = false)
    {
        logger.LogInformation(
            "{WriteName}. Writing to KV. Individual write. Episode-id '{EpisodeId}'.", nameof(Write), podcastEpisode.Episode.Id);
        var key = podcastEpisode.Episode.Id.ToBase64();
        var shortUrl = new Uri($"{_shortnerOptions.ShortnerUrl}{key}");

        if (!isDryRun)
        {
            var existing = await kvClient.ReadWithMetaData(key, _shortnerOptions.KVShortnerNamespaceId);
            if (existing != null)
            {
                logger.LogInformation(
                    "{WriteName}. Shortner key '{Key}' already exists; leaving unchanged.", nameof(Write), key);
                var hasShareImage = !string.IsNullOrEmpty(existing.Metadata?.Image);
                return new WriteResult(true, shortUrl, hasShareImage);
            }
        }

        var kvRecord = CreateRecord(podcastEpisode);
        var newHasShareImage = !string.IsNullOrEmpty(kvRecord.Metadata?.Image);

        if (!isDryRun)
        {
            var result = await kvClient.Write(kvRecord, _shortnerOptions.KVShortnerNamespaceId);
            if (result.Success)
            {
                result = result with { Url = shortUrl, HasShareImage = newHasShareImage };
            }
            return result;
        }

        logger.LogInformation(JsonSerializer.Serialize(kvRecord, JsonSerializerOptions));
        return new WriteResult(true, shortUrl, newHasShareImage);
    }

    public async Task<KVRecord?> Read(string requestKey)
    {
        return await kvClient.ReadWithMetaData(requestKey, _shortnerOptions.KVShortnerNamespaceId);
    }

    public async Task<DeleteResult> Delete(PodcastEpisode podcastEpisode)
    {
        return await kvClient.Delete(podcastEpisode.Episode.Id.ToBase64(), _shortnerOptions.KVShortnerNamespaceId);
    }

    public async Task<DeleteResult> Delete(IEnumerable<PodcastEpisode> podcastEpisodes)
    {
        return await kvClient.Delete(podcastEpisodes.Select(x => x.Episode.Id.ToBase64()), _shortnerOptions.KVShortnerNamespaceId);
    }

    private static KVRecord CreateRecord(PodcastEpisode podcastEpisode)
    {
        var item = new ShortUrlRecord(
            podcastEpisode.Podcast.PodcastNameInSafeUrlForm(),
            podcastEpisode.Episode.Id,
            podcastEpisode.Episode.Id.ToBase64(),
            podcastEpisode.Episode.Title,
            DateOnly.FromDateTime(podcastEpisode.Episode.Release),
            podcastEpisode.Episode.Length);
        var metadata = new MetaData
        {
            EpisodeTitle = item.EpisodeTitle,
            ReleaseDate = item.ReleaseDate,
            Duration = item.Duration
        };
        ShortnerShareImageMetadata.Apply(metadata, podcastEpisode.Episode);
        return new KVRecord
        {
            Key = item.Base64EpisodeKey,
            Value = $"{item.PodcastName}/{item.EpisodeId}",
            Metadata = metadata
        };
    }
}
