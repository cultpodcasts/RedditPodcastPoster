using Api.Dtos;

namespace Api.Models;

public enum SubmitUrlPrepareStatus
{
    Ok,
    BadRequest,
    Failed
}

public record SubmitUrlPrepareResult(
    SubmitUrlPrepareStatus Status,
    SubmitUrlPrepareResponse? Response = null,
    string? Message = null);
