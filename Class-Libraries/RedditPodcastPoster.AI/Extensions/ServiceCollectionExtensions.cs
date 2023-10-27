using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.AI.Configuration;
using RedditPodcastPoster.AI.Factories;

namespace RedditPodcastPoster.AI.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAIServices(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<TextAnalyticsSettings>().Bind(config.GetSection("textanalytics"));

        services
            .AddOptions<PodcastSubjectAIModelSettings>().Bind(config.GetSection("aipodcastsubjectcategorisation"));

        services
            .AddOptions<ClassificationSettings>().Bind(config.GetSection("classification"));

        return services
            //.AddSingleton<ITextAnalyticsClientFactory, TextAnalyticsClientFactory>()
            //.AddSingleton<IClassifyActionFactory, ClassifyActionFactory>()
            //.AddScoped(s => s.GetService<ITextAnalyticsClientFactory>()!.Create())
            //.AddScoped(s => s.GetService<IClassifyActionFactory>()!.Create())
            .AddScoped<ICategoriser, Categoriser>()
            //.AddScoped<IEpisodeClassifier, EpisodeClassifier>()
            .AddScoped<ISubjectMatcher, SubjectMatcher>();
    }
}