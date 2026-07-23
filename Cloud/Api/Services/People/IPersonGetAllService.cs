using Api.Models;

namespace Api.Services.People;

public interface IPersonGetAllService
{
    Task<PersonGetAllResult> GetAllAsync(CancellationToken cancellationToken);
}
