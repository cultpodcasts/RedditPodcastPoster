using System.Net;
using System.Text.Json;
using Api.Dtos;
using Api.Dtos.Extensions;
using Api.Extensions;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reddit.Inputs.Flair;
using RedditPodcastPoster.Auth0;
using RedditPodcastPoster.ContentPublisher;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Reddit;
using RedditPodcastPoster.Subjects;
using RedditPodcastPoster.Subjects.Factories;
using Subject = RedditPodcastPoster.Models.Subject;

namespace Api.Handlers;

public class SubjectHandler(
    ISubjectRepository subjectRepository,
    ISubjectService subjectService,
    ISubjectFactory subjectFactory,
    IContentPublisher contentPublisher,
    IAdminRedditClient redditClient,
    IOptions<SubredditSettings> subredditSettings,
    ILogger<SubjectHandler> logger) : ISubjectHandler
{
    private readonly SubredditSettings _subredditSettings = subredditSettings.Value;

    public async Task<HttpResponseData> Get(HttpRequestData req, string subjectName, ClientPrincipal? _,
        CancellationToken c)
    {
        try
        {
            logger.LogInformation("Get subject '{subjectName}'.", subjectName);
            var subject = await subjectRepository.GetBy(x => x.Name == subjectName);
            if (subject == null)
            {
                logger.LogInformation("Could not find subject with name '{subjectName}'.", subjectName);
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var dto = subject.ToDto();
            var success = await req.CreateResponse(HttpStatusCode.OK)
                .WithJsonBody(dto, c);
            return success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to get subject.", nameof(Get));
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to retrieve subject"), c);
        return failure;
    }

    public async Task<HttpResponseData> Post(HttpRequestData req,
        SubjectChangeRequestWrapper subjectChangeRequestWrapper, ClientPrincipal? _, CancellationToken c)
    {
        try
        {
            logger.LogInformation(
                "{method} Subject Change Request: episode-id: '{SubjectId}'. {subjectJson}",
                nameof(Post), subjectChangeRequestWrapper.SubjectId,
                JsonSerializer.Serialize(subjectChangeRequestWrapper.Subject));
            var subject = await subjectRepository.GetBy(x => x.Id == subjectChangeRequestWrapper.SubjectId);
            if (subject == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            logger.LogInformation(
                "{method} Updating subject-id '{SubjectId}'. Original-episode: {subject}",
                nameof(Post), subjectChangeRequestWrapper.SubjectId, JsonSerializer.Serialize(subject));

            await UpdateSubject(subject, subjectChangeRequestWrapper.Subject);
            await subjectRepository.Save(subject);
            return req.CreateResponse(HttpStatusCode.Accepted);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{method}: Failed to update subject.", nameof(Get));
        }

        var failure = await req.CreateResponse(HttpStatusCode.InternalServerError)
            .WithJsonBody(SubmitUrlResponse.Failure("Unable to update subject"), c);
        return failure;
    }

    public async Task<HttpResponseData> Put(HttpRequestData req, Dtos.Subject subject, ClientPrincipal? _,
        CancellationToken ct)
    {
        logger.LogInformation("{method}: received subject: {subject}",
            nameof(Put), JsonSerializer.Serialize(subject));
        if (string.IsNullOrWhiteSpace(subject.Name))
        {
            logger.LogWarning("Missing subject-name.");
            return await req.CreateResponse(HttpStatusCode.BadRequest)
                .WithJsonBody(new { message = "Missing subject-name" }, ct);
        }

        var entity = await subjectFactory.Create(subject.Name);
        await UpdateSubject(entity, subject);
        var matchingSubject = await subjectService.Match(entity);
        if (matchingSubject != null)
        {
            return await req.CreateResponse(HttpStatusCode.Conflict)
                .WithJsonBody(new { conflict = matchingSubject.Name }, ct);
        }

        await subjectRepository.Save(entity);
        await contentPublisher.PublishSubjects();
        logger.LogInformation("Created subject '{subjectName}' with subject-id '{subjectId}'.",
            subject.Name, subject.Id);

        return await req.CreateResponse(HttpStatusCode.Accepted).WithJsonBody(entity.ToDto(), ct);
    }

    private async Task UpdateSubject(Subject subject, Dtos.Subject change)
    {
        if (change.Aliases != null)
        {
            subject.Aliases = !change.Aliases.Any() ? null : change.Aliases.Select(x => x.Trim()).ToArray();
        }

        if (change.AssociatedSubjects != null)
        {
            subject.AssociatedSubjects = !change.AssociatedSubjects.Any()
                ? null
                : change.AssociatedSubjects.Select(x => x.Trim()).ToArray();
        }

        if (change.EnrichmentHashTags != null)
        {
            subject.EnrichmentHashTags = !change.EnrichmentHashTags.Any()
                ? null
                : change.EnrichmentHashTags.Select(x => x.Trim()).ToArray();
        }

        if (change.HashTag != null)
        {
            subject.HashTag = change.HashTag == string.Empty ? null : change.HashTag.Trim();
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
            subject.RedditFlareText = change.RedditFlareText == string.Empty ? null : change.RedditFlareText.Trim();
        }

        if (change.SubjectType != null)
        {
            subject.SubjectType = change.SubjectType != SubjectType.Unset ? change.SubjectType : null;
        }
    }

    private async Task UseFlair(Subject subject, Guid flairId)
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
                    "Adjusted subject '{subjectUsingFlairName}' with id '{subjectUsingFlairId}' to have  {nameofRedditFlareText}='{flairText}'.",
                    subjectUsingFlair.Name, subjectUsingFlair.Id, nameof(subjectUsingFlair.RedditFlareText),
                    flair.Text);
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
                logger.LogError("Error updating flare '{flairText}' with id '{flairId}'.", flair.Text, flair.Id);
            }
        }

        subject.RedditFlairTemplateId = flairId;
        if (!string.IsNullOrWhiteSpace(subject.RedditFlareText))
        {
            subject.RedditFlareText = subject.Name;
        }
    }
}