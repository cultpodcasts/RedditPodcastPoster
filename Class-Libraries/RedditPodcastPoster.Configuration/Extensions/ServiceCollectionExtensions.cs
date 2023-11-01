using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RedditPodcastPoster.Configuration.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPostingCriteria(this IServiceCollection services, IConfiguration config)
    {
        services
            .AddOptions<PostingCriteria>().Bind(config.GetSection("postingCriteria"));
        return services;
    }
}