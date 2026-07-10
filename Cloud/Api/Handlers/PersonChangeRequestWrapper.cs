using Api.Dtos;

namespace Api.Handlers;

public record PersonChangeRequestWrapper(Guid PersonId, Person Person);
