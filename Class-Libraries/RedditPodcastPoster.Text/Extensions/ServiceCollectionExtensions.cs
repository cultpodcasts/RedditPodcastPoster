using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Text.Enrichers;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.Text.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Text sanitisation services. Safe to call more than once (e.g. from
        /// <c>AddSubjectServices</c> and an explicit host registration).
        /// Requires Persistence <c>AddTitleCasingRules()</c> (and <c>AddRepositories()</c> for the
        /// rules repository) so <see cref="ITextSanitiser"/> can resolve
        /// <c>IAsyncInstance&lt;ITitleCasingRulesProvider&gt;</c> for <c>SanitiseTitle</c>.
        /// </summary>
        public IServiceCollection AddTextSanitiser()
        {
            if (services.Any(d => d.ServiceType == typeof(ITextSanitiser)))
            {
                return services;
            }

            return services
                .AddSingleton<ITextSanitiser, TextSanitiser>()
                .AddSingleton<IHtmlSanitiser, HtmlSanitiser>()
                .AddSingleton<IHashTagEnricher, HashTagEnricher>();
        }
    }
}
