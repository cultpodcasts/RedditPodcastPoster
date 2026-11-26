using CommandLine;

namespace D1SearchReconcile;

public class D1SearchReconcileRequestOptions
{
    [Option("write", Required = false, Default = false,
        HelpText = "Patch the index api:Endpoint points at. Without it this is a dry run: it reads D1 and Cosmos and reports, and writes nothing.")]
    public bool Write { get; set; }

    [Option("allow-drift", Required = false, Default = false,
        HelpText = "With --write: patch even above the drift threshold in this console's appsettings.json (d1Search:Reconcile:DriftThreshold, default 5%). For the initial backfill (D1 empty) or a deliberate repair.")]
    public bool AllowDrift { get; set; }

    [Option("full", Required = false, Default = false,
        HelpText = "Compare the whole catalogue against D1 instead of only items (and episodes of podcasts) with _ts at or after the last successful write watermark. Use for a weekly safety check or a deliberate rebuild. Incremental is the default once a watermark has been saved.")]
    public bool Full { get; set; }

    [Option("rebuild", Required = false, Default = false,
        HelpText = "With --write: rebuild the whole index from Cosmos into new tables, swap them in, check them (integrity, sample searches, a full reconcile) and drop the old index; any failed check swaps the old index back (SP-2.3, #226). For drift or corruption only. Rerun it to resume or clean up after an interrupted run.")]
    public bool Rebuild { get; set; }
}
