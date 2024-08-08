using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;

namespace RedditPodcastPoster.UrlShortening;

public class ShortnerService(
    HttpClient httpClient,
    IOptions<CloudFlareOptions> cloudFlareOptions,
    IOptions<ShortnerOptions> shortnerOptions,
    ILogger<ShortnerService> logger) : IShortnerService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly CloudFlareOptions _cloudFlareOptions = cloudFlareOptions.Value;
    private readonly ShortnerOptions _shortnerOptions = shortnerOptions.Value;

    public async Task<WriteResult> Write(IEnumerable<PodcastEpisode> podcastEpisodes)
    {
        logger.LogInformation($"{nameof(Write)}. Writing to KV. Bulk write: {podcastEpisodes.Count()} episodes.");
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
                {EpisodeTitle = item.EpisodeTitle, ReleaseDate = item.ReleaseDate, Duration = item.Duration}
        }).ToArray();

        var url = GetBulkWriteUrl(_cloudFlareOptions.AccountId, _cloudFlareOptions.KVShortnerNamespaceId);
        using var request = new HttpRequestMessage();
        request.Method = HttpMethod.Put;
        request.RequestUri = url;
        request.Headers.Add("Authorization", $"Bearer {_cloudFlareOptions.KVApiToken}");

        var requestContent = JsonContent.Create(kvRecords, options: JsonSerializerOptions);

        request.Content = requestContent;
        var result = await httpClient.SendAsync(request);
        if (result.StatusCode != HttpStatusCode.OK)
        {
            logger.LogError(
                $"{nameof(Write)} KV-write unsuccessful. Write-Bulk. Status-code: {result.StatusCode}. Response-body '{await result.Content.ReadAsStringAsync()}'.");
        }

        return new WriteResult(result.StatusCode == HttpStatusCode.OK);
    }

    public async Task<WriteResult> Write(PodcastEpisode podcastEpisode, bool isDryRun = false)
    {
        logger.LogInformation(
            $"{nameof(Write)}. Writing to KV. Individual write. Episode-id '{podcastEpisode.Episode.Id}'.");
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
            Metadata = new MetaData
                {EpisodeTitle = item.EpisodeTitle, ReleaseDate = item.ReleaseDate, Duration = item.Duration}
        };

        if (!isDryRun)
        {
            var url = WriteUrl(_cloudFlareOptions.AccountId, _cloudFlareOptions.KVShortnerNamespaceId, kvRecord.Key);
            using var request = new HttpRequestMessage();
            request.Method = HttpMethod.Put;
            request.RequestUri = url;
            request.Headers.Add("Authorization", $"Bearer {_cloudFlareOptions.KVApiToken}");
            var requestContent = new MultipartFormDataContent();
            requestContent.Add(new StringContent(kvRecord.Value), "value");
            var metaData = JsonSerializer.Serialize(kvRecord.Metadata, JsonSerializerOptions);
            requestContent.Add(new StringContent(metaData), "metadata");
            request.Content = requestContent;
            var result = await httpClient.SendAsync(request);
            if (result.StatusCode != HttpStatusCode.OK)
            {
                logger.LogError(
                    $"{nameof(Write)} KV-write unsuccessful. Write-Single. Status-code: {result.StatusCode}. Response-body '{await result.Content.ReadAsStringAsync()}'.");
            }

            return new WriteResult(
                result.StatusCode == HttpStatusCode.OK,
                new Uri($"{_shortnerOptions.ShortnerUrl}{podcastEpisode.Episode.Id.ToBase64()}"));
        }

        logger.LogInformation(JsonSerializer.Serialize(kvRecord));

        return new WriteResult(true);
    }

    public async Task<object> Read(string requestKey)
    {
        logger.LogInformation($"{nameof(Write)}. Reading from KV. Key '{requestKey}'.");
        var url = ReadMetadata(_cloudFlareOptions.AccountId, _cloudFlareOptions.KVShortnerNamespaceId, requestKey);
        using var request = new HttpRequestMessage();
        request.Method = HttpMethod.Get;
        request.RequestUri = url;
        request.Headers.Add("Authorization", $"Bearer {_cloudFlareOptions.KVApiToken}");
        var result = await httpClient.SendAsync(request);
        if (result.StatusCode != HttpStatusCode.OK)
        {
            logger.LogError(
                $"{nameof(Write)} KV-write unsuccessful. Read-Key. Status-code: {result.StatusCode}. Response-body '{await result.Content.ReadAsStringAsync()}'.");
        }

        var json = await result.Content.ReadAsStringAsync();
        return new object();
    }

    private Uri GetBulkWriteUrl(string accountId, string namespaceId)
    {
        return new Uri(
            $"https://api.cloudflare.com/client/v4/accounts/{accountId}/storage/kv/namespaces/{namespaceId}/bulk");
    }

    private Uri WriteUrl(string accountId, string namespaceId, string keyName)
    {
        return new Uri(
            $"https://api.cloudflare.com/client/v4/accounts/{accountId}/storage/kv/namespaces/{namespaceId}/values/{keyName}");
    }

    private Uri ReadMetadata(string accountId, string namespaceId, string keyName)
    {
        return new Uri(
            $"https://api.cloudflare.com/client/v4/accounts/{accountId}/storage/kv/namespaces/{namespaceId}/metadata/{keyName}");
    }
}