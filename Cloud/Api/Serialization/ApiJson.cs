using System.Reflection;
using System.Text.Json;
using Api.Extensions;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.Serialization;

/// <summary>
/// The JSON options the isolated worker uses for HTTP bodies (<c>WriteAsJsonAsync</c> and <c>[FromBody]</c>).
/// The function app builds these with <c>FunctionsApplication.CreateBuilder</c>, which calls
/// <c>ConfigureFunctionsWorkerDefaults</c> (case-insensitive property names, <c>DefaultIgnoreCondition</c> left at
/// <c>Never</c>). <c>AddUtcJsonDateTimes</c> adds the UTC converters.
/// <c>WorkerOptionsSetup</c> assigns <c>WorkerOptions.Serializer</c> from <c>IOptions&lt;JsonSerializerOptions&gt;</c>.
/// This reads that serializer, so a later worker default cannot drift from the options registration alone.
/// </summary>
public static class ApiJson
{
    public static JsonSerializerOptions CreateHttpSerializerOptions()
    {
        var builder = FunctionsApplication.CreateBuilder(Array.Empty<string>());
        builder.Services.AddUtcJsonDateTimes();
        using var host = builder.Build();
        var serializer = host.Services.GetRequiredService<IOptions<WorkerOptions>>().Value.Serializer
            ?? throw new InvalidOperationException(
                "WorkerOptions.Serializer is not set. WorkerOptionsSetup assigns it from IOptions<JsonSerializerOptions>.");
        return new JsonSerializerOptions(ReadSerializerOptions(serializer));
    }

    /// <summary>
    /// <see cref="JsonObjectSerializer"/> keeps the options instance it was constructed with in a private field.
    /// There is no public getter. That field is the options object <c>WriteAsJsonAsync</c> uses.
    /// </summary>
    private static JsonSerializerOptions ReadSerializerOptions(ObjectSerializer serializer)
    {
        if (serializer is not JsonObjectSerializer)
        {
            throw new InvalidOperationException(
                $"WorkerOptions.Serializer is {serializer.GetType().FullName}, not {nameof(JsonObjectSerializer)}.");
        }

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = serializer.GetType();
        foreach (var name in new[] { "Options", "SerializerOptions" })
        {
            if (type.GetProperty(name, flags)?.GetValue(serializer) is JsonSerializerOptions fromProperty)
            {
                return fromProperty;
            }
        }

        if (type.GetField("_options", flags)?.GetValue(serializer) is JsonSerializerOptions fromField)
        {
            return fromField;
        }

        throw new InvalidOperationException(
            "JsonObjectSerializer no longer stores its JsonSerializerOptions where WorkerOptionsSetup puts them.");
    }
}
