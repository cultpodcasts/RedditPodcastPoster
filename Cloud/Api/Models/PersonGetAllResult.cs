using Api.Dtos;

namespace Api.Models;

public enum PersonGetAllStatus
{
    Ok,
    Failed
}

public record PersonGetAllResult(
    PersonGetAllStatus Status,
    IReadOnlyList<Person>? People = null);
