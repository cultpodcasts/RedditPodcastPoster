using CommandLine;
using DiscoveryTrainingTrain;
using RedditPodcastPoster.Configuration;

if (args.Contains("--version"))
{
    VersionInfo.PrintVersion();
    return 0;
}

return await Parser.Default.ParseArguments<DiscoveryTrainingTrainRequest>(args)
    .MapResult(
        async request =>
        {
            var processor = new DiscoveryTrainingTrainProcessor();
            await processor.RunAsync(request);
            return 0;
        },
        _ => Task.FromResult(1));
