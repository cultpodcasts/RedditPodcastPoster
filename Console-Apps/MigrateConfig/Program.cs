using CommandLine;
using MigrateConfig;
using RedditPodcastPoster.Configuration;

if (args.Contains("--version"))
{
    VersionInfo.PrintVersion();
    return 0;
}

return await Parser.Default.ParseArguments<SecretsRequest, LaunchSettingsRequest>(args)
    .MapResult(
        (SecretsRequest request) => new SecretsProcessor().Process(request),
        (LaunchSettingsRequest request) => new LaunchSettingsProcessor().Process(request),
        _ => Task.FromResult(1));
