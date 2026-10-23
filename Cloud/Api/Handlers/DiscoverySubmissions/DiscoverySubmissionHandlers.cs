using Api.Dtos;
using System.Net;
using Api.Dtos.Extensions;
using Api.Models;
using Api.Services.Discovery;
using Microsoft.Azure.Functions.Worker.Http;

namespace Api.Handlers.DiscoverySubmissions;

public interface IGetDiscoverySubmissionsHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, CancellationToken c);
}

public interface IGetDiscoverySubmissionHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx, Guid submissionId, CancellationToken c);
}

public interface IPutDiscoverySubmissionReviewHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx,
        DiscoverySubmissionReviewRoute<DiscoverySubmissionReviewRequest> model, CancellationToken c);
}

public interface IPostDiscoverySubmissionApplyHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx,
        DiscoverySubmissionReviewRoute<DiscoverySubmissionApplyRequest> model, CancellationToken c);
}

public interface IPostDiscoverySubmissionRetryHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx,
        DiscoverySubmissionReviewRoute<DiscoverySubmissionRetryRequest> model, CancellationToken c);
}

public interface IPostDiscoverySubmissionReleaseHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx,
        DiscoverySubmissionReviewRoute<DiscoverySubmissionReleaseRequest> model, CancellationToken c);
}

public interface IPostDiscoveryReportReleaseHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx,
        DiscoverySubmissionReviewRoute<DiscoveryReportReleaseRequest> model, CancellationToken c);
}

public interface IPostDiscoverySubmissionStateHandler
{
    Task<HttpResponseData> Handle(IHandlerContext ctx,
        DiscoverySubmissionReviewRoute<DiscoverySubmissionSetStateRequest> model, CancellationToken c);
}

internal static class SupervisionCallerFactory
{
    /// <summary>
    /// A <c>curate:supervised</c> caller never gets supervisor rights (even with <c>supervise</c>/<c>admin</c>),
    /// because a supervised curator must never be able to promote reports to Processed.
    /// </summary>
    public static SupervisionCaller? From(IHandlerContext ctx)
    {
        if (string.IsNullOrWhiteSpace(ctx.Subject))
        {
            return null;
        }

        var supervised = ctx.HasScope(DiscoveryPermissions.CurateSupervised);
        return new SupervisionCaller(ctx.Subject,
            !supervised && ctx.HasScope(DiscoveryPermissions.Admin),
            !supervised && ctx.HasScope(DiscoveryPermissions.Supervise));
    }

    public static Task<HttpResponseData> Forbidden(IHandlerContext ctx, CancellationToken c) =>
        ctx.Json(HttpStatusCode.Forbidden, new DiscoveryErrorResponse { Message = "Forbidden" }, c);

    public static Task<HttpResponseData> MissingSubject(IHandlerContext ctx, CancellationToken c) =>
        ctx.BadRequest(new DiscoveryErrorResponse { Message = "Missing subject." }, c);

    public static Task<HttpResponseData> Query<T>(IHandlerContext ctx, DiscoverySubmissionQueryResult<T> result,
        Func<T, object> toDto, CancellationToken c) => result.Status switch
    {
        DiscoverySubmissionQueryStatus.Ok => ctx.Ok(toDto(result.Value!), c),
        DiscoverySubmissionQueryStatus.NotFound => ctx.NotFound(new DiscoveryErrorResponse { Message = "Not found" }, c),
        DiscoverySubmissionQueryStatus.Forbidden => Forbidden(ctx, c),
        _ => ctx.InternalError(new DiscoveryErrorResponse { Message = "Failure" }, c)
    };

