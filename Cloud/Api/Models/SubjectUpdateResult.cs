namespace Api.Models;

public enum SubjectUpdateStatus
{
    Accepted,
    NotFound,
    Failed
}

public record SubjectUpdateResult(SubjectUpdateStatus Status);
