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

    [Option('s', "show-rates-path", Required = false,
        HelpText = "Optional show-accept-rates.csv for show-level feature")]
    public string? ShowAcceptRatesPath { get; set; }

    [Option("max-rows", Required = false, HelpText = "Limit rows for a quick training run")]
    public int? MaxRows { get; set; }

    [Option("threshold", Required = false, Default = 0.05f,
        HelpText = "Auto-hide probability threshold recorded in manifest")]
    public float AutoHideThreshold { get; set; }

    [Option("skip-download", Required = false, Default = false,
        HelpText = "Skip downloading ONNX model if files already exist")]
    public bool SkipDownload { get; set; }
}
