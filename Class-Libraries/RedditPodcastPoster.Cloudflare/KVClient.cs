using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Configuration;

namespace RedditPodcastPoster.Cloudflare;

public class KVClient(
    HttpClient httpClient,
    IOptions<CloudFlareOptions> cloudFlareOptions,
    ILogger<KVClient> logger
    ) : IKVClient
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly CloudFlareOptions _cloudFlareOptions = cloudFlareOptions.Value;

    public async Task<KVRecord?> ReadWithMetaData(string key, Func<CloudFlareOptions, string> selector)
    {
        logger.LogInformation($"{nameof(ReadWithMetaData)}. Reading from KV. Key '{key}'.");
        Uri url = GetReadMetadataUrl(_cloudFlareOptions.AccountId, selector(_cloudFlareOptions), key);
        var urlS = url.ToString();
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
        return JsonSerializer.Deserialize<KVRecord>(json);
    }

    public async Task<string?> Read(string key, Func<CloudFlareOptions, string> selector)
    {
        logger.LogInformation($"{nameof(ReadWithMetaData)}. Reading from KV. Key '{key}'.");
        Uri url = GetReadUrl(_cloudFlareOptions.AccountId, selector(_cloudFlareOptions), key);
        var urlS = url.ToString();
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

        var text = await result.Content.ReadAsStringAsync();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }
        return text;
    }


    public async Task<WriteResult> Write(IEnumerable<KVRecord> records, Func<CloudFlareOptions, string> selector)
    {

        var url = GetBulkWriteUrl(_cloudFlareOptions.AccountId, selector(_cloudFlareOptions));
        using var request = new HttpRequestMessage();
        request.Method = HttpMethod.Put;
        request.RequestUri = url;
        request.Headers.Add("Authorization", $"Bearer {_cloudFlareOptions.KVApiToken}");

        var requestContent = JsonContent.Create(records, options: JsonSerializerOptions);
        request.Content = requestContent;
        var result = await httpClient.SendAsync(request);
        if (result.StatusCode != HttpStatusCode.OK)
        {
            logger.LogError(
                $"{nameof(Write)} KV-write unsuccessful. Write-Bulk. Status-code: {result.StatusCode}. Response-body '{await result.Content.ReadAsStringAsync()}'.");
        }
        return new WriteResult(result.StatusCode == HttpStatusCode.OK);
    }

    public async Task<WriteResult> Write(KVRecord record, Func<CloudFlareOptions, string> selector)
    {
        var url = GetWriteUrl(_cloudFlareOptions.AccountId, selector(_cloudFlareOptions), record.Key);
        using var request = new HttpRequestMessage();
        request.Method = HttpMethod.Put;
        request.RequestUri = url;
        request.Headers.Add("Authorization", $"Bearer {_cloudFlareOptions.KVApiToken}");
        var requestContent = new MultipartFormDataContent();
        requestContent.Add(new StringContent(record.Value), "value");
        var metaData = JsonSerializer.Serialize(record.Metadata, JsonSerializerOptions);
        requestContent.Add(new StringContent(metaData), "metadata");
        request.Content = requestContent;
        var result = await httpClient.SendAsync(request);
        if (result.StatusCode != HttpStatusCode.OK)
        {
            logger.LogError(
                $"{nameof(Write)} KV-write unsuccessful. Write-Single. Status-code: {result.StatusCode}. Response-body '{await result.Content.ReadAsStringAsync()}'.");
        }

        return new WriteResult(result.StatusCode == HttpStatusCode.OK);
    }

    public async Task<IDictionary<string, string>> GetAll(Func<CloudFlareOptions, string> selector)
    {
        var url = GetAllKeysUrl(_cloudFlareOptions.AccountId, selector(_cloudFlareOptions));
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
        var list = JsonSerializer.Deserialize<KVList>(json);

        if (list != null)
        {
            var records = new Dictionary<string, string>();
            foreach (var key in list.Result)
            {
                var value = await Read(key.Name, selector);
                if (value == null)
                {
                    throw new InvalidOperationException($"Unable to parse kv-record with key '{key.Name}'.");
                }
                records.Add(key.Name, value);
            }
            return records;
        }
        else
        {
            throw new InvalidOperationException("Unable to parse kv-key-list respons");
        }
    }


    private Uri GetBulkWriteUrl(string accountId, string namespaceId)
    {
        return new Uri(
            $"https://api.cloudflare.com/client/v4/accounts/{accountId}/storage/kv/namespaces/{namespaceId}/bulk");
    }

    private Uri GetWriteUrl(string accountId, string namespaceId, string keyName)
    {
        return new Uri(
            $"https://api.cloudflare.com/client/v4/accounts/{accountId}/storage/kv/namespaces/{namespaceId}/values/{Uri.EscapeDataString(keyName)}");
    }

    private Uri GetReadMetadataUrl(string accountId, string namespaceId, string keyName)
    {
        var keyArg = Uri.EscapeDataString(keyName);
        string uriString = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/storage/kv/namespaces/{namespaceId}/metadata/{keyArg}";
        Uri uri = new Uri(uriString);
        return uri;
    }
    private Uri GetReadUrl(string accountId, string namespaceId, string keyName)
    {
        var keyArg = Uri.EscapeDataString(keyName);
        string uriString = $"https://api.cloudflare.com/client/v4/accounts/{accountId}/storage/kv/namespaces/{namespaceId}/values/{keyArg}";
        Uri uri = new Uri(uriString);
        return uri;
    }

    private Uri GetAllKeysUrl(string accountId, string namespaceId)
    {
        return new Uri(
            $"https://api.cloudflare.com/client/v4/accounts/{accountId}/storage/kv/namespaces/{namespaceId}/keys");
    }
}
