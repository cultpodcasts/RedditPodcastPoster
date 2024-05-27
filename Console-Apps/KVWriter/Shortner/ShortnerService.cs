using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.ContentPublisher.Configuration;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Models.Extensions;

namespace KVWriter.Shortner;

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
        var url = BulkWriteUrl(_cloudFlareOptions.AccountId, _cloudFlareOptions.KVShortnerNamespaceId);
        using var request = new HttpRequestMessage();
        request.Method = HttpMethod.Put;
        request.RequestUri = url;
        request.Headers.Add("Authorization", $"Bearer {_cloudFlareOptions.KVApiToken}");
        var kvRecords = items.Select(x => new KVRecord
        {
            Key = x.Base64EpisodeKey,
            Value = $"{x.PodcastName}/{x.EpisodeId}"
        }).ToArray();
        var requestContent = JsonContent.Create(kvRecords, options: new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });


        request.Content = requestContent;
        var result = await httpClient.SendAsync(request);

        var json = await result.Content.ReadAsStringAsync();


        return new WriteResult(result.StatusCode == HttpStatusCode.OK);
    }

    private Uri BulkWriteUrl(string accountId, string namespaceId)
    {
        return new Uri(
            $"https://api.cloudflare.com/client/v4/accounts/{accountId}/storage/kv/namespaces/{namespaceId}/bulk");
    }
}