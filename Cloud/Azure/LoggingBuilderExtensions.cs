using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;

namespace Azure;

public static class LoggingBuilderExtensions
{
    private const string ProviderName =
        "Microsoft.Extensions.Logging.ApplicationInsights.ApplicationInsightsLoggerProvider";

    public static void RemoveApplicationInsightsBaselineWarningRule(this ILoggingBuilder loggingBuilder)
    {
        loggingBuilder.Services.Configure<LoggerFilterOptions>(options =>
        {
            var defaultRule = options.Rules.FirstOrDefault(rule =>
                rule.ProviderName == typeof(ApplicationInsightsLoggerProvider).FullName &&
                rule.CategoryName == null &&
                rule is {LogLevel: LogLevel.Warning, Filter: null});
            if (defaultRule is not null)
            {
                options.Rules.Remove(defaultRule);
            }
        });
    }
}