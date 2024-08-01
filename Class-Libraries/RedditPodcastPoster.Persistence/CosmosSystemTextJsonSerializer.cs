using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core.Serialization;
using Microsoft.Azure.Cosmos;

namespace RedditPodcastPoster.Persistence;

/// <remarks>
// See: https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/Microsoft.Azure.Cosmos.Samples/Usage/SystemTextJson/CosmosSystemTextJsonSerializer.cs
/// </remarks>
public sealed class CosmosSystemTextJsonSerializer(JsonSerializerOptions jsonSerializerOptions) : CosmosLinqSerializer
{
    private readonly JsonObjectSerializer systemTextJsonSerializer = new(jsonSerializerOptions);

    public override T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (stream is {CanSeek: true, Length: 0})
            {
                return default;
            }

            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T) (object) stream;
            }

            return (T) systemTextJsonSerializer.Deserialize(stream, typeof(T), default);
        }
    }

    public override Stream ToStream<T>(T input)
    {
        var streamPayload = new MemoryStream();
        systemTextJsonSerializer.Serialize(streamPayload, input, input.GetType(), default);
        streamPayload.Position = 0;
        return streamPayload;
    }

    public override string SerializeMemberName(MemberInfo memberInfo)
    {
        var jsonExtensionDataAttribute = memberInfo.GetCustomAttribute<JsonExtensionDataAttribute>(true);
        if (jsonExtensionDataAttribute != null)
        {
            return null;
        }

        var jsonPropertyNameAttribute = memberInfo.GetCustomAttribute<JsonPropertyNameAttribute>(true);
        if (!string.IsNullOrEmpty(jsonPropertyNameAttribute?.Name))
        {
            return jsonPropertyNameAttribute.Name;
        }

        if (jsonSerializerOptions.PropertyNamingPolicy != null)
        {
            return jsonSerializerOptions.PropertyNamingPolicy.ConvertName(memberInfo.Name);
        }

        // Do any additional handling of JsonSerializerOptions here.

        return memberInfo.Name;
    }
}