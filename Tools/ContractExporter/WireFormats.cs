namespace ContractExporter;

/// <summary>Patterns for values the HTTP serializer actually writes. Shared by the emitters and the tests.</summary>
public static class WireFormats
{
    /// <summary>UTC ISO-8601 instant with a trailing Z and up to seven fractional digits, matching <c>UtcDateTimeConverter</c>.</summary>
    public const string IsoDateTime = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?Z$";

    /// <summary>ISO-8601 with a numeric offset, never Z. <c>LocalOffsetDateTimeOffsetConverter</c> (the schedule's <c>slotStartUk</c>).</summary>
    public const string OffsetDateTime = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?[+-]\d{2}:\d{2}$";

    /// <summary>System.Text.Json TimeSpan constant ("c") format, for example <c>01:30:00</c> or <c>1.00:00:00</c>.</summary>
    public const string TimeSpan = @"^-?(\d+\.)?\d{2}:\d{2}:\d{2}(\.\d{1,7})?$";

    public const string IsoDateTimeDescription = "ISO-8601 UTC instant ending in Z.";

    public const string OffsetDateTimeDescription =
        "ISO-8601 date-time with a numeric UTC offset (never Z).";

    public const string TimeSpanDescription =
        "TimeSpan constant format, for example 01:30:00 or 1.00:00:00.";
}
