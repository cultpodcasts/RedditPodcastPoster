using Api.Models;
using PersonDto = Api.Dtos.Person;

namespace Api.Services.People;

public interface IPersonCreateService
{
    Task<PersonCreateResult> CreateAsync(PersonDto person, CancellationToken cancellationToken);
}
