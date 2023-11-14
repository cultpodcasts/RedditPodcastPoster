using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

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
                x.Aliases!.Select(y => y).Contains(subject.Name.ToLowerInvariant()));
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
                    var subjectLower = subjectAlias.ToLowerInvariant();
                    var matchedSubjectsForAlias = subjects.Where(x =>
                    {
                        return x.Aliases != null && x.Aliases.Select(y => y.ToLowerInvariant())
                            .Contains(subjectLower);
                    });
                    if (matchedSubjectsForAlias.Any())
                    {
                        matchedSubjects.AddRange(matchedSubjectsForAlias);
                    }
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
            var matchedSubject = subjects
                .Where(x => x.AssociatedSubjects != null)
                .FirstOrDefault(x =>
                    x.AssociatedSubjects!.Select(y => y.ToLowerInvariant())
                        .Contains(subject.Name.ToLowerInvariant()));
            if (matchedSubject != null)
            {
                return matchedSubject;
            }
        }

        return null;
    }

    public async Task<Subject?> Match(string subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentNullException(nameof(subject));
        }

        var subjects = await _subjectRepository.GetAll(Subject.PartitionKey);

        var matchedSubject =
            subjects.SingleOrDefault(x => x.Name.ToLowerInvariant() == subject.ToLowerInvariant());
        if (matchedSubject != null)
        {
            return matchedSubject;
        }

        matchedSubject = subjects.Where(x => x.Aliases != null).FirstOrDefault(x =>
            x.Aliases!.Select(y => y.ToLowerInvariant()).Contains(subject.ToLowerInvariant()));
        if (matchedSubject != null)
        {
            return matchedSubject;
        }

        matchedSubject = subjects.Where(x => x.AssociatedSubjects != null).FirstOrDefault(x =>
            x.AssociatedSubjects!.Select(y => y.ToLowerInvariant()).Contains(subject.ToLowerInvariant()));
        if (matchedSubject != null)
        {
            return matchedSubject;
        }

        return null;
    }

    public async Task<IEnumerable<SubjectMatch>> Match(
        Episode episode,
        bool withDescription,
        string[]? ignoredTerms = null)
    {
        if (ignoredTerms != null)
        {
            ignoredTerms = ignoredTerms.Select(x => x.ToLowerInvariant()).ToArray();
        }

        var subjects = await _subjectRepository.GetAll(Subject.PartitionKey);
        var matches = subjects
            .Select(subject => new SubjectMatch(subject, Matches(episode, subject, false, ignoredTerms)))
            .Where(x => x.MatchResults.Any());
        if (!matches.Any())
        {
            matches = subjects
                .Select(subject => new SubjectMatch(subject, Matches(episode, subject, true, ignoredTerms)))
                .Where(x => x.MatchResults.Any());
        }

        return matches;
    }

    private MatchResult[] Matches(Episode episode, Subject subject, bool withDescription, string[]? ignoredTerms = null)
    {
        var matches = new List<MatchResult>();
        var subjectTerm = subject.GetSubjectTerms();
        foreach (var term in subjectTerm.Where(x => !string.IsNullOrWhiteSpace(x.Term)))
        {
            if (ignoredTerms == null ||
                term.SubjectTermType != SubjectTermType.AssociatedSubject ||
                !ignoredTerms.Contains(term.Term.ToLowerInvariant()))
            {
                var matchCtr = 0;
                var match = GetMatches(term.Term, episode.Title);
                if (match > 0)
                {
                    matchCtr += match;
                }

                if (withDescription)
                {
                    var descMatch = GetMatches(term.Term, episode.Description);
                    if (descMatch > 0)
                    {
                        matchCtr += descMatch;
                    }
                }

                if (matchCtr > 0)
                {
                    matches.Add(new MatchResult(term.Term, matchCtr));
                }
            }
        }

        return matches.ToArray();
    }

    private int GetMatches(string term, string sentence)
    {
        sentence = sentence
            .Replace("’", "'")
            .Replace("´", "'");
        var pattern = @"\b" + Regex.Escape(term) + @"\b";
        var re = new Regex(pattern, RegexOptions.IgnoreCase);

        return re.Matches(sentence).Count;
    }
}