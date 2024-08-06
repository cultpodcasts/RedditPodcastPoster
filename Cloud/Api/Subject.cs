using System.Net;
using System.Text.Json;
using Api.Dtos;
using Api.Dtos.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using Subject = RedditPodcastPoster.Models.Subject;

namespace Api;

public class SubjectController(
    ISubjectRepository subjectRepository,
    ILogger<SubjectController> logger,
    ILogger<BaseHttpFunction> baseLogger,
    IOptions<HostingOptions> hostingOptions
) : BaseHttpFunction(hostingOptions, baseLogger)
{
    [Function("SubjectGet")]
    public Task<HttpResponseData> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "subject/{subjectName}")]
        HttpRequestData req,
        string subjectName,
        FunctionContext executionContext,
        CancellationToken ct
    )
    {
        return HandleRequest(req, ["curate"], subjectName, Get, Unauthorised, ct);
    }

    private async Task<HttpResponseData> Get(HttpRequestData req, string subjectName, CancellationToken c)
    {
        try
        {
            var subject = await subjectRepository.GetBy(x => x.Name == subjectName);
            if (subject == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var dto = subject.ToDto();
            var success = await req.CreateResponse(HttpStatusCode.OK)
                .WithJsonBody(dto, c);
            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Get)}: Failed to get subject.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve subject"), c);
        return failure;
    }

    [Function("SubjectPost")]
    public Task<HttpResponseData> Post(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "subject/{subjectId:guid}")]
        HttpRequestData req,
        Guid subjectId,
        FunctionContext executionContext,
        [FromBody] Dtos.Subject subjectChangeRequest,
        CancellationToken ct
    )
    {
        //return Task.FromResult(req.CreateResponse(HttpStatusCode.Accepted));
        return HandleRequest(req, ["curate"], new SubjectChangeRequestWrapper(subjectId, subjectChangeRequest), Post,
            Unauthorised, ct);
    }

    private async Task<HttpResponseData> Post(HttpRequestData req,
        SubjectChangeRequestWrapper subjectChangeRequestWrapper, CancellationToken c)
    {
        try
        {
            logger.LogInformation(
                $"{nameof(Post)} Subject Change Request: episode-id: '{subjectChangeRequestWrapper.SubjectId}'. {JsonSerializer.Serialize(subjectChangeRequestWrapper.Subject)}");
            var subject = await subjectRepository.GetBy(x => x.Id == subjectChangeRequestWrapper.SubjectId);
            if (subject == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            logger.LogInformation(
                $"{nameof(Post)} Updating subject-id '{subjectChangeRequestWrapper.SubjectId}'. Original-episode: {JsonSerializer.Serialize(subject)}");

            UpdateSubject(subject, subjectChangeRequestWrapper.Subject);
            await subjectRepository.Save(subject);
            return req.CreateResponse(HttpStatusCode.Accepted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(Get)}: Failed to update subject.");
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to update subject"), c);
        return failure;
    }

    private void UpdateSubject(Subject subject, Dtos.Subject change)
    {
        if (change.Aliases != null)
        {
            if (!change.Aliases.Any())
            {
                subject.Aliases = null;
            }
            else
            {
                subject.Aliases = change.Aliases;
            }
        }

        if (change.AssociatedSubjects != null)
        {
            if (!change.AssociatedSubjects.Any())
            {
                subject.AssociatedSubjects = null;
            }
            else
            {
                subject.AssociatedSubjects = change.AssociatedSubjects;
            }
        }

        if (change.EnrichmentHashTags != null)
        {
            if (!change.EnrichmentHashTags.Any())
            {
                subject.EnrichmentHashTags = null;
            }
            else
            {
                subject.EnrichmentHashTags = change.EnrichmentHashTags;
            }
        }

        if (change.HashTag != null)
        {
            if (change.HashTag == string.Empty)
            {
                subject.HashTag = null;
            }
            else
            {
                subject.HashTag = change.HashTag.Trim();
            }
        }

        if (change.RedditFlairTemplateId != null)
        {
            if (change.RedditFlairTemplateId == Guid.Empty)
            {
                subject.RedditFlairTemplateId = null;
            }
            else
            {
                subject.RedditFlairTemplateId = change.RedditFlairTemplateId;
            }
        }

        if (change.RedditFlareText != null)
        {
            if (change.RedditFlareText == string.Empty)
            {
                subject.RedditFlareText = null;
            }
            else
            {
                subject.RedditFlareText = change.RedditFlareText.Trim();
            }
        }

        if (change.SubjectType != null)
        {
            if (change.SubjectType != SubjectType.Unset)
            {
                subject.SubjectType = change.SubjectType;
            }
            else
            {
                subject.SubjectType = null;
            }
        }
    }
}