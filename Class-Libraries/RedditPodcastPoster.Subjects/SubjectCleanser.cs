using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace RedditPodcastPoster.Subjects;

public class SubjectCleanser(
    ISubjectService subjectService,
    ILogger<SubjectCleanser> logger)
    : ISubjectCleanser
{
    private static readonly Regex BracketedTerm = new(@"\((?'bracketedterm'.*)\)", RegexOptions.Compiled);

    public async Task<(bool, List<string>)> CleanSubjects(List<string> subjects)
    {
        var splitSubjects = new List<string>();
        foreach (var subject in subjects.Select(x => x.ToLower().Trim()).Distinct())
        {
            List<string> components = new();

            if (subject.Contains("/"))
            {
                components.AddRange(subject.Split("/").Select(x => x.Trim()));
            }
            else if (subject.Contains(","))
            {
                components.AddRange(subject.Split(",").Select(x => x.Trim()));
            }
            else if (subject.Contains("\\"))
            {
                components.AddRange(subject.Split("\\").Select(x => x.Trim()));
            }
            else if (subject.Contains("&"))
            {
                components.AddRange(subject.Split("&").Select(x => x.Trim()));
            }
            else
            {
                components.Add(subject);
            }

            foreach (var component in components)
            {
                var match = BracketedTerm.Match(component);
                if (match.Success)
                {
                    var bracketedTerm = match.Groups["bracketedterm"].Value;
                    splitSubjects.Add(bracketedTerm.Trim());
                    var cleansed = BracketedTerm.Replace(component, string.Empty).Trim();
                    splitSubjects.Add(cleansed);
                }
                else
                {
                    splitSubjects.Add(component);
                }
            }
        }

        var unmatchedSubject = false;
        var cleansedSubjects = new List<string>();
        foreach (var subject in splitSubjects)
        {
            var cleansed = subject.Replace("\u0026", "and");
            cleansed = cleansed.Replace("!", string.Empty);
            cleansed = cleansed.Replace("- ", string.Empty);
            var matchedSubject = await subjectService.Match(cleansed);
            if (matchedSubject == null)
            {
                unmatchedSubject = true;
//                _logger.LogError($"Unmatched subject '{cleansed}'.");
            }
            else
            {
                cleansedSubjects.Add(matchedSubject.Name);
            }
        }

        return (unmatchedSubject, cleansedSubjects.Distinct().ToList());
    }
}