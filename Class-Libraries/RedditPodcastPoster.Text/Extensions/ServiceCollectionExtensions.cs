using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.Text.EliminationTerms;
using RedditPodcastPoster.Text.KnownTerms;

namespace RedditPodcastPoster.Text.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTextSanitiser(this IServiceCollection services)
    {
        return services
            .AddSingleton<ITextSanitiser, TextSanitiser>()
            .AddSingleton<IHtmlSanitiser, HtmlSanitiser>()
            .AddSingleton<IKnownTermsProviderFactory, KnownTermsProviderFactory>()
            .AddSingleton<IKnownTermsRepository, KnownTermsRepository>()
            .AddSingleton<IHashTagEnricher, HashTagEnricher>()
            .AddSingleton(s => s.GetService<IKnownTermsProviderFactory>()!.Create().GetAwaiter().GetResult());
    }

    public static IServiceCollection AddEliminationTerms(this IServiceCollection services)
    {
        return services
            .AddScoped<IEliminationTermsProviderFactory, EliminationTermsProviderFactory>()
            .AddSingleton(s => s.GetService<IEliminationTermsProviderFactory>()!.Create().GetAwaiter().GetResult());
    }
}