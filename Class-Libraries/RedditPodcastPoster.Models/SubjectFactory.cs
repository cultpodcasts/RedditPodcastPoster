namespace RedditPodcastPoster.Models;

public static class SubjectFactory
{
    public static Subject Create(string name, string? aliases = null, string? associatedSubjects = null,
        string? hashTags = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentNullException(nameof(name));
        }

        var subject = new Subject(name)
        {
            FileKey = FileKeyFactory.GetFileKey(name)
        };

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