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
            var subjects = await System.Linq.AsyncEnumerable.ToArrayAsync(subjectRepository.GetByNames(subjectNames), System.Threading.CancellationToken.None);
            var redditSubjects = subjects.Where(x => x.RedditFlairTemplateId != null);
            var subject =
                redditSubjects.FirstOrDefault(x => x.SubjectType is null or SubjectType.Canonical) ??
                redditSubjects.FirstOrDefault();
            if (subject != null)
            {
                var flairTemplateId = subject.RedditFlairTemplateId.ToString();
                post.SetFlair(subject.RedditFlareText ?? subject.Name, flairTemplateId);
                logger.LogInformation(
                    $"Set flair-text '{subject.RedditFlareText ?? subject.Name}' and flair-id '{flairTemplateId}'.");
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