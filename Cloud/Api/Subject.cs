using System.Net;
using System.Text.Json;
using Api.Dtos;
using Api.Dtos.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit.Inputs.Flair;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Reddit;
using RedditPodcastPoster.Subjects;
using Subject = RedditPodcastPoster.Models.Subject;

namespace Api;

public class SubjectController(
    ISubjectRepository subjectRepository,
    ISubjectService subjectService,
    IContentPublisher contentPublisher,
    IAdminRedditClient redditClient,
    IOptions<SubredditSettings> subredditSettings,
    ILogger<SubjectController> logger,
    ILogger<BaseHttpFunction> baseLogger,
    IOptions<HostingOptions> hostingOptions
) : BaseHttpFunction(hostingOptions, baseLogger)
{
    private readonly SubredditSettings _subredditSettings = subredditSettings.Value;

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
        return HandleRequest(req, ["curate"], new SubjectChangeRequestWrapper(subjectId, subjectChangeRequest), Post,
            Unauthorised, ct);
    }

    [Function("SubjectPut")]
    public Task<HttpResponseData> Put(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "subject")]
        HttpRequestData req,
        FunctionContext executionContext,
        [FromBody] Dtos.Subject subjectChangeRequest,
        CancellationToken ct
    )
    {
        return HandleRequest(req, ["curate"], subjectChangeRequest, Put, Unauthorised, ct);
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

            await UpdateSubject(subject, subjectChangeRequestWrapper.Subject);
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

    private async Task<HttpResponseData> Put(HttpRequestData req, Dtos.Subject subject, CancellationToken ct)
    {
        logger.LogInformation($"{nameof(Put)}: received subject: {JsonSerializer.Serialize(subject)}");
        if (string.IsNullOrWhiteSpace(subject.Name))
        {
            logger.LogWarning("Missing name.");
            return await req.CreateResponse(HttpStatusCode.BadRequest).WithJsonBody(new {message = "Missing name"}, ct);
        }

        var entity = new Subject(subject.Name);
        await UpdateSubject(entity, subject);
        var matchingSubject = await subjectService.Match(entity);
        if (matchingSubject != null)
        {
            return await req.CreateResponse(HttpStatusCode.Conflict)
                .WithJsonBody(new {conflict = matchingSubject.Name}, ct);
        }

        await subjectRepository.Save(entity);
        await contentPublisher.PublishSubjects();
        logger.LogInformation($"Created subject '{subject.Name}' with subject-id '{subject.Id}'.");

        return await req.CreateResponse(HttpStatusCode.Accepted).WithJsonBody(entity.ToDto(), ct);
    }

    private async Task UpdateSubject(Subject subject, Dtos.Subject change)
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
                await UseFlair(subject, change.RedditFlairTemplateId.Value);
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

    public async Task UseFlair(Subject subject, Guid flairId)
    {
        var subredditFlairs = redditClient.Client
            .Subreddit(_subredditSettings.SubredditName)
            .Flairs
            .GetLinkFlairV2();
        var flair = subredditFlairs.SingleOrDefault(x => x.Id == flairId.ToString());
        if (flair == null)
        {
            throw new InvalidOperationException($"Unable to find subreddit-flair with id '{flairId}'.");
        }

        if (!flair.TextEditable)
        {
            var subjectsUsingFlair =
                await subjectRepository.GetAllBy(x => x.RedditFlairTemplateId == flairId).ToListAsync();
            if (subjectsUsingFlair.Count == 1)
            {
                var subjectUsingFlair = subjectsUsingFlair.Single();
                subjectUsingFlair.RedditFlareText = flair.Text;
                await subjectRepository.Save(subjectUsingFlair);
                logger.LogInformation(
                    $"Adjusted subject '{subjectUsingFlair.Name}' with id '{subjectUsingFlair.Id}' to have  {nameof(subjectUsingFlair.RedditFlareText)}='{flair.Text}'.");
            }

            flair.TextEditable = true;
            var updateResult = await redditClient.Client
                .Subreddit(_subredditSettings.SubredditName)
                .Flairs
                .UpdateLinkFlairTemplateV2Async(new FlairTemplateV2Input
                {
                    background_color = flair.BackgroundColor,
                    flair_template_id = flair.Id,
                    flair_type = flair.Type,
                    text = flair.Text,
                    text_color = flair.TextColor,
                    text_editable = true
                });
            if (!updateResult.TextEditable)
            {
                logger.LogError($"Error updating flare '{flair.Text}' with id '{flair.Id}'.");
            }
        }

        subject.RedditFlairTemplateId = flairId;
        if (!string.IsNullOrWhiteSpace(subject.RedditFlareText))
        {
            subject.RedditFlareText = subject.Name;
        }
    }
}