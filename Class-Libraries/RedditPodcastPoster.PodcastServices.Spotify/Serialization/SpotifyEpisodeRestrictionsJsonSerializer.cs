using System.Net.Http;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SpotifyAPI.Web.Http;

namespace RedditPodcastPoster.PodcastServices.Spotify.Serialization;

/// <summary>
/// Mirrors SpotifyAPI.Web's Newtonsoft serializer, plus a converter so episode
/// <c>restrictions.reason</c> is retained for non-playable skip logging.
/// </summary>
public sealed class SpotifyEpisodeRestrictionsJsonSerializer : IJSONSerializer
{
    private readonly JsonSerializerSettings _serializerSettings;

    public SpotifyEpisodeRestrictionsJsonSerializer()
    {
        var contractResolver = new PrivateFieldDefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy()
        };

        _serializerSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = contractResolver,
            Converters = { new SpotifyEpisodeRestrictionsConverter() }
        };
    }

    public IAPIResponse<T> DeserializeResponse<T>(IResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.ContentType?.Equals("application/json", StringComparison.OrdinalIgnoreCase) is true)
        {
            var body = JsonConvert.DeserializeObject<T>(response.Body as string ?? "", _serializerSettings);
            return new APIResponse<T>(response, body!);
        }

        return new APIResponse<T>(response);
    }

    public void SerializeRequest(IRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Body is string or Stream or HttpContent or null)
        {
            return;
        }

        request.Body = JsonConvert.SerializeObject(request.Body, _serializerSettings);
    }

    private sealed class PrivateFieldDefaultContractResolver : DefaultContractResolver
    {
        protected override List<MemberInfo> GetSerializableMembers(Type objectType)
        {
            var list = base.GetSerializableMembers(objectType);
            list.AddRange(objectType.GetProperties(BindingFlags.NonPublic | BindingFlags.Instance));
            return list;
        }
    }
}
