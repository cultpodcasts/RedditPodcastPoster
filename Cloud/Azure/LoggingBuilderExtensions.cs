using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;

namespace Azure;

public static class LoggingBuilderExtensions
{
    public static void SetApplicationInsightsBaselineWarningRule(this ILoggingBuilder loggingBuilder, LogLevel logLevel)
    {
        const string? categoryName = null;
        const Func<string?, string?, LogLevel, bool>? filter = null;
        var providerName = typeof(ApplicationInsightsLoggerProvider).FullName!;

        if (logLevel != LogLevel.Warning)
        {
            loggingBuilder.Services.Configure<LoggerFilterOptions>(options =>
            {
                var defaultRule = options.Rules.FirstOrDefault(rule =>
                    rule.ProviderName == providerName &&
                    rule.CategoryName == categoryName &&
                    rule is
                    {
                        LogLevel: LogLevel.Warning,
                        Filter: filter
                    });
                if (defaultRule is not null)
                {
                    options.Rules.Remove(defaultRule);
                    var loggerFilterRule = new LoggerFilterRule(providerName, categoryName, logLevel, filter);
                    options.Rules.Insert(0, loggerFilterRule);
                }
            });
        }
    }
}