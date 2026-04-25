using Microsoft.Extensions.Logging;
using Reddit.Controllers;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Reddit;

public class FlareManager(
    ISubjectRepository subjectRepository,
    ILogger<FlareManager> logger
) : IFlareManager
{
    public async Task<FlareState> SetFlare(string[] subjectNames, Post post)
    {
        var flareState = FlareState.Unknown;
        if (subjectNames.Any())
        {
            var normalizedSubjectNames = subjectNames
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var subjects = await subjectRepository
                .GetAllBy(x => Enumerable.Contains(normalizedSubjectNames, x.Name))
                .ToArrayAsync();
            var redditSubjects = subjects
                .OrderBy(x => Array.IndexOf(normalizedSubjectNames, x.Name))
                .Where(x => x.RedditFlairTemplateId != null);
            var subject =
                redditSubjects.FirstOrDefault(x => x.SubjectType is null or SubjectType.Canonical) ??
                redditSubjects.FirstOrDefault();
            if (subject != null)
            {
                var flairTemplateId = subject.RedditFlairTemplateId.ToString();
                post.SetFlair(subject.RedditFlareText ?? subject.Name, flairTemplateId);
                logger.LogInformation(
                    "Set post-flair for episode-id '{episodeId}' flair with flair-text '{subject}' and flair-id '{flairTemplateId}'. Subject-names: [{subjectNames}]. Reddit-subjects: [{redditSubjects}]",
                    post.Id, subject.RedditFlareText ?? subject.Name, flairTemplateId, string.Join(',', subjectNames.Select(x => $"'{x}'")),
                    string.Join(',', redditSubjects.Select(x => $"{{name: '{x.Name}', subjectType: {x.SubjectType}}}")));
                flareState = FlareState.Set;
            }
            else
            {
                flareState = FlareState.NoFlareId;
                post.SetFlair(string.Empty);
            }
        }
        else
        {
            flareState = FlareState.Unset;
            post.SetFlair(string.Empty);
        }

        return flareState;
    }
}