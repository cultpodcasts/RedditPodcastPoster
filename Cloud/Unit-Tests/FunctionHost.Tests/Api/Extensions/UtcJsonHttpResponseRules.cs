using System.Text.Json;
using Api;
using Api.Dtos;
using Api.Dtos.Mapping;
using Api.Extensions;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Episodes.TestSupport.Fixtures;
using RedditPodcastPoster.Models.Discovery;
using RedditPodcastPoster.Models.Serialization;
using Xunit;

namespace FunctionHost.Tests.Api.Extensions;

/// <summary>
/// HTTP responses (<c>WriteAsJsonAsync</c>) and bodies (<c>[FromBody]</c>) go through
/// <see cref="WorkerOptions.Serializer"/>, which is built from <see cref="IOptions{JsonSerializerOptions}"/>.
/// </summary>
public class UtcJsonHttpResponseRules
{
    private readonly DomainTestFixture _fixture = new();

    private DateTime CreateUtcInstant() =>
        DomainTestFixture.UtcAtTime(-(_fixture.Create<int>() % 300 + 1),
            TimeSpan.FromMinutes(_fixture.Create<int>() % 1439 + 1));

    private static string Serialize(IServiceProvider provider, object value)
    {
        var serializer = provider.GetRequiredService<IOptions<WorkerOptions>>().Value.Serializer!;
        return serializer.Serialize(value, value.GetType(), CancellationToken.None)!.ToString();
    }

    [Fact(DisplayName =
        "When an HTTP response holds zone-less, local or offset date-times, then the worker serializer writes them as UTC with a Z, because the API always emits UTC.")]
    public void http_response_serializer_writes_utc_z()
    {
        // Arrange
        var value = CreateUtcInstant();
        var services = new ServiceCollection();
        services.AddFunctionsWorkerCore();
        services.AddUtcJsonDateTimes();
        using var provider = services.BuildServiceProvider();
        var body = new
        {
            unspecified = DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
            local = value.ToLocalTime(),
            offset = new DateTimeOffset(value).ToOffset(TimeSpan.FromHours(1)),
            missing = (DateTime?)null
        };
        var expected = $"{value:yyyy-MM-ddTHH:mm:ss}Z";

        // Act
        var json = Serialize(provider, body);

        // Assert
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("unspecified").GetString().Should().Be(expected);
        doc.RootElement.GetProperty("local").GetString().Should().Be(expected);
        doc.RootElement.GetProperty("offset").GetString().Should().Be(expected);
    }

    [Fact(DisplayName =
        "When the Api host is configured, then its JSON options carry the UTC date-time converters, because the HTTP serializer is built from those options.")]
    public void api_ioc_registers_utc_converters()
    {
        // Arrange
        var services = FunctionHostTestSupport.CreateServiceCollection(Ioc.ConfigureServices);
        using var provider = services.BuildServiceProvider();

        // Act
        var options = provider.GetRequiredService<IOptions<JsonSerializerOptions>>().Value;

        // Assert
        options.Converters.Should().ContainSingle(c => c is UtcDateTimeConverter);
        options.Converters.Should().ContainSingle(c => c is UtcDateTimeOffsetConverter);
    }

    [Theory(DisplayName =
        "When a discovery schedule response holds a slot with a UK offset, then the serializer writes slotStartUtc with a Z and slotStartUk with its own offset, because the slotStartUk property opts out of the UTC converters.")]
    [InlineData(1)]
    [InlineData(0)]
    public void discovery_schedule_keeps_uk_local_offset(int ukOffsetHours)
    {
        // Arrange
        var value = CreateUtcInstant();
        var ukOffset = TimeSpan.FromHours(ukOffsetHours);
        var slotUk = new DateTimeOffset(value).ToOffset(ukOffset);
        var services = new ServiceCollection();
        services.AddFunctionsWorkerCore();
        services.AddUtcJsonDateTimes();
        using var provider = services.BuildServiceProvider();
        var response = new DiscoveryScheduleResponse
        {
            RunTimes = [_fixture.Create<string>()],
            TimeZoneId = _fixture.Create<string>(),
            Enabled = true,
            IsDefault = false,
            NextRuns =
            [
                new DiscoveryScheduleResponse.NextRun
                {
                    SlotId = _fixture.Create<string>(),
                    SlotStartUtc = slotUk.ToUniversalTime(),
                    SlotStartUk = slotUk
                }
            ]
        };
        var expectedOffset = ukOffsetHours == 0 ? "+00:00" : "+01:00";

        // Act
        var json = Serialize(provider, response);

        // Assert
        using var doc = JsonDocument.Parse(json);
        var run = doc.RootElement.GetProperty("nextRuns")[0];
        run.GetProperty("slotStartUtc").GetString().Should().Be($"{value:yyyy-MM-ddTHH:mm:ss}Z");
        run.GetProperty("slotStartUk").GetString().Should().Be($"{slotUk:yyyy-MM-ddTHH:mm:ss}{expectedOffset}");
    }
    /// <summary>The last Sunday of <paramref name="month"/> next year: the UK clock-change day (October or March).</summary>
    private static DateTime LastSundayNextYear(int month)
    {
        var year = DomainTestFixture.UtcToday.Year + 1;
        var last = new DateTime(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Utc);
        return last.AddDays(-(int)last.DayOfWeek);
    }

