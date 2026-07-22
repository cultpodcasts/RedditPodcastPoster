using Api.Models;
using Microsoft.Extensions.Logging;
using PersonEntity = RedditPodcastPoster.Models.People.Person;
using RedditPodcastPoster.ContentPublisher.Publishers;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.People;

public class PersonUpdateService(
    IPersonRepository personRepository,
    IPeoplePublisher peoplePublisher,
    ILogger<PersonUpdateService> logger) : IPersonUpdateService
{
    public async Task<PersonUpdateResult> UpdateAsync(
        PersonChangeRequestWrapper personChangeRequestWrapper,
        CancellationToken cancellationToken)
    {
        try
        {
            var person = await personRepository.GetBy(x => x.Id == personChangeRequestWrapper.PersonId);
            if (person == null)
            {
                return new PersonUpdateResult(PersonUpdateStatus.NotFound);
            }

            var change = personChangeRequestWrapper.Person;
            if (change.Name != null)
            {
                if (string.IsNullOrWhiteSpace(change.Name))
                {
                    return new PersonUpdateResult(
                        PersonUpdateStatus.BadRequest,
                        Message: "Person name cannot be empty");
                }

                var trimmedName = change.Name.Trim();
                var newNameKey = PersonEntity.NormalizeNameKey(trimmedName);
                person.EnsureNameKey();
                if (newNameKey != person.NameKey)
                {
                    var nameConflict = await personRepository.GetByName(trimmedName);
                    if (nameConflict != null && nameConflict.Id != person.Id)
                    {
                        return new PersonUpdateResult(
                            PersonUpdateStatus.Conflict,
                            ConflictName: nameConflict.Name);
                    }
                }
            }

            PersonChangeApplier.Apply(person, change);
            await personRepository.Save(person);
            await peoplePublisher.PublishPeople();
            return new PersonUpdateResult(PersonUpdateStatus.Accepted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update person.", nameof(UpdateAsync));
            return new PersonUpdateResult(PersonUpdateStatus.Failed);
        }
    }
}
