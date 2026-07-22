using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Api.Models;
using Api.Services.People;
using RedditPodcastPoster.Models.People;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using Xunit;

namespace FunctionHost.Tests.Api.Services;

public class PersonGetServiceTests
{
    [Fact(DisplayName = "Get returns NotFound when repository has no match")]
    public async Task Get_returns_not_found()
    {
        var repo = new Mock<IPersonRepository>();
        repo.Setup(r => r.GetByName("missing")).ReturnsAsync((Person?)null);

        var service = new PersonGetService(repo.Object, NullLogger<PersonGetService>.Instance);
        var result = await service.GetAsync("missing", CancellationToken.None);

        result.Status.Should().Be(PersonGetStatus.NotFound);
        result.Person.Should().BeNull();
    }

    [Fact(DisplayName = "Get returns Ok with domain person")]
    public async Task Get_returns_ok()
    {
        var person = new Person("Ada") { Id = Guid.NewGuid() };
        var repo = new Mock<IPersonRepository>();
        repo.Setup(r => r.GetByName("Ada")).ReturnsAsync(person);

        var service = new PersonGetService(repo.Object, NullLogger<PersonGetService>.Instance);
        var result = await service.GetAsync("Ada", CancellationToken.None);

        result.Status.Should().Be(PersonGetStatus.Ok);
        result.Person.Should().BeSameAs(person);
    }
}
