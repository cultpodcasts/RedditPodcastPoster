using Api.Models;

namespace Api.Services.Subjects;

public interface ISubjectGetService
{
    Task<SubjectGetResult> GetAsync(string subjectName, CancellationToken cancellationToken);
}
