using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

    public async Task<KVRecord?> ReadWithMetaData(string key, string namespaceId)
    {
        logger.LogInformation("{ReadWithMetaDataName}. Reading from KV. Key '{Key}'.", nameof(ReadWithMetaData), key);
        var url = GetReadMetadataUrl(_cloudFlareOptions.AccountId, namespaceId, key);
        var urlS = url.ToString();
        using var request = new HttpRequestMessage();
        request.Method = HttpMethod.Get;
        request.RequestUri = url;
        request.Headers.Add("Authorization", $"Bearer {_cloudFlareOptions.KVApiToken}");
        var result = await httpClient.SendAsync(request);
        if (result.StatusCode != HttpStatusCode.OK)
        {
            logger.LogError(
                "{WriteName} KV-write unsuccessful. Read-Key. Status-code: {ResultStatusCode}. Response-body '{ReadAsStringAsync}'.",
                nameof(Write), result.StatusCode, await result.Content.ReadAsStringAsync());
        }

        var json = await result.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<KVRecord>(json);
    }

    public async Task<string?> Read(string key, string namespaceId)
    {
        logger.LogInformation("{ReadWithMetaDataName}. Reading from KV. Key '{Key}'.", nameof(ReadWithMetaData), key);
        var url = GetReadUrl(_cloudFlareOptions.AccountId, namespaceId, key);
        var urlS = url.ToString();
        using var request = new HttpRequestMessage();
        request.Method = HttpMethod.Get;
        request.RequestUri = url;
        request.Headers.Add("Authorization", $"Bearer {_cloudFlareOptions.KVApiToken}");
        var result = await httpClient.SendAsync(request);
        if (result.StatusCode != HttpStatusCode.OK)
        {
            logger.LogError(
                "{WriteName} KV-write unsuccessful. Read-Key. Status-code: {ResultStatusCode}. Response-body '{ReadAsStringAsync}'.",
                nameof(Write), result.StatusCode, await result.Content.ReadAsStringAsync());
        }

        var text = await result.Content.ReadAsStringAsync();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text;
    }

    public async Task<WriteResult> Write(IEnumerable<KVRecord> records, string namespaceId)
    {
        var url = GetBulkWriteUrl(_cloudFlareOptions.AccountId, namespaceId);
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
                "{WriteName} KV-write unsuccessful. Write-Bulk. Status-code: {ResultStatusCode}. Response-body '{ReadAsStringAsync}'.",
                nameof(Write), result.StatusCode, await result.Content.ReadAsStringAsync());
        }

        return new WriteResult(result.StatusCode == HttpStatusCode.OK);
    }

    public async Task<WriteResult> Write(KVRecord record, string namespaceId)
    {
        var url = GetWriteUrl(_cloudFlareOptions.AccountId, namespaceId, record.Key);
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
                "{WriteName} KV-write unsuccessful. Write-Single. Status-code: {ResultStatusCode}. Response-body '{ReadAsStringAsync}'.",
                nameof(Write), result.StatusCode, await result.Content.ReadAsStringAsync());
        }

        return new WriteResult(result.StatusCode == HttpStatusCode.OK);
    }

    public async Task<IDictionary<string, string>> GetAll(string namespaceId)
    {
        var url = GetAllKeysUrl(_cloudFlareOptions.AccountId, namespaceId);
        using var request = new HttpRequestMessage();
        request.Method = HttpMethod.Get;
        request.RequestUri = url;
        request.Headers.Add("Authorization", $"Bearer {_cloudFlareOptions.KVApiToken}");
        var result = await httpClient.SendAsync(request);
        if (result.StatusCode != HttpStatusCode.OK)
        {
            logger.LogError(
                "{WriteName} KV-write unsuccessful. Read-Key. Status-code: {ResultStatusCode}. Response-body '{ReadAsStringAsync}'.",
                nameof(Write), result.StatusCode, await result.Content.ReadAsStringAsync());
        }

        var json = await result.Content.ReadAsStringAsync();
        var list = JsonSerializer.Deserialize<KVList>(json);

        if (list != null)
        {
            var records = new Dictionary<string, string>();
            foreach (var key in list.Result)
            {
                var value = await Read(key.Name, namespaceId);
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
            throw new InvalidOperationException("Unable to parse kv-key-list response");
        }
    }

    public async Task<DeleteResult> Delete(string keyName, string namespaceId)
    {
        var url = GetKeyDeleteUrl(_cloudFlareOptions.AccountId, namespaceId, keyName);
        using var request = new HttpRequestMessage();
        request.Method = HttpMethod.Delete;
        request.RequestUri = url;
        request.Headers.Add("Authorization", $"Bearer {_cloudFlareOptions.KVApiToken}");
        try
        {
            var result = await httpClient.SendAsync(request);
            if (result.StatusCode != HttpStatusCode.OK)
            {
                logger.LogError(
                    "{method} KV-delete unsuccessful. Delete-Key '{KeyName}'. Status-code: {ResultStatusCode}. Response-body '{ReadAsStringAsync}'.",
                    nameof(Delete), keyName, result.StatusCode, await result.Content.ReadAsStringAsync());
            }

            return new DeleteResult(result.StatusCode == HttpStatusCode.OK);
        } catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{method} KV-delete unsuccessful. Delete-Key '{KeyName}'. Exception occurred.",
                nameof(Delete), keyName);
            return new DeleteResult(false);
        }
    }

    public async Task<DeleteResult> Delete(IEnumerable<string> keys, string namespaceId)
    {
        var url = GetKeyBulkDeleteUrl(_cloudFlareOptions.AccountId, namespaceId);
        using var request = new HttpRequestMessage();
        request.Method = HttpMethod.Post;
        request.RequestUri = url;
        request.Headers.Add("Authorization", $"Bearer {_cloudFlareOptions.KVApiToken}");
        request.Content = new StringContent(JsonSerializer.Serialize(keys), Encoding.UTF8, "application/json");
        try
        {
            var result = await httpClient.SendAsync(request);
            if (result.StatusCode != HttpStatusCode.OK)
            {
                logger.LogError(
                    "{method} KV-bulk-delete unsuccessful. Delete-Keys. Status-code: {ResultStatusCode}. Response-body '{ReadAsStringAsync}'. Keys: {Keys}",
                    nameof(Delete), result.StatusCode, await result.Content.ReadAsStringAsync(), string.Join(", ", keys));
            }

            return new DeleteResult(result.StatusCode == HttpStatusCode.OK);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "{method} KV-delete unsuccessful. Delete-Keys. Exception occurred. Keys: {Keys}",
                nameof(Delete), string.Join(", ", keys));
            return new DeleteResult(false);
        }
    }


    private Uri GetKeyBulkDeleteUrl(string accountId, string namespaceId)
    {
        return new Uri(
            $"https://api.cloudflare.com/client/v4/accounts/{accountId}/storage/kv/namespaces/{namespaceId}/bulk/delete");
    }

    private Uri GetKeyDeleteUrl(string accountId, string namespaceId, string keyName)
    {
        return new Uri(
            $"https://api.cloudflare.com/client/v4/accounts/{accountId}/storage/kv/namespaces/{namespaceId}/values/{keyName}");
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
        var uriString =
            $"https://api.cloudflare.com/client/v4/accounts/{accountId}/storage/kv/namespaces/{namespaceId}/metadata/{keyArg}";
        var uri = new Uri(uriString);
        return uri;
    }

    private Uri GetReadUrl(string accountId, string namespaceId, string keyName)
    {
        var keyArg = Uri.EscapeDataString(keyName);
        var uriString =
            $"https://api.cloudflare.com/client/v4/accounts/{accountId}/storage/kv/namespaces/{namespaceId}/values/{keyArg}";
        var uri = new Uri(uriString);
        return uri;
    }

    private Uri GetAllKeysUrl(string accountId, string namespaceId)
    {
        return new Uri(
            $"https://api.cloudflare.com/client/v4/accounts/{accountId}/storage/kv/namespaces/{namespaceId}/keys");
    }
}