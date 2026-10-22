namespace CatalogueMigrate;

/// <summary>
/// When CatalogueMigrate may move an auto-indexed podcast. The Indexer's <c>Hourly</c> timer starts at :03 and
/// <c>HourlyCatchUp</c> at :08 (Cloud/Indexer/Triggers/OrchestrationTrigger.cs). A pass reads each podcast, indexes it
/// and then saves it, so a move that deletes the podcast while a pass holds it would be undone when the pass saves the
/// podcast again. Minutes :15 to :54 are clear of both starts with margin either side. Minutes are the same in UTC and
/// London time.
/// <para>
/// A pass that overruns past :15 is still possible. <see cref="CatalogueMigrateApplier"/> re-reads the source just
/// before it deletes it, and stops (source kept, re-run resumes) if the indexer touched it during the move.
/// </para>
/// </summary>
public static class CatalogueMigrateIndexerWindow
{
    public const int SafeFromMinute = 15;
    public const int SafeUntilMinute = 55;

    public static bool IndexerMayBeRunning(DateTimeOffset now)
    {
        var minute = now.ToUniversalTime().Minute;
        return minute is < SafeFromMinute or >= SafeUntilMinute;
    }
}
