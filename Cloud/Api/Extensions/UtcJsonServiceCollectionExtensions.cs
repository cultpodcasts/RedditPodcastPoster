using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Models.Serialization;

namespace Api.Extensions;

public static class UtcJsonServiceCollectionExtensions
{
    /// <summary>
    /// The API always emits date-times in UTC: every <see cref="DateTime"/> / <see cref="DateTimeOffset"/> in an HTTP
    /// response is written as ISO-8601 with a trailing <c>Z</c>, never with an offset or as a zone-less local time.
    /// Request bodies are read the same way (no zone = UTC, an offset is adjusted to UTC).
    /// The isolated worker builds <c>WorkerOptions.Serializer</c> (used by <c>WriteAsJsonAsync</c> and
    /// <c>[FromBody]</c>) from <c>IOptions&lt;JsonSerializerOptions&gt;</c>, so the converters are added there and the
    /// worker defaults (<c>PropertyNameCaseInsensitive</c>) are kept.
    /// </summary>
    public static IServiceCollection AddUtcJsonDateTimes(this IServiceCollection services)
    {
        services.Configure<JsonSerializerOptions>(options => options.AddUtcDateTimeConverters());
        return services;
    }
}
