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
            var defaultRule = options.Rules.FirstOrDefault(rule => rule.ProviderName == ProviderName);
            if (defaultRule is not null)
            {
                options.Rules.Remove(defaultRule);
            }
        });
    }
}