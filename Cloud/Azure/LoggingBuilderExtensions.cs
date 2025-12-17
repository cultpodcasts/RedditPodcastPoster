using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;

namespace Azure;

public static class LoggingBuilderExtensions
{
    private const string Prefix = "LOGCONFIG: ";
    extension(ILoggingBuilder loggingBuilder)
    {
        public void ConsoleWriteConfig()
        {
            loggingBuilder.Services.Configure<LoggerFilterOptions>(options =>
            {
                var log = new StringBuilder($"{Prefix}Logging options: {options.Rules.Count} rules. ");
                var ctr = 0;
                foreach (var rule in options.Rules)
                {
                    ctr++;
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
                        $"({ctr}: '{rule.ProviderName}', '{rule.CategoryName}', '{rule.LogLevel}', '{string.Join(", ", filteredLogLevels)}') ";
                    log.Append(buffer);
                }
                Console.Out.WriteLine(log.ToString());
            });
        }

        public void RemoveDefaultApplicationInsightsWarningRule()
        {
            loggingBuilder.Services.Configure<LoggerFilterOptions>(options =>
            {
                var removed= options.Rules.RemoveRuleFirst(rule =>
                    rule.ProviderName == typeof(ApplicationInsightsLoggerProvider).FullName! &&
                    rule.CategoryName == null &&
                    rule is { Filter: null, LogLevel: LogLevel.Warning });
                if (removed)
                {
                    Console.Out.WriteLine($"{Prefix}Removed default-application-insights-warning-rule.");
                }
            });
        }

        public void RemoveInformationRules()
        {
            loggingBuilder.Services.Configure<LoggerFilterOptions>(options =>
            {
                var removed = options.Rules.RemoveRuleWhere(rule =>
                    rule.ProviderName == null &&
                    rule.CategoryName == null &&
                    rule is { Filter: null, LogLevel: LogLevel.Information });
                Console.Out.WriteLine($"{Prefix}Removed {removed} information-rules.");
            });
        }
    }

    extension(IList<LoggerFilterRule> rules)
    {
        private int RemoveRuleWhere(Func<LoggerFilterRule, bool> filter)
        {
            var matchingRules = rules.Where(filter).ToArray();
            var i = 0;
            foreach (var rule in matchingRules)
            {
                rules.Remove(rule);
                i++;
            }

            return i;
        }

        private bool RemoveRuleFirst(Func<LoggerFilterRule, bool> filter)
        {
            var matchingRule = rules.FirstOrDefault(filter);
            if (matchingRule != null)
            {
                rules.Remove(matchingRule);
                return true;
            }

            return false;
        }
    }
}