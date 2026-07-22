using Api;
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
using FluentAssertions;
using Xunit;

namespace FunctionHost.Tests;

public class ApiIocTests
{
    public static IEnumerable<object[]> EntryPoints =>
    [
        [typeof(IGetPodcastHandler)],
        [typeof(IPostPodcastHandler)],
        [typeof(IIndexPodcastHandler)],
        [typeof(IRenamePodcastHandler)],
        [typeof(IGetEpisodeHandler)],
        [typeof(IGetOutgoingEpisodesHandler)],
        [typeof(IPostEpisodeHandler)],
        [typeof(IPublishEpisodeHandler)],
        [typeof(IDeleteEpisodeHandler)],
        [typeof(IGetPublicEpisodeHandler)],
        [typeof(IPublishHomepageHandler)],
        [typeof(ICreatePushSubscriptionHandler)],
        [typeof(IRunSearchIndexHandler)],
        [typeof(IPostSubmitUrlHandler)],
        [typeof(IGetSubjectHandler)],
        [typeof(IPostSubjectHandler)],
        [typeof(IPutSubjectHandler)],
        [typeof(IGetAllPeopleHandler)],
        [typeof(IGetPersonHandler)],
        [typeof(IPostPersonHandler)],
        [typeof(IPutPersonHandler)],
        [typeof(IPostTermsHandler)],
        [typeof(IGetDiscoveryCurationHandler)],
        [typeof(IPostDiscoveryCurationHandler)],
        [typeof(IGetDiscoveryScheduleHandler)],
        [typeof(IPutDiscoveryScheduleHandler)],
    ];

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public async Task Api_host_resolves_entry_point(Type entryPointType)
    {
        var services = FunctionHostTestSupport.CreateServiceCollection(Ioc.ConfigureServices);

        var act = async () => await FunctionHostTestSupport.ValidateEntryPointAsync(services, entryPointType);

        await act.Should().NotThrowAsync();
    }
}
