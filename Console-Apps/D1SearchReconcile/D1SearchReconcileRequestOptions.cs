using CommandLine;

namespace D1SearchReconcile;

public class D1SearchReconcileRequestOptions
{
    [Option('t', "target", Required = false, Separator = ',',
        HelpText = "Targets to reconcile (production, preview), comma-separated. Default: every enabled, configured target.")]
    public IEnumerable<string> Targets { get; set; } = [];

    [Option("write", Required = false, Default = false,
        HelpText = "Write to the --target targets (required with --write). Without it this is a dry run: it reads D1 and Cosmos and reports, and writes nothing.")]
    public bool Write { get; set; }

    [Option("allow-drift", Required = false, Default = false,
        HelpText = "With --write: patch even above the drift threshold. For the initial backfill (D1 empty) or a deliberate repair.")]
    public bool AllowDrift { get; set; }
}
