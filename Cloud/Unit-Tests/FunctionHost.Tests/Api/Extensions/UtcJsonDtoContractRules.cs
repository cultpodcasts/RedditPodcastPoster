using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Api;
using Api.Dtos;
using Api.Extensions;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Episodes.TestSupport.TimeZones;
using RedditPodcastPoster.Models.Serialization;
using Xunit;

namespace FunctionHost.Tests.Api.Extensions;

/// <summary>
/// Contract fixture for #1212: every top-level <see cref="DateTime"/> / <see cref="DateTimeOffset"/> property on a type
/// in <c>Api.Dtos</c> or <c>Api.Models</c> is written by the HTTP serializer as UTC ISO-8601 ending in <c>Z</c>, never
/// zone-less and never with an offset. A new property-level <c>[JsonConverter]</c> on such a property, or a type-level
/// converter on such a type, that writes an offset fails here. The only allowed exception is the discovery schedule's
/// UK-local <c>slotStartUk</c>.
/// <para>
/// Scope: nested domain types reachable from the DTOs (for example <c>RedditPodcastPoster.Models</c>) are not
/// instantiated; their date-times are covered only by the globally registered UTC converters, not by this scan.
/// </para>
/// <para>
/// <see cref="UtcJsonDtoContractEastOfUtcHostGuard"/> re-runs this class under
/// <see cref="EastOfUtcChildTestHost.EastOfUtcZone"/>, so the zone-less and Local-kind cases bite as well.
/// </para>
/// </summary>
public partial class UtcJsonDtoContractRules
{
    /// <summary>Properties that deliberately keep a local offset (see <c>LocalOffsetDateTimeOffsetConverter</c>).</summary>
    private static readonly HashSet<(Type, string)> LocalOffsetExceptions =
    [
        (typeof(DiscoveryScheduleResponse.NextRun), nameof(DiscoveryScheduleResponse.NextRun.SlotStartUk))
    ];

    private readonly DomainTestFixture _fixture = new();

    internal static IReadOnlyList<Type> ScanContractTypes() =>
        typeof(Ioc).Assembly.GetTypes()
            .Where(t => t.Namespace is "Api.Dtos" or "Api.Models")
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .Where(t => !t.IsDefined(typeof(CompilerGeneratedAttribute), false))
            .Where(t => DateProperties(t).Any())
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

    public static TheoryData<Type> ContractTypes() => new(ScanContractTypes());

    private static IEnumerable<PropertyInfo> DateProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0)
            .Where(p => IsDateType(p.PropertyType))
            .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always });

    private static bool IsDateType(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) is var t && (t == typeof(DateTime) || t == typeof(DateTimeOffset));

    private static string Serialize(object value)
    {
        var services = new ServiceCollection();
        services.AddFunctionsWorkerCore();
        services.AddUtcJsonDateTimes();
        using var provider = services.BuildServiceProvider();
        var serializer = provider.GetRequiredService<IOptions<WorkerOptions>>().Value.Serializer!;
        return serializer.Serialize(value, value.GetType(), CancellationToken.None)!.ToString();
    }

    private static IEnumerable<string> DateStrings(JsonElement element, IReadOnlySet<string> skipped) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .Where(p => !skipped.Contains(p.Name))
                .SelectMany(p => DateStrings(p.Value, skipped)),
            JsonValueKind.Array => element.EnumerateArray().SelectMany(e => DateStrings(e, skipped)),
            JsonValueKind.String when DateTimePattern().IsMatch(element.GetString()!) => [element.GetString()!],
            _ => []
        };

    [Fact(DisplayName =
        "When the Api's DTOs and models are scanned for date-time properties, then some are found, because an empty scan would let the UTC Z contract pass without checking anything.")]
    public void contract_scan_finds_date_time_properties()
    {
        // Arrange
        var assembly = typeof(Ioc).Assembly;

        // Act
        var count = ScanContractTypes().Count(t => t.Assembly == assembly);

        // Assert
        count.Should().BeGreaterThan(5);
    }

    [Theory(DisplayName =
        "When an Api DTO or model holding zone-less, local and offset date-times is written by the HTTP serializer, then every date-time is ISO-8601 UTC ending in Z, because the API always emits UTC and only the UK-local discovery slot may keep an offset.")]
    [MemberData(nameof(ContractTypes))]
    public void every_api_date_time_is_written_as_utc_z(Type type)
    {
        // Arrange
        var instant = DomainTestFixture.UtcAtTime(-(_fixture.Create<int>() % 300 + 1), _fixture.CreateNonMidnightTimeOfDay());
        var instance = RuntimeHelpers.GetUninitializedObject(type);
        var properties = DateProperties(type).Where(p => !LocalOffsetExceptions.Contains((type, p.Name))).ToList();
        var index = 0;
        foreach (var property in properties)
        {
            var kind = index++ % 3;
            // Typed as object per branch: a shared DateTime/DateTimeOffset type would convert implicitly (#1194).
            var value = (Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType) == typeof(DateTime)
                ? kind == 0 ? DateTime.SpecifyKind(instant, DateTimeKind.Unspecified) : (object)instant.ToLocalTime()
                : UtcDateTimes.ToUtcOffset(instant).ToOffset(TimeSpan.FromHours(kind + 1));
            property.SetValue(instance, value);
        }

        // Act
        var json = Serialize(instance);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var skipped = LocalOffsetExceptions.Where(e => e.Item1 == type).Select(e => e.Item2)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var written = DateStrings(doc.RootElement, skipped).ToList();
        written.Should().HaveCountGreaterThanOrEqualTo(properties.Count,
            "every date-time property on {0} should be written:\n{1}", type.FullName, json);
        written.Should().AllSatisfy(text => text.Should().Be($"{instant:yyyy-MM-ddTHH:mm:ss}Z",
            "{0} should write UTC with a Z:\n{1}", type.FullName, json));
    }

    [UtcJsonDtoContractGuardChildFact(DisplayName =
        "When this class runs in the guard's child test host, then the host really is on Pacific/Auckland time, because a missing tzdata would silently fall back to UTC and the zone-less and local cases would pass without proving anything.")]
    public void child_host_is_east_of_utc()
    {
        // Arrange
        var local = TimeZoneInfo.Local;

        // Act
        var standardOffset = local.BaseUtcOffset;

        // Assert
        standardOffset.Should().Be(TimeSpan.FromHours(12),
            "TZ={0} should give NZST; local zone was '{1}'", EastOfUtcChildTestHost.EastOfUtcZone, local.Id);
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}")]
    private static partial Regex DateTimePattern();
}
