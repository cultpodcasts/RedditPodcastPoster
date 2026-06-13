using CommandLine;

namespace DiscoveryTrainingExport;

public class DiscoveryTrainingExportRequest
{
    [Option('e', "export-path", Required = false, HelpText = "Root folder of a CosmosDbDownloaderV2 export.")]
    public string? ExportPath { get; set; }

    [Option('o', "output-path", Required = false,
        HelpText = "Folder for CSV output (default: <export-path>/analysis).")]
    public string? OutputPath { get; set; }

    [Option('a', "analyze-only", Required = false, Default = false,
        HelpText = "Skip export; analyze existing CSVs in output-path or <export-path>/analysis.")]
    public bool AnalyzeOnly { get; set; }

    [Option("analysis-path", Required = false,
        HelpText = "Folder containing discovery-results.csv (overrides output-path for analyze-only).")]
    public string? AnalysisPath { get; set; }
}
