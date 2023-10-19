using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using RedditPodcastPoster.PodcastServices.Apple;
using RedditPodcastPoster.PodcastServices.Spotify;
using RedditPodcastPoster.PodcastServices.YouTube;
using RedditPodcastPoster.UrlSubmission.Categorisation;

namespace RedditPodcastPoster.UrlSubmission.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddUrlSubmission(this IServiceCollection services)
        {
            return services
                .AddScoped<IUrlCategoriser, UrlCategoriser>()
                .AddScoped<IAppleUrlCategoriser, AppleUrlCategoriser>()
                .AddScoped<ISpotifyUrlCategoriser, SpotifyUrlCategoriser>()
                .AddScoped<IYouTubeUrlCategoriser, YouTubeUrlCategoriser>()
                .AddScoped<IUrlSubmitter, UrlSubmitter>();
        }
    }
}
