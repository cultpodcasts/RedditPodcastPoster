using CommandLine;
using MigrateConfig;
using RedditPodcastPoster.Configuration;

if (VersionInfo.TryHandleVersionRequest(args))
{
    return 0;
}

return await Parser.Default.ParseArguments<SecretsRequest, LaunchSettingsRequest>(args)
    .MapResult(
        (SecretsRequest request) =>
        {
            return new SecretsProcessor().Process(request);
        },
        (LaunchSettingsRequest request) =>
        {
            return new LaunchSettingsProcessor().Process(request);
        },
        errs => Task.FromResult(1));