    private static JsonElement BuildAndSerializeNextRuns(string runTime, DateTime utcNow)
    {
        var services = new ServiceCollection();
        services.AddFunctionsWorkerCore();
        services.AddUtcJsonDateTimes();
        using var provider = services.BuildServiceProvider();
        var config = new DiscoveryScheduleConfig
        {
            RunTimes = [runTime],
            TimeZoneId = "Europe/London",
            Enabled = true
        };
        var response = DiscoveryScheduleResponseBuilder.Build(config, isDefault: false, nextRunsPreviewCount: 2, utcNow);
        using var doc = JsonDocument.Parse(Serialize(provider, response));
        return doc.RootElement.GetProperty("nextRuns").Clone();
    }

    [Theory(DisplayName =
        "When the discovery schedule is built across a UK clock change, then slotStartUk keeps the 09:00 run time with the new UK offset and slotStartUtc moves by an hour, because curators set run times in UK local time, which moves against UTC when the clocks change.")]
    [InlineData(10, "+01:00", "08:00:00Z", "+00:00", "09:00:00Z")]
    [InlineData(3, "+00:00", "09:00:00Z", "+01:00", "08:00:00Z")]
    public void discovery_schedule_keeps_local_run_time_across_clock_change(
        int month,
        string beforeOffset,
        string beforeUtc,
        string afterOffset,
        string afterUtc)
    {
        // Arrange
        var changeDay = LastSundayNextYear(month);
        var dayBefore = changeDay.AddDays(-1);
        var utcNow = dayBefore;

        // Act
        var nextRuns = BuildAndSerializeNextRuns("09:00", utcNow);

        // Assert
        nextRuns.GetArrayLength().Should().Be(2);
        nextRuns[0].GetProperty("slotStartUk").GetString().Should().Be($"{dayBefore:yyyy-MM-dd}T09:00:00{beforeOffset}");
        nextRuns[0].GetProperty("slotStartUtc").GetString().Should().Be($"{dayBefore:yyyy-MM-dd}T{beforeUtc}");
        nextRuns[1].GetProperty("slotStartUk").GetString().Should().Be($"{changeDay:yyyy-MM-dd}T09:00:00{afterOffset}");
        nextRuns[1].GetProperty("slotStartUtc").GetString().Should().Be($"{changeDay:yyyy-MM-dd}T{afterUtc}");
    }

    [Fact(DisplayName =
        "When a discovery run time falls in the repeated hour on the October clock-change day, then the slot is the second occurrence (GMT, +00:00), because slot identity must be stable and match how UK wall-clock times are converted elsewhere.")]
    public void discovery_schedule_ambiguous_october_hour_is_second_occurrence()
    {
        // Arrange
        var changeDay = LastSundayNextYear(10);
        var dayBefore = changeDay.AddDays(-1);

        // Act
        var nextRuns = BuildAndSerializeNextRuns("01:30", dayBefore);

        // Assert
        nextRuns[0].GetProperty("slotStartUk").GetString().Should().Be($"{dayBefore:yyyy-MM-dd}T01:30:00+01:00");
        nextRuns[0].GetProperty("slotStartUtc").GetString().Should().Be($"{dayBefore:yyyy-MM-dd}T00:30:00Z");
        nextRuns[1].GetProperty("slotStartUk").GetString().Should().Be($"{changeDay:yyyy-MM-dd}T01:30:00+00:00");
        nextRuns[1].GetProperty("slotStartUtc").GetString().Should().Be($"{changeDay:yyyy-MM-dd}T01:30:00Z");
    }
}
