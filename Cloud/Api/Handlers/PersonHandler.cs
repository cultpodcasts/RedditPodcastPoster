using System.Net;
using System.Text.Json;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Extensions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.People;
using RedditPodcastPoster.People.Factories;
using PersonEntity = RedditPodcastPoster.Models.Person;
using PersonDto = Api.Dtos.Person;

namespace Api.Handlers;

public class PersonHandler(
    IPersonRepository personRepository,
    IPersonService personService,
    IPersonFactory personFactory,
    IPeoplePublisher peoplePublisher,
    ILogger<PersonHandler> logger) : IPersonHandler
{
    public async Task<HttpResponseData> GetAll(HttpRequestData req, ClientPrincipal? _, CancellationToken c)
    {
        try
        {
            var people = await personRepository.GetAll()
                .Select(x => x.ToDto())
                .OrderBy(x => x.Name)
                .ToListAsync(c);
            return await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(people, c);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to list people.", nameof(GetAll));
        }

        return await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve people"), c);
    }

    public async Task<HttpResponseData> Get(HttpRequestData req, string personName, ClientPrincipal? _, CancellationToken c)
    {
        try
        {
            var person = await personRepository.GetBy(x => x.Name == personName);
            if (person == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            return await req.CreateResponse(HttpStatusCode.OK).WithJsonBody(person.ToDto(), c);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to get person.", nameof(Get));
        }

        return await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve person"), c);
    }

    public async Task<HttpResponseData> Post(
        HttpRequestData req,
        PersonChangeRequestWrapper personChangeRequestWrapper,
        ClientPrincipal? _,
        CancellationToken c)
    {
        try
        {
            var person = await personRepository.GetBy(x => x.Id == personChangeRequestWrapper.PersonId);
            if (person == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            UpdatePerson(person, personChangeRequestWrapper.Person);
            await personRepository.Save(person);
            await peoplePublisher.PublishPeople();
            return req.CreateResponse(HttpStatusCode.Accepted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update person.", nameof(Post));
        }

        return await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to update person"), c);
    }

    public async Task<HttpResponseData> Put(HttpRequestData req, PersonDto person, ClientPrincipal? _, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(person.Name))
        {
            return await req.CreateResponse(HttpStatusCode.BadRequest)
                .WithJsonBody(new { message = "Missing person name" }, ct);
        }

        var entity = personFactory.Create(
            person.Name,
            person.Aliases,
            person.TwitterHandle,
            person.BlueskyHandle);
        var matchingPerson = await personService.Match(person.Name);
        if (matchingPerson != null)
        {
            return await req.CreateResponse(HttpStatusCode.Conflict)
                .WithJsonBody(new { conflict = matchingPerson.Name }, ct);
        }

        await personRepository.Save(entity);
        await peoplePublisher.PublishPeople();
        return await req.CreateResponse(HttpStatusCode.Accepted).WithJsonBody(entity.ToDto(), ct);
    }

    private static void UpdatePerson(PersonEntity entity, PersonDto change)
    {
        if (change.Aliases != null)
        {
            entity.Aliases = change.Aliases.Length == 0
                ? null
                : change.Aliases.Select(x => x.Trim()).ToArray();
        }

        if (change.TwitterHandle != null)
        {
            entity.TwitterHandle = string.IsNullOrWhiteSpace(change.TwitterHandle)
                ? null
                : PersonFactory.NormalizeHandle(change.TwitterHandle);
        }

        if (change.BlueskyHandle != null)
        {
            entity.BlueskyHandle = string.IsNullOrWhiteSpace(change.BlueskyHandle)
                ? null
                : PersonFactory.NormalizeHandle(change.BlueskyHandle);
        }
    }
}
