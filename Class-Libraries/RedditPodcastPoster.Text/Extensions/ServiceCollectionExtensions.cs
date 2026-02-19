using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.DependencyInjection;
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
                .AddSingleton<IAsyncInstance<IKnownTermsProvider>>(s =>
                    new AsyncInstance<IKnownTermsProvider>(s.GetService<IKnownTermsProviderFactory>()!))
                .AddSingleton<IKnownTermsRepository, KnownTermsRepository>()
                .AddSingleton<IHashTagEnricher, HashTagEnricher>();
        }

        public IServiceCollection AddEliminationTerms()
        {
            return services
                .AddSingleton<IEliminationTermsProviderFactory, EliminationTermsProviderFactory>()
                .AddSingleton<IAsyncInstance<IEliminationTermsProvider>>(s =>
                    new AsyncInstance<IEliminationTermsProvider>(s.GetService<IEliminationTermsProviderFactory>()!));
        }
    }
}