using Api.Models;

namespace Api.Services.People;

public interface IPersonUpdateService
{
    Task<PersonUpdateResult> UpdateAsync(
        PersonChangeRequestWrapper request,
        CancellationToken cancellationToken);
}
