using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.PodcastServices.Abstractions;

namespace RedditPodcastPoster.PodcastServices;

public class RemoteClient(
#pragma warning disable CS9113 // Parameter is unread.
    ILogger<RemoteClient> logger
#pragma warning restore CS9113 // Parameter is unread.
) : IRemoteClient
{
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
        var responseObject = (T) new DataContractJsonSerializer(typeof(T)).ReadObject(memoryStream)!;
        return responseObject;
    }
}