using System.IO.Compression;
using Microsoft.Extensions.Logging;
using X.Web.MetaExtractor;
using X.Web.MetaExtractor.ContentLoaders.HttpClient;

namespace X.Bluesky;

public class HttpClientPageContentLoader : IPageContentLoader
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;
    private readonly string _httpClientName;

    public HttpClientPageContentLoader(IHttpClientFactory httpClientFactory, ILogger logger)
    {
        _httpClientName = "PageContentLoaderHttpClient";
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }


    public virtual async Task<string> LoadPageContentAsync(Uri uri)
    {
        
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        var httpResponseMessage = await _httpClientFactory.CreateClient(_httpClientName).SendAsync(request);
        this._logger.LogInformation($"for request-url '{request.RequestUri}' status: '{httpResponseMessage.StatusCode}'");
        var readAsByteArrayAsync = await httpResponseMessage.Content.ReadAsByteArrayAsync();
        return await ReadFromResponseAsync(readAsByteArrayAsync);
    }

    protected static async Task<string> ReadFromResponseAsync(byte[]? bytes)
    {
        if (bytes == null)
        {
            return string.Empty;
        }

        int num;
        try
        {
            return await ReadFromGzipStreamAsync(new MemoryStream(bytes));
        }
        catch
        {
            num = 1;
        }

        if (num == 1)
        {
            return await ReadFromStandardStreamAsync(new MemoryStream(bytes));
        }

        return string.Empty;
    }

    private static async Task<string> ReadFromStandardStreamAsync(Stream stream)
    {
        string endAsync;
        using (var reader = new StreamReader(stream))
        {
            endAsync = await reader.ReadToEndAsync();
        }

        return endAsync;
    }

    private static async Task<string> ReadFromGzipStreamAsync(Stream stream)
    {
        string endAsync;
        using (var deflateStream = new GZipStream(stream, CompressionMode.Decompress))
        {
            using (var reader = new StreamReader(deflateStream))
            {
                endAsync = await reader.ReadToEndAsync();
            }
        }

        return endAsync;
    }
}