using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Text.EliminationTerms;
using RedditPodcastPoster.Text.Enrichers;
using RedditPodcastPoster.Text.Extensions;
using RedditPodcastPoster.Text.KnownTerms;
using RedditPodcastPoster.Text.Matchers;
using RedditPodcastPoster.Text.Models;
using RedditPodcastPoster.Text.Sanitisers;

namespace RedditPodcastPoster.Text.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Text sanitisation services. Safe to call more than once (e.g. from
        /// <c>AddSubjectServices</c> and an explicit host registration).
        /// Requires <c>AddRepositories()</c> (Persistence) for
        /// <see cref="TitleCasing.ITitleCasingRulesProvider"/> via <c>IAsyncInstance&lt;ITitleCasingRulesProvider&gt;</c>
        /// when resolving <see cref="ITextSanitiser"/>.
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
