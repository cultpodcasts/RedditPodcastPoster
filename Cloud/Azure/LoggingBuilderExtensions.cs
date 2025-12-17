using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;

namespace Azure;

public static class LoggingBuilderExtensions
{
    extension(ILoggingBuilder loggingBuilder)
    {
        public void ConsoleWriteConfig()
        {
            loggingBuilder.Services.Configure<LoggerFilterOptions>(options =>
            {
                Console.Out.WriteLine("Logging options:");
                foreach (var rule in options.Rules)
                {
                    var logLevel = new[]
                    {
                        LogLevel.Trace, LogLevel.Debug, LogLevel.Information, LogLevel.Warning, LogLevel.Error,
                        LogLevel.Critical, LogLevel.None
                    };
                    var filteredLogLevels = rule.Filter == null
                        ? []
                        : logLevel.Where(x => rule.Filter.Invoke(string.Empty, string.Empty, x))
                            .Select(x => x.ToString());

                    var buffer =
                        $"'{rule.ProviderName}', '{rule.CategoryName}', '{rule.LogLevel}', '{string.Join(", ", filteredLogLevels)}'";
                    Console.Out.WriteLine(buffer);
                }
            });
        }

        public void RemoveDefaultApplicationInsightsWarningRule()
        {
            loggingBuilder.Services.Configure<LoggerFilterOptions>(options =>
            {
                options.Rules.RemoveRuleFirst(rule =>
                    rule.ProviderName == typeof(ApplicationInsightsLoggerProvider).FullName! &&
                    rule.CategoryName == null &&
                    rule is { Filter: null, LogLevel: LogLevel.Warning });
            });
        }

        public void RemoveInformationRules()
        {
            loggingBuilder.Services.Configure<LoggerFilterOptions>(options =>
            {
                options.Rules.RemoveRuleWhere(rule =>
                    rule.ProviderName == null &&
                    rule.CategoryName == null &&
                    rule is { Filter: null, LogLevel: LogLevel.Information });
            });
        }
    }

    extension(IList<LoggerFilterRule> rules)
    {
        private void RemoveRuleWhere(Func<LoggerFilterRule, bool> filter)
        {
            var matchingRules = rules.Where(filter).ToArray();
            foreach (var rule in matchingRules)
            {
                rules.Remove(rule);
            }
        }

        private void RemoveRuleFirst(Func<LoggerFilterRule, bool> filter)
        {
            var matchingRule = rules.FirstOrDefault(filter);
            if (matchingRule != null)
            {
                rules.Remove(matchingRule);
            }
        }
    }
}