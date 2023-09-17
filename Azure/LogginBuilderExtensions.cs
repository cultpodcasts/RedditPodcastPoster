using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Azure;

public static class LoggingBuilderExtensions
{
    private const string ProviderName =
        "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider";

    public static void AllowAzureFunctionApplicationInsightsTraceLogging(this ILoggingBuilder loggingBuilder)
    {
        loggingBuilder.Services.Configure<LoggerFilterOptions>(options =>
        {
            var defaultRule =
                options.Rules.FirstOrDefault(
                    rule => rule.ProviderName ==
                            ProviderName);
            if (defaultRule is not null)
            {
                options.Rules.Remove(defaultRule);
            }
        });
    }
}

public static class ConfigurationBuilderExtensions
{
    public static IConfiguration AddToConfigurationBuilder<T>(this IConfigurationBuilder configurationBuilder) where T : class
    {
        return
            configurationBuilder
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddUserSecrets<T>()
                .AddEnvironmentVariables()
                .Build();
    }
}