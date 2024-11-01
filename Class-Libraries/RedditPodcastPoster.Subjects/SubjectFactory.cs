using Microsoft.Extensions.Logging;
using RedditPodcastPoster.Models;
using RedditPodcastPoster.Persistence.Abstractions;

namespace RedditPodcastPoster.Subjects;

public class SubjectFactory(
    IPodcastRepository podcastRepository,
    ILogger<SubjectFactory> logger) : ISubjectFactory
{
    private static string[]? _fileKeys;

    public async Task<Subject> Create(
        string subjectName,
        string? aliases = null,
        string? associatedSubjects = null,
        string? hashTags = null)
    {
        if (string.IsNullOrWhiteSpace(subjectName))
        {
            throw new ArgumentNullException(nameof(subjectName));
        }

        _fileKeys ??= await podcastRepository.GetAllFileKeys().ToArrayAsync();

        subjectName = subjectName.Trim();
        var fileKey = FileKeyFactory.GetFileKey(subjectName);
        var rootFileKey = fileKey;
        var ctr = 2;
        do
        {
            fileKey = $"{rootFileKey}_{ctr++}";
        } while (_fileKeys.Contains(fileKey));

        var subject = new Subject(subjectName) {FileKey = fileKey};

        if (!string.IsNullOrWhiteSpace(aliases))
        {
            subject.Aliases = aliases
                .Split(",")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToArray();
        }

        if (!string.IsNullOrWhiteSpace(associatedSubjects))
        {
            subject.AssociatedSubjects = associatedSubjects
                .Split(",")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToArray();
        }

        if (!string.IsNullOrWhiteSpace(hashTags))
        {
            subject.HashTag = hashTags;
        }

        return subject;
    }
}