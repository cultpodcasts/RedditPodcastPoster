using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.PodcastServices;

public class RemoteClient : IRemoteClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RemoteClient> _logger;

    public RemoteClient(HttpClient httpClient, ILogger<RemoteClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<T> InvokeGet<T>(string apiCall)
    {
        var httpClient = new HttpClient();
        var response = await httpClient.GetStringAsync(apiCall);
        var responseObject = DeserializeObject<T>(response);
        return responseObject;
    }

    private T DeserializeObject<T>(string objString)
    {
        using var memoryStream = new MemoryStream(Encoding.Unicode.GetBytes(objString));
        var responseObject = (T)new DataContractJsonSerializer(typeof(T)).ReadObject(memoryStream)!;
        return responseObject;
    }
}