    public static Task<HttpResponseData> Command(IHandlerContext ctx, Guid submissionId,
        DiscoverySubmissionCommandResult result, HttpStatusCode okStatus, CancellationToken c) => result.Status switch
    {
        DiscoverySubmissionCommandStatus.Ok => ctx.Json(okStatus, result.ToDto(submissionId), c),
        DiscoverySubmissionCommandStatus.NotFound => ctx.NotFound(new DiscoveryErrorResponse { Message = "Not found" }, c),
        DiscoverySubmissionCommandStatus.Forbidden => Forbidden(ctx, c),
        DiscoverySubmissionCommandStatus.Conflict => ctx.Conflict(result.ToDto(submissionId), c),
        DiscoverySubmissionCommandStatus.Invalid => ctx.BadRequest(result.ToDto(submissionId), c),
        DiscoverySubmissionCommandStatus.LanguageDecisionRequired => ctx.Ok(result.ToDto(submissionId), c),
        _ => ctx.InternalError(result.ToDto(submissionId), c)
    };
}

public class GetDiscoverySubmissionsHandler(IDiscoverySubmissionReviewService service)
    : IGetDiscoverySubmissionsHandler
{
    public async Task<HttpResponseData> Handle(IHandlerContext ctx, CancellationToken c)
    {
        var caller = SupervisionCallerFactory.From(ctx);
        if (caller == null)
        {
            return await SupervisionCallerFactory.MissingSubject(ctx, c);
        }

        return await SupervisionCallerFactory.Query(ctx, await service.List(caller, c), x => x.ToDto(), c);
    }
}

public class GetDiscoverySubmissionHandler(IDiscoverySubmissionReviewService service)
    : IGetDiscoverySubmissionHandler
{
    public async Task<HttpResponseData> Handle(IHandlerContext ctx, Guid submissionId, CancellationToken c)
    {
        var caller = SupervisionCallerFactory.From(ctx);
        if (caller == null)
        {
            return await SupervisionCallerFactory.MissingSubject(ctx, c);
        }

        return await SupervisionCallerFactory.Query(ctx, await service.Get(submissionId, caller, c),
            x => x.ToDto(), c);
    }
}

public class PutDiscoverySubmissionReviewHandler(IDiscoverySubmissionReviewService service)
    : IPutDiscoverySubmissionReviewHandler
{
    public async Task<HttpResponseData> Handle(IHandlerContext ctx,
        DiscoverySubmissionReviewRoute<DiscoverySubmissionReviewRequest> model, CancellationToken c)
    {
        var caller = SupervisionCallerFactory.From(ctx);
        if (caller == null)
        {
            return await SupervisionCallerFactory.MissingSubject(ctx, c);
        }

        if (!caller.CanSuperviseAny)
        {
            return await SupervisionCallerFactory.Forbidden(ctx, c);
        }

        var result = await service.SaveReview(model.SubmissionId, model.Request, caller, c);
        return await SupervisionCallerFactory.Command(ctx, model.SubmissionId, result, HttpStatusCode.Accepted, c);
    }
}

public class PostDiscoverySubmissionApplyHandler(IDiscoverySubmissionReviewService service)
    : IPostDiscoverySubmissionApplyHandler
{
    public async Task<HttpResponseData> Handle(IHandlerContext ctx,
        DiscoverySubmissionReviewRoute<DiscoverySubmissionApplyRequest> model, CancellationToken c)
    {
        var caller = SupervisionCallerFactory.From(ctx);
        if (caller == null)
        {
            return await SupervisionCallerFactory.MissingSubject(ctx, c);
        }

        if (!caller.CanSuperviseAny)
        {
            return await SupervisionCallerFactory.Forbidden(ctx, c);
        }

        var result = await service.Apply(model.SubmissionId, model.Request, caller, c);
        return await SupervisionCallerFactory.Command(ctx, model.SubmissionId, result, HttpStatusCode.OK, c);
    }
}

