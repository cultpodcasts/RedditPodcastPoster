using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;

namespace Azure;

public static class LoggingBuilderExtensions
{
    public static void RemoveDefaultApplicationInsightsWarningRule(this ILoggingBuilder loggingBuilder)
    {
        const string? categoryName = null;
        const Func<string?, string?, LogLevel, bool>? filter = null;
        var providerName = typeof(ApplicationInsightsLoggerProvider).FullName!;
        LogLevel? warning = LogLevel.Warning;

        loggingBuilder.Services.Configure<LoggerFilterOptions>(options =>
        {
            var defaultRule = options.Rules.FirstOrDefault(rule =>
                rule.ProviderName == providerName &&
                rule.CategoryName == categoryName &&
                rule is {Filter: filter} &&
                rule.LogLevel == warning);
            if (defaultRule is not null)
            {
                options.Rules.Remove(defaultRule);
            }
        });
    }
}