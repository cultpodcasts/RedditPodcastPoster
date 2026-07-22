using Api.Dtos;

namespace Api.Models;

public record PersonChangeRequestWrapper(Guid PersonId, Person Person);