public class PostDiscoverySubmissionRetryHandler(IDiscoverySubmissionReviewService service)
    : IPostDiscoverySubmissionRetryHandler
{
    public async Task<HttpResponseData> Handle(IHandlerContext ctx,
        DiscoverySubmissionReviewRoute<DiscoverySubmissionRetryRequest> model, CancellationToken c)
    {
        var caller = SupervisionCallerFactory.From(ctx);
        if (caller == null)
        {
            return await SupervisionCallerFactory.MissingSubject(ctx, c);
        }

        if (!caller.CanSuperviseAny)
        {
            return await SupervisionCallerFactory.Forbidden(ctx, c);
        }

        var result = await service.Retry(model.SubmissionId, model.Request, caller, c);
        return await SupervisionCallerFactory.Command(ctx, model.SubmissionId, result, HttpStatusCode.OK, c);
    }
}

public class PostDiscoverySubmissionReleaseHandler(IDiscoverySubmissionReviewService service)
    : IPostDiscoverySubmissionReleaseHandler
{
    public async Task<HttpResponseData> Handle(IHandlerContext ctx,
        DiscoverySubmissionReviewRoute<DiscoverySubmissionReleaseRequest> model, CancellationToken c)
    {
        var caller = SupervisionCallerFactory.From(ctx);
        if (caller == null)
        {
            return await SupervisionCallerFactory.MissingSubject(ctx, c);
        }

        if (!caller.CanSuperviseAny)
        {
            return await SupervisionCallerFactory.Forbidden(ctx, c);
        }

        var result = await service.Release(model.SubmissionId, model.Request, caller, c);
        return await SupervisionCallerFactory.Command(ctx, model.SubmissionId, result, HttpStatusCode.OK, c);
    }
}

/// <summary>Admin-only release of a report stuck in PendingSupervisor with no live submission.</summary>
public class PostDiscoveryReportReleaseHandler(IDiscoverySubmissionReviewService service)
    : IPostDiscoveryReportReleaseHandler
{
    public async Task<HttpResponseData> Handle(IHandlerContext ctx,
        DiscoverySubmissionReviewRoute<DiscoveryReportReleaseRequest> model, CancellationToken c)
    {
        var caller = SupervisionCallerFactory.From(ctx);
        if (caller == null)
        {
            return await SupervisionCallerFactory.MissingSubject(ctx, c);
        }

        if (!caller.IsAdmin)
        {
            return await SupervisionCallerFactory.Forbidden(ctx, c);
        }

        var result = await service.ReleaseStuckReport(model.SubmissionId, model.Request, caller, c);
        var body = new Dtos.DiscoveryReportReleaseResponse
        {
            DocumentId = model.SubmissionId,
            Status = result.Status == DiscoverySubmissionCommandStatus.Ok ? "Released" : result.Status.ToString(),
            Message = result.Message,
            SubmissionIds = result.InvalidIds?.ToArray()
        };
        return result.Status switch
        {
            DiscoverySubmissionCommandStatus.Ok => await ctx.Ok(body, c),
            DiscoverySubmissionCommandStatus.NotFound => await ctx.NotFound(body, c),
            DiscoverySubmissionCommandStatus.Forbidden => await SupervisionCallerFactory.Forbidden(ctx, c),
            DiscoverySubmissionCommandStatus.Conflict => await ctx.Conflict(body, c),
            _ => await ctx.InternalError(body, c)
        };
    }
}

/// <summary>Admin-only manual status override.</summary>
public class PostDiscoverySubmissionStateHandler(IDiscoverySubmissionReviewService service)
    : IPostDiscoverySubmissionStateHandler
{
    public async Task<HttpResponseData> Handle(IHandlerContext ctx,
        DiscoverySubmissionReviewRoute<DiscoverySubmissionSetStateRequest> model, CancellationToken c)
    {
        var caller = SupervisionCallerFactory.From(ctx);
        if (caller == null)
        {
            return await SupervisionCallerFactory.MissingSubject(ctx, c);
        }

        if (!caller.IsAdmin)
        {
            return await SupervisionCallerFactory.Forbidden(ctx, c);
        }

        var result = await service.SetState(model.SubmissionId, model.Request, caller, c);
        return await SupervisionCallerFactory.Command(ctx, model.SubmissionId, result, HttpStatusCode.OK, c);
    }
}
