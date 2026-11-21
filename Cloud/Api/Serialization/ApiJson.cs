using Api.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Api.Serialization;

/// <summary>
/// The JSON options the isolated worker uses for HTTP bodies (<c>WriteAsJsonAsync</c> and <c>[FromBody]</c>).
/// <c>AddFunctionsWorkerCore</c> registers the worker defaults; <c>AddUtcJsonDateTimes</c> adds the UTC converters.
/// </summary>
public static class ApiJson
{
    public static JsonSerializerOptions CreateHttpSerializerOptions()
    {
        var services = new ServiceCollection();
        services.AddFunctionsWorkerCore();
        services.AddUtcJsonDateTimes();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<JsonSerializerOptions>>().Value;
        return new JsonSerializerOptions(options);
    }
}
