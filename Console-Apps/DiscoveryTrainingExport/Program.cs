using CommandLine;
using DiscoveryTrainingExport;

return await Parser.Default.ParseArguments<DiscoveryTrainingExportRequest>(args)
    .MapResult(
        async request =>
        {
            if (request.AnalyzeOnly)
            {
                var analysisPath = request.AnalysisPath
                                   ?? request.OutputPath
                                   ?? (request.ExportPath == null
                                       ? null
                                       : Path.Combine(Path.GetFullPath(request.ExportPath), "analysis"));
                if (string.IsNullOrWhiteSpace(analysisPath))
                {
                    Console.Error.WriteLine("Provide --analysis-path or --export-path with --analyze-only.");
                    return 1;
                }

                new DiscoveryTrainingAnalyzeProcessor().Run(analysisPath);
                return 0;
            }

            if (string.IsNullOrWhiteSpace(request.ExportPath))
            {
                Console.Error.WriteLine("--export-path is required unless --analyze-only is set.");
                return 1;
            }

            var processor = new DiscoveryTrainingExportProcessor();
            await processor.Run(request);

            var outputPath = Path.GetFullPath(request.OutputPath ?? Path.Combine(request.ExportPath, "analysis"));
            new DiscoveryTrainingAnalyzeProcessor().Run(outputPath);
            return 0;
        },
        _ => Task.FromResult(1));
