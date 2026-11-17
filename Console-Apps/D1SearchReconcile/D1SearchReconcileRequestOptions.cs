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
}
