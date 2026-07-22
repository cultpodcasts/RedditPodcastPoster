using Microsoft.Extensions.DependencyInjection;
using Api.Configuration;
using Api.Factories;
using Api.Handlers;
using Api.Handlers.Discovery;
using Api.Handlers.DiscoverySchedule;
using Api.Handlers.Episodes;
using Api.Handlers.Homepage;
using Api.Handlers.People;
using Api.Handlers.Podcasts;
using Api.Handlers.Public;
using Api.Handlers.PushSubscriptions;
using Api.Handlers.SearchIndex;
using Api.Handlers.Subjects;
using Api.Handlers.SubmitUrl;
using Api.Handlers.Terms;
using Api.Resolvers;
using Api.Services;
using Api.Services.Discovery;
using Api.Services.DiscoverySchedule;
using Api.Services.Episodes;
using Api.Services.People;
using Api.Services.Podcasts;
using Api.Services.Public;
using Api.Services.Homepage;
using Api.Services.PushSubscriptions;
using Api.Services.SearchIndex;
using Api.Services.Subjects;
using Api.Services.SubmitUrl;
using Api.Services.Terms;
using Api.Dtos.Mapping;
using Azure.Diagnostics;
using iTunesSearch.Library;
using RedditPodcastPoster.Auth0.Extensions;
using RedditPodcastPoster.BBC.Extensions;
using RedditPodcastPoster.Bluesky.Extensions;
using RedditPodcastPoster.Cloudflare.Extensions;
using RedditPodcastPoster.CloudflareRedirect.Extensions;
using RedditPodcastPoster.Common.Extensions;
using RedditPodcastPoster.Configuration.Extensions;
using RedditPodcastPoster.Configuration.Options;
using RedditPodcastPoster.ContentPublisher.Extensions;
using RedditPodcastPoster.Discovery.Extensions;
using RedditPodcastPoster.Discovery.Services;
using RedditPodcastPoster.EntitySearchIndexer.Extensions;
using RedditPodcastPoster.Episodes.Extensions;
using RedditPodcastPoster.Indexing.Extensions;
using RedditPodcastPoster.InternetArchive.Extensions;
using RedditPodcastPoster.People.Extensions;
using RedditPodcastPoster.Persistence.Extensions;
using RedditPodcastPoster.PodcastServices.Apple.Extensions;
using RedditPodcastPoster.PodcastServices.Extensions;
using RedditPodcastPoster.PodcastServices.Spotify.Extensions;
using RedditPodcastPoster.PodcastServices.YouTube.Configuration;
using RedditPodcastPoster.PodcastServices.YouTube.Extensions;
using RedditPodcastPoster.PushSubscriptions.Extensions;
using RedditPodcastPoster.Reddit.Extensions;
using RedditPodcastPoster.Reddit.Factories;
using RedditPodcastPoster.Subjects.Extensions;
using RedditPodcastPoster.Text.Extensions;
using RedditPodcastPoster.Twitter.Extensions;
using RedditPodcastPoster.UrlShortening.Extensions;
using RedditPodcastPoster.UrlSubmission.Extensions;

namespace Api;

