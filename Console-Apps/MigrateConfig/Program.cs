using CommandLine;
using MigrateConfig;

return await Parser.Default.ParseArguments<SecretsRequest, LaunchSettingsRequest>(args)
    .MapResult(
        (SecretsRequest request) => new SecretsProcessor().Process(request),
        (LaunchSettingsRequest request) => new LaunchSettingsProcessor().Process(request),
        _ => Task.FromResult(1));
