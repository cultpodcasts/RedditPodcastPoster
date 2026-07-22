using Api.Models;

namespace Api.Services.Subjects;

public interface ISubjectCreateService
{
    Task<SubjectCreateResult> CreateAsync(SubjectChangeRequest subject, CancellationToken cancellationToken);
}