public static class Ioc
{
    public static void ConfigureServices(IServiceCollection serviceCollection)
    {
        AdminRedditClientFactory.AddAdminRedditClient(serviceCollection);

        serviceCollection
            .AddEpisodesDomain()
            .AddRepositories()
            .AddTextSanitiser()
            .AddYouTubeServices(ApplicationUsage.Api)
            .AddSpotifyServices()
            .AddAppleServices()
            .AddPodcastServices()
            .AddCommonServices()
            .AddRemoteClient()
            .AddScoped(s => new iTunesSearchManager())
            .AddSubjectServices()
            .AddSubjectProvider()
            .AddPeopleServices()
            .AddUrlSubmission()
            .AddDiscoveryRepository()
            .AddSingleton<IDiscoveryResultDeduplicator, DiscoveryResultDeduplicator>()
            .AddScoped<IDiscoveryResultsService, DiscoveryResultsService>()
            .AddIndexer()
            .AddEliminationTerms()
            .AddContentPublishing()
            .AddTwitterServices()
            .AddBlueskyServices()
            .AddRedditServices()
            .AddCloudflareClients()
            .AddShortnerServices()
            .AddRedirectServices()
            .AddPushSubscriptionsRepository()
            .AddScoped<IClientPrincipalFactory, ClientPrincipalFactory>()
            .AddAuth0Validation()
            .AddBBCServices()
            .AddInternetArchiveServices()
            .AddHttpClient()
            .AddEpisodeSearchIndexerService()
            .AddScoped<PodcastEpisodeProjectionHelper>()
            .AddScoped<PodcastChangeApplier>()
            .AddScoped<IPodcastGetService, PodcastGetService>()
            .AddScoped<IPodcastUpdateService, PodcastUpdateService>()
            .AddScoped<IPodcastIndexService, PodcastIndexService>()
            .AddScoped<IPodcastRenameService, PodcastRenameService>()
            .AddScoped<IGetPodcastHandler, GetPodcastHandler>()
            .AddScoped<IPostPodcastHandler, PostPodcastHandler>()
            .AddScoped<IIndexPodcastHandler, IndexPodcastHandler>()
            .AddScoped<IRenamePodcastHandler, RenamePodcastHandler>()
            .AddScoped<EpisodeSearchIndexCleanup>()
            .AddScoped<EpisodeDiscreteMapper>()
            .AddScoped<EpisodeChangeApplier>()
            .AddScoped<IEpisodeDeleteService, EpisodeDeleteService>()
            .AddScoped<IEpisodeGetService, EpisodeGetService>()
            .AddScoped<IEpisodeOutgoingService, EpisodeOutgoingService>()
            .AddScoped<IEpisodeUpdateService, EpisodeUpdateService>()
            .AddScoped<IEpisodePublishService, EpisodePublishService>()
            .AddScoped<IDeleteEpisodeHandler, DeleteEpisodeHandler>()
            .AddScoped<IGetEpisodeHandler, GetEpisodeHandler>()
            .AddScoped<IGetOutgoingEpisodesHandler, GetOutgoingEpisodesHandler>()
            .AddScoped<IPostEpisodeHandler, PostEpisodeHandler>()
            .AddScoped<IPublishEpisodeHandler, PublishEpisodeHandler>()
            .AddScoped<IGetPublicEpisodeHandler, GetPublicEpisodeHandler>()
            .AddScoped<IPublicEpisodeGetService, PublicEpisodeGetService>()
            .AddScoped<IPublishHomepageHandler, PublishHomepageHandler>()
            .AddScoped<IHomepagePublishService, HomepagePublishService>()
            .AddScoped<ICreatePushSubscriptionHandler, CreatePushSubscriptionHandler>()
            .AddScoped<IPushSubscriptionCreateService, PushSubscriptionCreateService>()
            .AddScoped<IRunSearchIndexHandler, RunSearchIndexHandler>()
            .AddScoped<ISearchIndexRunService, SearchIndexRunService>()
            .AddScoped<IPostSubmitUrlHandler, PostSubmitUrlHandler>()
            .AddScoped<ISubmitUrlService, SubmitUrlService>()
            .AddScoped<SubjectChangeApplier>()
            .AddScoped<ISubjectGetService, SubjectGetService>()
            .AddScoped<ISubjectUpdateService, SubjectUpdateService>()
            .AddScoped<ISubjectCreateService, SubjectCreateService>()
            .AddScoped<IGetSubjectHandler, GetSubjectHandler>()
            .AddScoped<IPostSubjectHandler, PostSubjectHandler>()
            .AddScoped<IPutSubjectHandler, PutSubjectHandler>()
            .AddScoped<IPersonGetAllService, PersonGetAllService>()
            .AddScoped<IPersonGetService, PersonGetService>()
            .AddScoped<IPersonUpdateService, PersonUpdateService>()
            .AddScoped<IPersonCreateService, PersonCreateService>()
            .AddScoped<IGetAllPeopleHandler, GetAllPeopleHandler>()
            .AddScoped<IGetPersonHandler, GetPersonHandler>()
            .AddScoped<IPostPersonHandler, PostPersonHandler>()
            .AddScoped<IPutPersonHandler, PutPersonHandler>()
            .AddScoped<IPostTermsHandler, PostTermsHandler>()
            .AddScoped<ITermsSubmitService, TermsSubmitService>()
            .AddScoped<IDiscoveryCurationGetService, DiscoveryCurationGetService>()
            .AddScoped<IDiscoveryCurationSubmitService, DiscoveryCurationSubmitService>()
            .AddScoped<IGetDiscoveryCurationHandler, GetDiscoveryCurationHandler>()
            .AddScoped<IPostDiscoveryCurationHandler, PostDiscoveryCurationHandler>()
            .AddScoped<IDiscoveryScheduleGetService, DiscoveryScheduleGetService>()
            .AddScoped<IDiscoveryScheduleUpdateService, DiscoveryScheduleUpdateService>()
            .AddScoped<IGetDiscoveryScheduleHandler, GetDiscoveryScheduleHandler>()
            .AddScoped<IPutDiscoveryScheduleHandler, PutDiscoveryScheduleHandler>()
            .AddScoped<IPodcastEpisodeResolver, PodcastEpisodeResolver>()
            .BindConfiguration<HostingOptions>("hosting")
            .BindConfiguration<IndexerOptions>("indexer")
            .BindConfiguration<MemoryProbeOptions>("memoryProbe")
            .AddSingleton<IMemoryProbeOrchestrator, MemoryProbeOrchestrator>()
            .AddPostingCriteria();
    }
}