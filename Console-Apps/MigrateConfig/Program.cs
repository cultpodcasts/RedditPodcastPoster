using CommandLine;
using MigrateConfig;
using RedditPodcastPoster.Configuration;

return await Parser.Default.ParseArguments<SecretsRequest, LaunchSettingsRequest>(args)
    .MapResult(
        (SecretsRequest request) =>
        {
            if (request.Version)
            {
                VersionInfo.PrintVersion();
                return Task.FromResult(0);
            }

            return new SecretsProcessor().Process(request);
        },
        (LaunchSettingsRequest request) =>
        {
            if (request.Version)
            {
                VersionInfo.PrintVersion();
                return Task.FromResult(0);
            }

            return new LaunchSettingsProcessor().Process(request);
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
