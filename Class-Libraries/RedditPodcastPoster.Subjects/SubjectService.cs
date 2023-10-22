﻿using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;

namespace RedditPodcastPoster.Subjects;

public class SubjectService : ISubjectService
{
    private readonly ILogger<SubjectService> _logger;
    private readonly ICachedSubjectRepository _subjectRepository;

    public SubjectService(ICachedSubjectRepository subjectRepository, ILogger<SubjectService> logger)
    {
        _subjectRepository = subjectRepository;
        _logger = logger;
    }

    public async Task<Subject?> Match(Subject subject)
    {
        var subjects = await _subjectRepository.GetAll(Subject.PartitionKey);
        if (!subjects.Any())
        {
            return null;
        }

        if (subject.Id != Guid.Empty)
        {
            var matchedSubject = subjects.SingleOrDefault(x => x.Id == subject.Id);
            if (matchedSubject != null)
            {
                return matchedSubject;
            }
        }

        if (!string.IsNullOrWhiteSpace(subject.Name))
        {
            var matchedSubject =
                subjects.SingleOrDefault(x => x.Name.ToLowerInvariant() == subject.Name.ToLowerInvariant());
            if (matchedSubject != null)
            {
                return matchedSubject;
            }
        }

        // does subject-alias match a subject
        if (subject.Aliases != null && subject.Aliases.Any())
        {
            foreach (var subjectAlias in subject.Aliases)
            {
                if (!string.IsNullOrWhiteSpace(subjectAlias))
                {
                    var matchedSubject =
                        subjects.SingleOrDefault(x => x.Name.ToLowerInvariant() == subjectAlias.ToLowerInvariant());
                    if (matchedSubject != null)
                    {
                        return matchedSubject;
                    }
                }
            }
        }

        // does subject match a subject with aliases
        if (!string.IsNullOrWhiteSpace(subject.Name))
        {
            var matchedSubject = subjects.Where(x => x.Aliases != null).FirstOrDefault(x =>
                x.Aliases.Select(y => y.ToLowerInvariant()).Contains(subject.Name.ToLowerInvariant()));
            if (matchedSubject != null)
            {
                return matchedSubject;
            }
        }

        // does subject-alias match a subject's alias
        if (subject.Aliases != null && subject.Aliases.Any())
        {
            var matchedSubjects = new List<Subject>();
            foreach (var subjectAlias in subject.Aliases)
            {
                if (!string.IsNullOrWhiteSpace(subjectAlias))
                {
                    var matchedSubjectsForAlias = subjects.Where(x =>
                        x.Aliases != null && x.Aliases.Select(y => y.ToLowerInvariant())
                            .Contains(subjectAlias.ToLowerInvariant()));
                    matchedSubjects.AddRange(matchedSubjectsForAlias);
                }
            }

            matchedSubjects = matchedSubjects.Distinct().ToList();
            if (matchedSubjects.Count() > 1)
            {
                var message =
                    $"Subject '{subject.Name}' with id '{subject.Id}' with aliases '{string.Join(",", subject.Aliases.Select(x => $"'{x}'"))}' matches multiple subjects: {string.Join(",", matchedSubjects.Select(x => $"'{x.Name}'"))}.";
                _logger.LogError(message);
                throw new InvalidOperationException(message);
            }

            if (matchedSubjects.Count() == 1)
            {
                return matchedSubjects.First();
            }
        }

        // does subject match a subject with associatedSubjects
        if (!string.IsNullOrWhiteSpace(subject.Name))
        {
            var matchedSubject = subjects.Where(x => x.AssociatedSubjects != null).FirstOrDefault(x =>
                x.AssociatedSubjects.Select(y => y.ToLowerInvariant()).Contains(subject.Name.ToLowerInvariant()));
            if (matchedSubject != null)
            {
                return matchedSubject;
            }
        }

        return null;
    }
}