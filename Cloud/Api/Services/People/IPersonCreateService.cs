using Api.Models;

namespace Api.Services.People;

public interface IPersonCreateService
{
    Task<PersonCreateResult> CreateAsync(PersonChangeRequest person, CancellationToken cancellationToken);
}
