using Api.Services.Catalogue;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Api.Dtos.Mapping;
using Api.Handlers.Discovery;
using Api.Handlers.DiscoverySchedule;
using Api.Handlers.DiscoverySubmissions;
using Api.Handlers.Episodes;
using Api.Handlers.Catalogue;
using Api.Handlers.Films;
using Api.Handlers.Homepage;
using Api.Handlers.People;
using Api.Handlers.Podcasts;
using Api.Handlers.Public;
using Api.Handlers.PushSubscriptions;
using Api.Handlers.SearchIndex;
using Api.Handlers.Subjects;
using Api.Handlers.SubmitUrl;
using Api.Handlers.SupportedLanguages;
using Api.Handlers.TitleCasingRules;
using Api.Handlers.News;
using Api.Handlers.TvShows;
using Api.Resolvers;
using Api.Services.Discovery;
using Api.Services.DiscoverySchedule;
using Api.Services.Episodes;
using Api.Services.Films;
using Api.Services.Homepage;
using Api.Services.People;
using Api.Services.Podcasts;
using Api.Services.Public;
using Api.Services.PushSubscriptions;
using Api.Services.SearchIndex;
using Api.Services.Subjects;
using Api.Services.SubmitUrl;
using Api.Services.SupportedLanguages;
using Api.Services.TitleCasingRules;
using Api.Services.News;
using Api.Services.TvShows;

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
            .AddScoped<EpisodeUpdateService>()
            .AddScoped<IEpisodeUpdateService>(sp =>
            {
                var inner = sp.GetRequiredService<EpisodeUpdateService>();
                return TimedEpisodeUpdateService.EnableDiagnosticTiming
                    ? new TimedEpisodeUpdateService(
                        inner,
                        sp.GetRequiredService<ILogger<TimedEpisodeUpdateService>>())
                    : inner;
            })
            .AddScoped<IEpisodePublishService, EpisodePublishService>()
            .AddScoped<IEpisodeFilmMoveService, EpisodeFilmMoveService>()
            .AddScoped<IPostEpisodeFilmMoveHandler, PostEpisodeFilmMoveHandler>()
            .AddScoped<IEpisodeTrailerAttachService, EpisodeTrailerAttachService>()
            .AddScoped<IPostEpisodeTrailerAttachHandler, PostEpisodeTrailerAttachHandler>()
            .AddScoped<IEpisodeTvShowMoveService, EpisodeTvShowMoveService>()
            .AddScoped<IPostEpisodeTvShowMoveHandler, PostEpisodeTvShowMoveHandler>()
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
            .AddScoped<IPodcastKindTransferService, PodcastKindTransferService>()
            .AddScoped<IPodcastIndexService, PodcastIndexService>()
            .AddScoped<IPodcastRenameService, PodcastRenameService>()
            .AddScoped<IGetPodcastHandler, GetPodcastHandler>()
            .AddScoped<IPostPodcastHandler, PostPodcastHandler>()
            .AddScoped<IPostPodcastKindTransferHandler, PostPodcastKindTransferHandler>()
            .AddScoped<IIndexPodcastHandler, IndexPodcastHandler>()
            .AddScoped<IRenamePodcastHandler, RenamePodcastHandler>()
            .AddScoped<IPodcastEpisodeResolver, PodcastEpisodeResolver>()
            .AddScoped<ICataloguePlayableResolver, CataloguePlayableResolver>();

    public static IServiceCollection AddApiTvShows(this IServiceCollection services) =>
        services
            .AddCatalogueEditing()
            .AddScoped<ITvShowService, TvShowService>()
            .AddScoped<IGetTvShowHandler, GetTvShowHandler>()
            .AddScoped<IPostTvShowHandler, PostTvShowHandler>()
            .AddScoped<ITvShowEpisodeService, TvShowEpisodeService>()
            .AddScoped<IGetTvShowEpisodeHandler, GetTvShowEpisodeHandler>()
            .AddScoped<IPostTvShowEpisodeHandler, PostTvShowEpisodeHandler>();

    /// <summary>Shared by the news, TV and film editors (post-status retraction, rename). Safe to call more than once.</summary>
    private static IServiceCollection AddCatalogueEditing(this IServiceCollection services)
    {
        services.TryAddScoped<IPlayableSocialRetraction, PlayableSocialRetraction>();
        services.TryAddScoped<IServiceIdsUniqueness, ServiceIdsUniqueness>();
        services.TryAddScoped<PlayableSearchRename>();
        services.TryAddScoped<CataloguePlayableSearchSync>();
        services.TryAddScoped<ICatalogueRenameService, CatalogueRenameService>();
        services.TryAddScoped<ICatalogueRenameHandler, CatalogueRenameHandler>();
        return services;
    }

    public static IServiceCollection AddApiNews(this IServiceCollection services) =>
        services
            .AddCatalogueEditing()
            .AddScoped<INewsOrganisationService, NewsOrganisationService>()
            .AddScoped<IGetNewsOrganisationHandler, GetNewsOrganisationHandler>()
            .AddScoped<IPostNewsOrganisationHandler, PostNewsOrganisationHandler>()
            .AddScoped<INewsReportService, NewsReportService>()
            .AddScoped<IGetNewsReportHandler, GetNewsReportHandler>()
            .AddScoped<IPostNewsReportHandler, PostNewsReportHandler>();

    public static IServiceCollection AddApiFilms(this IServiceCollection services) =>
        services
            .AddCatalogueEditing()
            .AddScoped<IFilmService, FilmService>()
            .AddScoped<IGetFilmHandler, GetFilmHandler>()
            .AddScoped<IPostFilmHandler, PostFilmHandler>();

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
            .AddScoped<IGetSubmitUrlLookupHandler, GetSubmitUrlLookupHandler>()
            .AddScoped<IPostSubmitUrlPrepareHandler, PostSubmitUrlPrepareHandler>()
            .AddScoped<IPostSubmitUrlExtractHandler, PostSubmitUrlExtractHandler>()
            .AddScoped<ISubmitUrlService, SubmitUrlService>()
            .AddScoped<ISubmitUrlLookupService, SubmitUrlLookupService>()
            .AddScoped<ISubmitUrlPrepareService, SubmitUrlPrepareService>();

    public static IServiceCollection AddApiDiscovery(this IServiceCollection services) =>
        services
            .AddScoped<IDiscoveryCurationGetService, DiscoveryCurationGetService>()
            .AddScoped<IDiscoveryCurationSubmitService, DiscoveryCurationSubmitService>()
            .AddScoped<IDiscoverySubmissionHoldService, DiscoverySubmissionHoldService>()
            .AddTimeProviderIfMissing()
            .AddScoped<IGetDiscoveryCurationHandler, GetDiscoveryCurationHandler>()
            .AddScoped<IPostDiscoveryCurationHandler, PostDiscoveryCurationHandler>()
            .AddScoped<IDiscoveryChannelBanService, DiscoveryChannelBanService>()
            .AddScoped<IPostDiscoveryChannelBanHandler, PostDiscoveryChannelBanHandler>()
            .AddScoped<IDiscoverySubmissionReviewService, DiscoverySubmissionReviewService>()
            .AddScoped<IDiscoverySubmissionIngestedEpisodes, DiscoverySubmissionIngestedEpisodes>()
            .AddScoped<IGetDiscoverySubmissionsHandler, GetDiscoverySubmissionsHandler>()
            .AddScoped<IGetDiscoverySubmissionHandler, GetDiscoverySubmissionHandler>()
            .AddScoped<IPutDiscoverySubmissionReviewHandler, PutDiscoverySubmissionReviewHandler>()
            .AddScoped<IPostDiscoverySubmissionApplyHandler, PostDiscoverySubmissionApplyHandler>()
            .AddScoped<IPostDiscoverySubmissionRetryHandler, PostDiscoverySubmissionRetryHandler>()
            .AddScoped<IPostDiscoverySubmissionReleaseHandler, PostDiscoverySubmissionReleaseHandler>()
            .AddScoped<IPostDiscoveryReportReleaseHandler, PostDiscoveryReportReleaseHandler>()
            .AddScoped<IPostDiscoverySubmissionStateHandler, PostDiscoverySubmissionStateHandler>();

    public static IServiceCollection AddApiDiscoverySchedule(this IServiceCollection services) =>
        services
            .AddScoped<IDiscoveryScheduleGetService, DiscoveryScheduleGetService>()
            .AddScoped<IDiscoveryScheduleUpdateService, DiscoveryScheduleUpdateService>()
            .AddScoped<IGetDiscoveryScheduleHandler, GetDiscoveryScheduleHandler>()
            .AddScoped<IPutDiscoveryScheduleHandler, PutDiscoveryScheduleHandler>();

    public static IServiceCollection AddApiSupportedLanguages(this IServiceCollection services) =>
        services
            .AddScoped<ISupportedLanguagesGetService, SupportedLanguagesGetService>()
            .AddScoped<ISupportedLanguagesUpdateService, SupportedLanguagesUpdateService>()
            .AddScoped<IGetSupportedLanguagesHandler, GetSupportedLanguagesHandler>()
            .AddScoped<IPostSupportedLanguagesHandler, PostSupportedLanguagesHandler>()
            .AddScoped<IDeleteSupportedLanguagesHandler, DeleteSupportedLanguagesHandler>()
            .AddScoped<IGetNeutralCulturesHandler, GetNeutralCulturesHandler>()
            .AddScoped<RedditPodcastPoster.UrlSubmission.Languages.ISupportedLanguageRegistrar, SupportedLanguageRegistrar>();

    public static IServiceCollection AddApiTitleCasingRules(this IServiceCollection services) =>
        services
            .AddScoped<ITitleCasingRulesGetService, TitleCasingRulesGetService>()
            .AddScoped<ITitleCasingRulesUpdateService, TitleCasingRulesUpdateService>()
            .AddScoped<IGetTitleCasingRulesByLanguageHandler, GetTitleCasingRulesByLanguageHandler>()
            .AddScoped<IPostTitleCasingRulesLowerCaseTermHandler, PostTitleCasingRulesLowerCaseTermHandler>()
            .AddScoped<IDeleteTitleCasingRulesLowerCaseTermHandler, DeleteTitleCasingRulesLowerCaseTermHandler>()
            .AddScoped<IPostTitleCasingRulesKnownTermHandler, PostTitleCasingRulesKnownTermHandler>()
            .AddScoped<IDeleteTitleCasingRulesKnownTermHandler, DeleteTitleCasingRulesKnownTermHandler>()
            .AddScoped<IPostTitleCasingRulesIgnoredSubjectHandler, PostTitleCasingRulesIgnoredSubjectHandler>()
            .AddScoped<IDeleteTitleCasingRulesIgnoredSubjectHandler, DeleteTitleCasingRulesIgnoredSubjectHandler>();

    private static IServiceCollection AddTimeProviderIfMissing(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
