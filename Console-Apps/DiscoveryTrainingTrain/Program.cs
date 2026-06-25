using CommandLine;
using DiscoveryTrainingTrain;

return await Parser.Default.ParseArguments<DiscoveryTrainingTrainRequest>(args)
    .MapResult(
        async request =>
        {
            var processor = new DiscoveryTrainingTrainProcessor();
            await processor.RunAsync(request);
            return 0;
        },
        _ => Task.FromResult(1));
