using Microsoft.Azure.Functions.Worker.Http;
using Api.Models;

namespace Api.Handlers.Films;

public interface IPostFilmCreateHandler
{
    Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        FilmCreateCommand command,
        CancellationToken cancellationToken);
}