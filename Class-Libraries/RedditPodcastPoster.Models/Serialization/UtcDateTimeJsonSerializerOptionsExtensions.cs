using System.Text.Json;
using System.Text.Json.Serialization;

namespace RedditPodcastPoster.Models.Serialization;

/// <summary>Registers the UTC date-time converters on a set of serializer options.</summary>
public static class UtcDateTimeJsonSerializerOptionsExtensions
{
    /// <summary>
    /// Adds the UTC <see cref="DateTime"/> / <see cref="DateTimeOffset"/> converters unless they are already present.
    /// System.Text.Json uses them for the nullable types as well. Property-level <c>[JsonConverter]</c> attributes
    /// still win.
    /// </summary>
    public static JsonSerializerOptions AddUtcDateTimeConverters(this JsonSerializerOptions options)
    {
        AddIfMissing<UtcDateTimeConverter>(options);
        AddIfMissing<UtcDateTimeOffsetConverter>(options);
        return options;
    }

    private static void AddIfMissing<T>(JsonSerializerOptions options) where T : JsonConverter, new()
    {
        if (!options.Converters.Any(c => c is T))
        {
            options.Converters.Add(new T());
        }
    }
}
