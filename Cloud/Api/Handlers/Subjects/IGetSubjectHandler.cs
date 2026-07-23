using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.Subjects;

public interface IGetSubjectHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        string subjectName,
        CancellationToken c);
}
