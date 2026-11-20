using CommandLine;

namespace DiscoveryTrainingTrain;

public class DiscoveryTrainingTrainRequest
{
    [Option('c', "csv-path", Required = true, HelpText = "Path to discovery-results.csv")]
    public required string CsvPath { get; set; }

    [Option('o', "output-path", Required = true, HelpText = "Directory for model bundle output")]
    public required string OutputPath { get; set; }

    [Option('m', "onnx-model-directory", Required = false,
        HelpText = "Directory for MiniLM ONNX + vocab (default: <output-path>/onnx)")]
    public string? OnnxModelDirectory { get; set; }

    [Option("max-rows", Required = false, HelpText = "Limit rows for a quick training run")]
    public int? MaxRows { get; set; }

    [Option("target-recall", Required = false, Default = 0.97f,
        HelpText = "Share of accepted results the auto-hide threshold must keep visible (threshold chosen on the calibration slice)")]
    public float TargetRecall { get; set; }

    [Option("threshold", Required = false,
        HelpText = "Fixed auto-hide threshold instead of choosing one from --target-recall")]
    public float? FixedThreshold { get; set; }

    [Option("include-hidden-rejects", Required = false, Default = false,
        HelpText = "Train on rejected results that were auto-hidden (never shown to a curator). Off by default.")]
    public bool IncludeHiddenRejects { get; set; }

    [Option("skip-download", Required = false, Default = false,
        HelpText = "Skip downloading ONNX model if files already exist")]
    public bool SkipDownload { get; set; }
}
