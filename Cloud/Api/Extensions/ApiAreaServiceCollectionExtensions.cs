using Microsoft.Extensions.DependencyInjection;
using Api.Dtos.Mapping;
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
using Api.Services.Discovery;
using Api.Services.DiscoverySchedule;
using Api.Services.Episodes;
using Api.Services.Homepage;
using Api.Services.People;
using Api.Services.Podcasts;
using Api.Services.Public;
using Api.Services.PushSubscriptions;
using Api.Services.SearchIndex;
using Api.Services.Subjects;
using Api.Services.SubmitUrl;
using Api.Services.Terms;

namespace Api.Extensions;

/// <summary>
/// Area-scoped DI registration for Cloud/Api handlers and services.
/// Called from <see cref="Ioc"/> after domain library Add* extensions.
/// </summary>
public static class ApiAreaServiceCollectionExtensions
{
    public static IServiceCollection AddApiEpisodes(this IServiceCollection services) =>
        services
            .AddScoped<EpisodeSearchIndexCleanup>()
            .AddScoped<EpisodeDtoMapper>()
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
            .AddScoped<IPublishEpisodeHandler, PublishEpisodeHandler>();

    public static IServiceCollection AddApiPodcasts(this IServiceCollection services) =>
        services
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
            .AddScoped<IPodcastEpisodeResolver, PodcastEpisodeResolver>();

    public static IServiceCollection AddApiPeople(this IServiceCollection services) =>
        services
            .AddScoped<IPersonGetAllService, PersonGetAllService>()
            .AddScoped<IPersonGetService, PersonGetService>()
            .AddScoped<IPersonUpdateService, PersonUpdateService>()
            .AddScoped<IPersonCreateService, PersonCreateService>()
            .AddScoped<IGetAllPeopleHandler, GetAllPeopleHandler>()
            .AddScoped<IGetPersonHandler, GetPersonHandler>()
            .AddScoped<IPostPersonHandler, PostPersonHandler>()
            .AddScoped<IPutPersonHandler, PutPersonHandler>();

    public static IServiceCollection AddApiSubjects(this IServiceCollection services) =>
        services
            .AddScoped<SubjectChangeApplier>()
            .AddScoped<ISubjectGetService, SubjectGetService>()
            .AddScoped<ISubjectUpdateService, SubjectUpdateService>()
            .AddScoped<ISubjectCreateService, SubjectCreateService>()
            .AddScoped<IGetSubjectHandler, GetSubjectHandler>()
            .AddScoped<IPostSubjectHandler, PostSubjectHandler>()
            .AddScoped<IPutSubjectHandler, PutSubjectHandler>();

    public static IServiceCollection AddApiPublic(this IServiceCollection services) =>
        services
            .AddScoped<IGetPublicEpisodeHandler, GetPublicEpisodeHandler>()
            .AddScoped<IPublicEpisodeGetService, PublicEpisodeGetService>();

    public static IServiceCollection AddApiHomepage(this IServiceCollection services) =>
        services
            .AddScoped<IPublishHomepageHandler, PublishHomepageHandler>()
            .AddScoped<IHomepagePublishService, HomepagePublishService>();

    public static IServiceCollection AddApiPushSubscriptions(this IServiceCollection services) =>
        services
            .AddScoped<ICreatePushSubscriptionHandler, CreatePushSubscriptionHandler>()
            .AddScoped<IPushSubscriptionCreateService, PushSubscriptionCreateService>();

    public static IServiceCollection AddApiSearchIndex(this IServiceCollection services) =>
        services
            .AddScoped<IRunSearchIndexHandler, RunSearchIndexHandler>()
            .AddScoped<ISearchIndexRunService, SearchIndexRunService>();

    public static IServiceCollection AddApiSubmitUrl(this IServiceCollection services) =>
        services
            .AddScoped<IPostSubmitUrlHandler, PostSubmitUrlHandler>()
            .AddScoped<ISubmitUrlService, SubmitUrlService>();

    public static IServiceCollection AddApiTerms(this IServiceCollection services) =>
        services
            .AddScoped<IPostTermsHandler, PostTermsHandler>()
            .AddScoped<ITermsSubmitService, TermsSubmitService>();

    public static IServiceCollection AddApiDiscovery(this IServiceCollection services) =>
        services
            .AddScoped<IDiscoveryCurationGetService, DiscoveryCurationGetService>()
            .AddScoped<IDiscoveryCurationSubmitService, DiscoveryCurationSubmitService>()
            .AddScoped<IGetDiscoveryCurationHandler, GetDiscoveryCurationHandler>()
            .AddScoped<IPostDiscoveryCurationHandler, PostDiscoveryCurationHandler>();

    public static IServiceCollection AddApiDiscoverySchedule(this IServiceCollection services) =>
        services
            .AddScoped<IDiscoveryScheduleGetService, DiscoveryScheduleGetService>()
            .AddScoped<IDiscoveryScheduleUpdateService, DiscoveryScheduleUpdateService>()
            .AddScoped<IGetDiscoveryScheduleHandler, GetDiscoveryScheduleHandler>()
            .AddScoped<IPutDiscoveryScheduleHandler, PutDiscoveryScheduleHandler>();
}
