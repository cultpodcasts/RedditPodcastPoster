using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.ContentPublisher.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;

namespace RedditPodcastPoster.UrlShortening;

public class ShortnerService(
    HttpClient httpClient,
    IOptions<CloudFlareOptions> cloudFlareOptions,
    ILogger<ShortnerService> logger) : IShortnerService
{
    private readonly CloudFlareOptions _cloudFlareOptions = cloudFlareOptions.Value;

    public async Task<WriteResult> Write(IEnumerable<PodcastEpisode> podcastEpisodes)
    {
        var items = podcastEpisodes.Select(x =>
            new ShortUrlRecord(x.Podcast.PodcastNameInSafeUrlForm(), x.Episode.Id, x.Episode.Id.ToBase64()));
        var kvRecords = items.Select(x => new KVRecord
        {
            Key = x.Base64EpisodeKey,
            Value = $"{x.PodcastName}/{x.EpisodeId}"
        }).ToArray();

        logger.LogInformation(
            $"Cloudflare-options: account-id-length '{_cloudFlareOptions.AccountId.Length}', KVShortnerNamespaceId-length: '{_cloudFlareOptions.KVShortnerNamespaceId.Length}', KVApiToken-length: '{_cloudFlareOptions.KVApiToken.Length}'.");

        var url = BulkWriteUrl(_cloudFlareOptions.AccountId, _cloudFlareOptions.KVShortnerNamespaceId);
        using var request = new HttpRequestMessage();
        request.Method = HttpMethod.Put;
        request.RequestUri = url;
        request.Headers.Add("Authorization", $"Bearer {_cloudFlareOptions.KVApiToken}");

        var requestContent = JsonContent.Create(kvRecords, options: new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

        request.Content = requestContent;
        var result = await httpClient.SendAsync(request);
        if (result.StatusCode != HttpStatusCode.OK)
        {
            logger.LogError(
                $"{nameof(Write)} KV-write unsuccessful. Status-code: {result.StatusCode}. Response-body '{await result.Content.ReadAsStringAsync()}'.");
        }

        return new WriteResult(result.StatusCode == HttpStatusCode.OK);
    }

    public async Task<WriteResult> Write(PodcastEpisode podcastEpisode)
    {
        var item = new ShortUrlRecord(podcastEpisode.Podcast.PodcastNameInSafeUrlForm(), podcastEpisode.Episode.Id,
            podcastEpisode.Episode.Id.ToBase64());
        var kvRecord = new KVRecord
        {
            Key = item.Base64EpisodeKey,
            Value = $"{item.PodcastName}/{item.EpisodeId}"
        };

        logger.LogInformation(
            $"Cloudflare-options: account-id-length '{_cloudFlareOptions.AccountId.Length}', KVShortnerNamespaceId-length: '{_cloudFlareOptions.KVShortnerNamespaceId.Length}', KVApiToken-length: '{_cloudFlareOptions.KVApiToken.Length}'.");

        var url = WriteUrl(_cloudFlareOptions.AccountId, _cloudFlareOptions.KVShortnerNamespaceId, kvRecord.Key);
        using var request = new HttpRequestMessage();
        request.Method = HttpMethod.Put;
        request.RequestUri = url;
        request.Headers.Add("Authorization", $"Bearer {_cloudFlareOptions.KVApiToken}");
        var requestContent = new MultipartFormDataContent();
        requestContent.Add(new StringContent(kvRecord.Value), "value");
        requestContent.Add(new StringContent("{}"), "metadata");
        request.Content = requestContent;
        var result = await httpClient.SendAsync(request);
        if (result.StatusCode != HttpStatusCode.OK)
        {
            logger.LogError(
                $"{nameof(Write)} KV-write unsuccessful. Status-code: {result.StatusCode}. Response-body '{await result.Content.ReadAsStringAsync()}'.");
        }

        return new WriteResult(result.StatusCode == HttpStatusCode.OK);
    }

    private Uri BulkWriteUrl(string accountId, string namespaceId)
    {
        return new Uri(
            $"https://api.cloudflare.com/client/v4/accounts/{accountId}/storage/kv/namespaces/{namespaceId}/bulk");
    }

    private Uri WriteUrl(string accountId, string namespaceId, string keyName)
    {
        return new Uri(
            $"https://api.cloudflare.com/client/v4/accounts/{accountId}/storage/kv/namespaces/{namespaceId}/values/{keyName}");
    }
}