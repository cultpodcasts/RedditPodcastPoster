using Api.Dtos.Extensions;
using Api.Models;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Persistence.Abstractions.Repositories;

namespace Api.Services.People;

public class PersonGetAllService(
    IPersonRepository personRepository,
    ILogger<PersonGetAllService> logger) : IPersonGetAllService
{
    public async Task<PersonGetAllResult> GetAllAsync(CancellationToken cancellationToken)
    {
        try
        {
            var people = await personRepository.GetAll()
                .OrderBy(x => x.GetEffectiveSortKey())
                .ThenBy(x => x.Name)
                .Select(x => x.ToDto())
                .ToListAsync(cancellationToken);
            return new PersonGetAllResult(PersonGetAllStatus.Ok, people);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to list people.", nameof(GetAllAsync));
            return new PersonGetAllResult(PersonGetAllStatus.Failed);
        }
    }
}
