using CommandLine;
using DiscoveryTrainingTrain;
using RedditPodcastPoster.Configuration;

return await Parser.Default.ParseArguments<DiscoveryTrainingTrainRequest>(args)
    .MapResult(
        async request =>
        {
            if (request.Version)
            {
                VersionInfo.PrintVersion();
                return 0;
            }

            var processor = new DiscoveryTrainingTrainProcessor();
            await processor.RunAsync(request);
            return 0;
        },
        errs =>
        {
            if (errs.Any(x => x is VersionRequestedError))
            {
                VersionInfo.PrintVersion();
                return Task.FromResult(0);
            }

            return Task.FromResult(1);
        });
