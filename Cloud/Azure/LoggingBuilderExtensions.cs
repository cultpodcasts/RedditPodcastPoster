using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.ApplicationInsights;

namespace Azure;

public static class LoggingBuilderExtensions
{
    extension(ILoggingBuilder loggingBuilder)
    {
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