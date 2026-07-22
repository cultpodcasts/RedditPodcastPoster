using Api.Dtos;

namespace Api.Models;

public enum SearchIndexRunStatus
{
    Ok,
    BadRequest,
    Failed
}

public record SearchIndexRunResult(
    SearchIndexRunStatus Status,
    IndexerState? Response = null);
