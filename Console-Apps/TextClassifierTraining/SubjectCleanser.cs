﻿using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Subjects;

namespace TextClassifierTraining;

public class SubjectCleanser : ISubjectCleanser
{
    private static readonly Regex BracketedTerm = new(@"\((?'bracketedterm'.*)\)", RegexOptions.Compiled);
    private readonly ILogger<SubjectCleanser> _logger;
    private readonly ISubjectService _subjectService;

    public SubjectCleanser(
        ISubjectService subjectService,
        ILogger<SubjectCleanser> logger)
    {
        _subjectService = subjectService;
        _logger = logger;
    }

    public async Task<List<string>> CleanSubjects(List<string> subjects)
    {
        var splitSubjects = new List<string>();
        foreach (var subject in subjects.Select(x => x.ToLower().Trim()).Distinct())
        {
            List<string> components = new();

            if (subject.Contains("/"))
            {
                components.AddRange(subject.Split("/").Select(x => x.Trim()));
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

        var cleansedSubjects = new List<string>();
        foreach (var subject in splitSubjects)
        {
            var cleansed = subject.Replace("\u0026", "and");
            cleansed = cleansed.Replace("!", string.Empty);
            cleansed = cleansed.Replace("- ", string.Empty);
            if (await _subjectService.Match(cleansed) == null)
            {
                cleansedSubjects.Add(cleansed);
            }
        }

        return cleansedSubjects;
    }
}