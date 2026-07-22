using System.Net;
using FluentAssertions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
using Api.Models;
using Api.Services.People;
using Api.Services.Public;
using RedditPodcastPoster.Models.Episodes;
using RedditPodcastPoster.Models.People;
using RedditPodcastPoster.Models.Podcasts;
using Xunit;
using Episode = RedditPodcastPoster.Models.Episodes.Episode;
using Podcast = RedditPodcastPoster.Models.Podcasts.Podcast;
using Person = RedditPodcastPoster.Models.People.Person;

namespace FunctionHost.Tests.Api.Handlers;

public class ThinHandlerTests
{
    [Fact(DisplayName = "GetPublicEpisodeHandler maps Ok to 200")]
    public async Task Public_get_ok_returns_200()
    {
        var episode = new Episode { Id = Guid.NewGuid(), Title = "Ep" };
        var podcast = new Podcast { Id = Guid.NewGuid(), Name = "Show" };
        var service = new Mock<IPublicEpisodeGetService>();
        service.Setup(s => s.GetAsync(It.IsAny<PodcastEpisodeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicEpisodeGetResult(PublicEpisodeGetStatus.Ok, episode, podcast));

        var handler = new GetPublicEpisodeHandler(service.Object, NullLogger<GetPublicEpisodeHandler>.Instance);
        var (req, response) = HttpTestHelpers.CreateRequestResponse("GET");

        var result = await handler.Handle(req.Object, new PodcastEpisodeRequestWrapper(episode.Id), null, CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "GetPublicEpisodeHandler maps NotFound to 404")]
    public async Task Public_get_not_found_returns_404()
    {
        var service = new Mock<IPublicEpisodeGetService>();
        service.Setup(s => s.GetAsync(It.IsAny<PodcastEpisodeRequestWrapper>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublicEpisodeGetResult(PublicEpisodeGetStatus.NotFound));

        var handler = new GetPublicEpisodeHandler(service.Object, NullLogger<GetPublicEpisodeHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        var result = await handler.Handle(req.Object, new PodcastEpisodeRequestWrapper(Guid.NewGuid()), null, CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "GetPersonHandler maps Ok to 200")]
    public async Task Person_get_ok_returns_200()
    {
        var person = new Person("Ada") { Id = Guid.NewGuid() };
        var service = new Mock<IPersonGetService>();
        service.Setup(s => s.GetAsync("Ada", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PersonGetResult(PersonGetStatus.Ok, person));

        var handler = new GetPersonHandler(service.Object, NullLogger<GetPersonHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        var result = await handler.Handle(req.Object, "Ada", null, CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "GetPersonHandler maps NotFound to 404")]
    public async Task Person_get_not_found_returns_404()
    {
        var service = new Mock<IPersonGetService>();
        service.Setup(s => s.GetAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PersonGetResult(PersonGetStatus.NotFound));

        var handler = new GetPersonHandler(service.Object, NullLogger<GetPersonHandler>.Instance);
        var (req, _) = HttpTestHelpers.CreateRequestResponse("GET");

        var result = await handler.Handle(req.Object, "missing", null, CancellationToken.None);

        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
