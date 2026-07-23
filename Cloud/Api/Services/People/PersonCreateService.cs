using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.People.Factories;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;
using RedditPodcastPoster.People.Services;

namespace Api.Services.People;

public class PersonCreateService(
    IPersonRepository personRepository,
    IPersonService personService,
    IPersonFactory personFactory,
    IPeoplePublisher peoplePublisher,
    ILogger<PersonCreateService> logger) : IPersonCreateService
{
    public async Task<PersonCreateResult> CreateAsync(PersonChangeRequest person, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(person.Name))
            {
                return new PersonCreateResult(
                    PersonCreateStatus.BadRequest,
                    Message: "Missing person name");
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
                return new PersonCreateResult(
                    PersonCreateStatus.Conflict,
                    ConflictName: nameConflict.Name);
            }

            var matchingPerson = await personService.Match(person.Name);
            if (matchingPerson != null)
            {
                return new PersonCreateResult(
                    PersonCreateStatus.Conflict,
                    ConflictName: matchingPerson.Name);
            }

            await personRepository.Save(entity);
            await peoplePublisher.PublishPeople();
            return new PersonCreateResult(PersonCreateStatus.Accepted, entity);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to create person.", nameof(CreateAsync));
            return new PersonCreateResult(PersonCreateStatus.Failed);
        }
    }
}
