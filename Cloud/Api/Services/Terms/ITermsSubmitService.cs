using Api.Models;

namespace Api.Services.Terms;

public interface ITermsSubmitService
{
    Task<TermsSubmitResult> SubmitAsync(TermSubmitRequest request, CancellationToken cancellationToken);
}
