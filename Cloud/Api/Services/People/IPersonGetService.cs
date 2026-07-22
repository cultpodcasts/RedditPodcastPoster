using Api.Models;

namespace Api.Services.People;

public interface IPersonGetService
{
    Task<PersonGetResult> GetAsync(string personName, CancellationToken cancellationToken);
}
