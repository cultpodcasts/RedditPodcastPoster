namespace RedditPodcastPoster.Models;

public static class SubjectFactory
{
    public static Subject Create(string name, string? aliases = null, string? associatedSubjects = null)
    {
        var subject = new Subject(name);
        subject.FileKey = FileKeyFactory.GetFileKey(name);

        if (aliases != null)
        {
            subject.Aliases = aliases.Split(",").Select(x => x.Trim()).ToArray();
        }

        if (associatedSubjects != null)
        {
            subject.AssociatedSubjects = associatedSubjects.Split(",").Select(x => x.Trim()).ToArray();
        }

        return subject;
    }
}