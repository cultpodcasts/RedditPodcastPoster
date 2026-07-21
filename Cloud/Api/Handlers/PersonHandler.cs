using System.Net;
using System.Text.Json;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Extensions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
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
                .OrderBy(x => x.GetEffectiveSortKey())
                .ThenBy(x => x.Name)
                .Select(x => x.ToDto())
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
            var person = await personRepository.GetByName(personName);
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

            var change = personChangeRequestWrapper.Person;
            if (change.Name != null)
            {
                if (string.IsNullOrWhiteSpace(change.Name))
                {
                    return await req.CreateResponse(HttpStatusCode.BadRequest)
                        .WithJsonBody(new { message = "Person name cannot be empty" }, c);
                }

                var trimmedName = change.Name.Trim();
                var newNameKey = PersonEntity.NormalizeNameKey(trimmedName);
                person.EnsureNameKey();
                if (newNameKey != person.NameKey)
                {
                    var nameConflict = await personRepository.GetByName(trimmedName);
                    if (nameConflict != null && nameConflict.Id != person.Id)
                    {
                        return await req.CreateResponse(HttpStatusCode.Conflict)
                            .WithJsonBody(new { conflict = nameConflict.Name }, c);
                    }
                }
            }

            UpdatePerson(person, change);
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
            person.BlueskyHandle,
            person.SortName,
            person.IsOrganization ?? false);

        var nameConflict = await personRepository.GetByName(entity.Name);
        if (nameConflict != null)
        {
            return await req.CreateResponse(HttpStatusCode.Conflict)
                .WithJsonBody(new { conflict = nameConflict.Name }, ct);
        }

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
        if (change.Name != null && !string.IsNullOrWhiteSpace(change.Name))
        {
            entity.Name = change.Name.Trim();
            entity.EnsureNameKey();
        }

        if (change.IsOrganization.HasValue)
        {
            entity.IsOrganization = change.IsOrganization.Value;
        }

        if (change.SortName != null)
        {
            entity.SortName = PersonSortNameResolver.ResolveForPersist(
                entity.Name,
                change.SortName,
                entity.IsOrganization);
        }
        else if (change.IsOrganization == true)
        {
            entity.SortName = PersonSortNameResolver.ResolveForPersist(
                entity.Name,
                entity.SortName,
                isOrganization: true);
        }
        else if (change.IsOrganization == false)
        {
            // Dropping the org flag without an explicit sortName → surname default.
            entity.SortName = PersonSortNameResolver.ResolveForPersist(
                entity.Name,
                sortName: null,
                isOrganization: false);
        }

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
