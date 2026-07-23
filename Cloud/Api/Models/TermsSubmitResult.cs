namespace Api.Models;

public enum TermsSubmitStatus
{
    Ok,
    Conflict,
    Failed
}

public record TermsSubmitResult(TermsSubmitStatus Status);
