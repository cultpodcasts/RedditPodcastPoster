using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;
using RedditPodcastPoster.Subjects.Models;

namespace RedditPodcastPoster.Subjects;

public class SubjectService(
    ISubjectsProvider subjectRepository,
    ILogger<SubjectService> logger
) : ISubjectService
{
    public async Task<Subject?> Match(Subject subject)
    {
        var subjects = await subjectRepository.GetAll().ToListAsync();
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
                logger.LogError(message);
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

        var subjects = await subjectRepository.GetAll().ToListAsync();

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
        string[]? ignoredAssociatedSubjects = null,
        string[]? ignoredSubjects = null)
    {
        ignoredAssociatedSubjects = ignoredAssociatedSubjects?.Select(x => x.ToLowerInvariant()).ToArray();
        ignoredSubjects = ignoredSubjects?.Select(x => x.ToLowerInvariant()).ToArray();

        var subjects = await subjectRepository.GetAll().ToListAsync();
        var matches = subjects
            .Select(subject => new SubjectMatch(subject,
                Matches(episode, subject, false, ignoredAssociatedSubjects, ignoredSubjects)))
            .Where(x => x.MatchResults.Any());
        if (!matches.Any())
        {
            matches = subjects
                .Select(subject => new SubjectMatch(subject,
                    Matches(episode, subject, true, ignoredAssociatedSubjects, ignoredSubjects)))
                .Where(x => x.MatchResults.Any());
        }

        return matches;
    }

    private MatchResult[] Matches(
        Episode episode,
        Subject subject,
        bool withDescription,
        string[]? ignoredAssociatedSubjects = null,
        string[]? ignoredSubjects = null)
    {
        var matches = new List<MatchResult>();
        var subjectTerm = subject.GetSubjectTerms();
        if (ignoredSubjects == null ||
            !ignoredSubjects.Contains(subjectTerm
                .SingleOrDefault(x => x.SubjectTermType == SubjectTermType.Name)?.Term
                .ToLowerInvariant()))
        {
            foreach (var term in subjectTerm.Where(x => !string.IsNullOrWhiteSpace(x.Term)))
            {
                if (Include(ignoredAssociatedSubjects, term))
                {
                    var matchCtr = 0;
                    var match = GetMatches(term.Term, WebUtility.HtmlDecode(episode.Title));
                    if (match > 0)
                    {
                        matchCtr += match;
                    }

                    if (withDescription)
                    {
                        var descMatch = GetMatches(term.Term, WebUtility.HtmlDecode(episode.Description));
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
        }

        return matches.ToArray();
    }

    private static bool Include(string[]? ignoredAssociatedSubjects, SubjectTerm term)
    {
        var include = ignoredAssociatedSubjects == null ||
                      term.SubjectTermType != SubjectTermType.AssociatedSubject ||
                      !ignoredAssociatedSubjects.Contains(term.Term.ToLowerInvariant());
        return include;
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