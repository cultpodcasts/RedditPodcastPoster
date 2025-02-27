using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;

namespace Azure;

public static class LoggingBuilderExtensions
{
    public static void SetApplicationInsightsBaselineWarningRule(this ILoggingBuilder loggingBuilder, LogLevel logLevel)
    {
        if (logLevel != LogLevel.Warning)
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
                    var loggerFilterRule = new LoggerFilterRule(
                        typeof(ApplicationInsightsLoggerProvider).FullName,
                        null,
                        logLevel,
                        null);
                    options.Rules.Insert(0, loggerFilterRule);
                }
            });
        }
    }
}