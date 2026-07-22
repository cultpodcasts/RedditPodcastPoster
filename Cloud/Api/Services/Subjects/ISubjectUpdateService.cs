using Api.Dtos;
using Api.Models;

namespace Api.Services.Subjects;

public interface ISubjectUpdateService
{
    Task<SubjectUpdateResult> UpdateAsync(
        SubjectChangeRequestWrapper request,
        CancellationToken cancellationToken);
}
