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
        /// Text sanitisation services. Requires <c>AddRepositories()</c> (Persistence) for
        /// <see cref="KnownTerms.IKnownTermsProvider"/> resolution via Cosmos lookup repos.
        /// </summary>
        public IServiceCollection AddTextSanitiser()
        {
            return services
                .AddSingleton<ITextSanitiser, TextSanitiser>()
                .AddSingleton<IHtmlSanitiser, HtmlSanitiser>()
                .AddSingleton<IHashTagEnricher, HashTagEnricher>();
        }
    }
}
