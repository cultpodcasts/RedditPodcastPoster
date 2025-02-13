﻿using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.AI.Configuration;
using RedditPodcastPoster.Configuration.Extensions;

namespace RedditPodcastPoster.AI.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAIServices(this IServiceCollection services)
    {
        services
            .BindConfiguration<TextAnalyticsSettings>("textanalytics")
            .BindConfiguration<PodcastSubjectAIModelSettings>("aipodcastsubjectcategorisation")
            .BindConfiguration<ClassificationSettings>("classification");

        return services
            //.AddSingleton<ITextAnalyticsClientFactory, TextAnalyticsClientFactory>()
            //.AddSingleton<IClassifyActionFactory, ClassifyActionFactory>()
            //.AddScoped(s => s.GetService<ITextAnalyticsClientFactory>()!.Create())
            //.AddScoped(s => s.GetService<IClassifyActionFactory>()!.Create())

            //.AddScoped<IEpisodeClassifier, EpisodeClassifier>()
            ;
    }
}