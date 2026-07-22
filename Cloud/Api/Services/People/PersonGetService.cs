using Api.Dtos.Extensions;
using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.People;

public class PersonGetService(
    IPersonRepository personRepository,
    ILogger<PersonGetService> logger) : IPersonGetService
{
    public async Task<PersonGetResult> GetAsync(string personName, CancellationToken cancellationToken)
    {
        try
        {
            var person = await personRepository.GetByName(personName);
            if (person == null)
            {
                return new PersonGetResult(PersonGetStatus.NotFound);
            }

            return new PersonGetResult(PersonGetStatus.Ok, person.ToDto());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to get person.", nameof(GetAsync));
            return new PersonGetResult(PersonGetStatus.Failed);
        }
    }
}
