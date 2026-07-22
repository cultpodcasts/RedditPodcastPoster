using PersonEntity = RedditPodcastPoster.Models.People.Person;
using Person = Api.Dtos.Person;
using RedditPodcastPoster.People.Factories;
using RedditPodcastPoster.People.Resolvers;

namespace Api.Services.People;

public static class PersonFieldUpdater
{
    public static void Apply(PersonEntity entity, Person change)
    {
        if (change.Name != null && !string.IsNullOrWhiteSpace(change.Name))
        {
            entity.Name = change.Name.Trim();
            entity.EnsureNameKey();
        }

        if (change.IsOrganization.HasValue)
        {
            entity.IsOrganization = change.IsOrganization.Value;
        }

        if (change.SortName != null)
        {
            entity.SortName = PersonSortNameResolver.ResolveForPersist(
                entity.Name,
                change.SortName,
                entity.IsOrganization);
        }
        else if (change.IsOrganization == true)
        {
            entity.SortName = PersonSortNameResolver.ResolveForPersist(
                entity.Name,
                entity.SortName,
                isOrganization: true);
        }
        else if (change.IsOrganization == false)
        {
            // Dropping the org flag without an explicit sortName → surname default.
            entity.SortName = PersonSortNameResolver.ResolveForPersist(
                entity.Name,
                sortName: null,
                isOrganization: false);
        }

        if (change.Aliases != null)
        {
            entity.Aliases = change.Aliases.Length == 0
                ? null
                : change.Aliases.Select(x => x.Trim()).ToArray();
        }

        if (change.TwitterHandle != null)
        {
            entity.TwitterHandle = string.IsNullOrWhiteSpace(change.TwitterHandle)
                ? null
                : PersonFactory.NormalizeHandle(change.TwitterHandle);
        }

        if (change.BlueskyHandle != null)
        {
            entity.BlueskyHandle = string.IsNullOrWhiteSpace(change.BlueskyHandle)
                ? null
                : PersonFactory.NormalizeHandle(change.BlueskyHandle);
        }
    }
}
