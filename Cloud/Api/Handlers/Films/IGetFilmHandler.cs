using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.Films;

public interface IGetFilmHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, string identifier, CancellationToken c);
}
