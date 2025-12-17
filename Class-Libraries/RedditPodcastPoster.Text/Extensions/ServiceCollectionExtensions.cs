using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Text.EliminationTerms;
using RedditPodcastPoster.Text.KnownTerms;

namespace RedditPodcastPoster.Text.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddTextSanitiser()
        {
            return services
                .AddSingleton<ITextSanitiser, TextSanitiser>()
                .AddSingleton<IHtmlSanitiser, HtmlSanitiser>()
                .AddSingleton<IKnownTermsProviderFactory, KnownTermsProviderFactory>()
                .AddSingleton<IKnownTermsRepository, KnownTermsRepository>()
                .AddSingleton<IHashTagEnricher, HashTagEnricher>()
                .AddSingleton(s => s.GetService<IKnownTermsProviderFactory>()!.Create().GetAwaiter().GetResult());
        }

        public IServiceCollection AddEliminationTerms()
        {
            return services
                .AddSingleton<IEliminationTermsProviderFactory, EliminationTermsProviderFactory>()
                .AddSingleton(s => s.GetService<IEliminationTermsProviderFactory>()!.Create().GetAwaiter().GetResult());
        }
    }
}