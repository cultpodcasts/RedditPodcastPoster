using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Films;

namespace Api.Handlers.Films;

/// <summary>
/// POST film (RPP #1413). A command: 202 with the film's id and name (the client GETs the film), never the film.
/// Ambiguous identity and cancel are 200 command outcomes with nothing written, as Move to film.
/// </summary>
public class PostFilmCreateHandler(
    IFilmCreateService filmCreateService,
    ILogger<PostFilmCreateHandler> logger) : IPostFilmCreateHandler
{
    public async Task<HttpResponseData> Handle(
        IHandlerContext ctx,
        FilmCreateCommand command,
        CancellationToken cancellationToken)
    {
        var result = await filmCreateService.CreateAsync(command, cancellationToken);
        return result.Status switch
        {
            FilmCreateStatus.Accepted => await ctx.Accepted(result.ToDto(), cancellationToken),
            FilmCreateStatus.IdentityDecisionRequired => await ctx.Ok(result.ToDto(), cancellationToken),
            FilmCreateStatus.Cancelled => await ctx.Ok(result.ToDto(), cancellationToken),
            FilmCreateStatus.Conflict => await ctx.Conflict(result.ToDto(), cancellationToken),
            FilmCreateStatus.IdentityNotFound =>
                await ctx.NotFound(ApiErrorResponse.Failure(result.Error ?? "No film found"), cancellationToken),
            FilmCreateStatus.BadRequest =>
                await ctx.BadRequest(ApiErrorResponse.Failure(result.Error ?? "Invalid request"), cancellationToken),
            // ToDto so failureIndexing reaches the client.
            FilmCreateStatus.Failed => await ctx.InternalError(result.ToDto(), cancellationToken),
            _ => await LogAndFail(ctx, cancellationToken)
        };
    }

    private async Task<HttpResponseData> LogAndFail(IHandlerContext ctx, CancellationToken cancellationToken)
    {
        logger.LogError("Film create failed with unexpected status.");
        return await ctx.InternalError(ApiErrorResponse.Failure("Unable to create film"), cancellationToken);
    }
}