using RedditPodcastPoster.Models;
using RedditPodcastPoster.People.Factories;

namespace PeopleMigrator;

internal sealed class PersonMigrationRegistry(IPersonFactory personFactory)
{
    private readonly Dictionary<string, Person> _byExactTwitter = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Person> _byExactBluesky = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Person> _byCrossPlatformToken = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, PersonBuildMetadata> _metadataByPersonId = new();

    public void Register(Person person)
    {
        IndexPerson(person);
    }

    public IEnumerable<Person> EnumeratePersons()
    {
        return _byExactTwitter.Values
            .Concat(_byExactBluesky.Values)
            .GroupBy(x => x.Id)
            .Select(x => x.First());
    }

    public PersonBuildMetadata GetMetadata(Person person)
    {
        return _metadataByPersonId.TryGetValue(person.Id, out var metadata)
            ? metadata
            : PersonBuildMetadata.Empty;
    }

    public void ApplyApiResolution(Person person, DisplayNameResolution resolution)
    {
        var metadata = GetOrCreateMetadata(person.Id);
        metadata.TwitterApiName = resolution.TwitterName;
        metadata.BlueskyApiName = resolution.BlueskyName;
        metadata.ApiNameSource = resolution.ChosenSource;

        if (string.IsNullOrWhiteSpace(resolution.ChosenName))
        {
            return;
        }

        metadata.NameResolvedFromApi = true;
    }

    public void ApplyDescriptionExtract(
        string? twitter,
        string? bluesky,
        string displayName,
        IEnumerable<string>? aliases,
        Guid episodeId)
    {
        var person = FindExisting(twitter, bluesky);
        if (person == null || string.IsNullOrWhiteSpace(displayName))
        {
            return;
        }

        var metadata = GetOrCreateMetadata(person.Id);
        metadata.SourceEpisodeIds.Add(episodeId);

        if (ShouldReplaceWithDescriptionName(person, displayName))
        {
            person.Name = displayName.Trim();
            metadata.DescriptionExtractedName = displayName.Trim();
        }

        MergeAliases(person, aliases, displayName);
    }

    public int ApplyAliasExtract(
        string? twitter,
        string? bluesky,
        IEnumerable<string>? aliases,
        Guid episodeId)
    {
        var person = FindExisting(twitter, bluesky);
        if (person == null)
        {
            return 0;
        }

        var metadata = GetOrCreateMetadata(person.Id);
        metadata.SourceEpisodeIds.Add(episodeId);

        var beforeCount = person.Aliases?.Length ?? 0;
        MergeAliases(person, aliases, person.Name);
        var afterCount = person.Aliases?.Length ?? 0;
        return Math.Max(0, afterCount - beforeCount);
    }

    /// <summary>
    /// When an episode lists twitter[i] with bluesky[i], treat as the same guest.
    /// </summary>
    public void LinkPair(string? twitter, string? bluesky)
    {
        if (string.IsNullOrWhiteSpace(twitter) && string.IsNullOrWhiteSpace(bluesky))
        {
            return;
        }

        var left = FindExisting(twitter, null);
        var right = FindExisting(null, bluesky);

        if (left != null && right != null && left.Id != right.Id)
        {
            MergeInto(left, right);
            ApplyHandles(left, twitter, bluesky);
            return;
        }

        var person = left ?? right ?? CreatePerson(twitter, bluesky);
        ApplyHandles(person, twitter, bluesky);
    }

    public (Person Person, bool Created, bool Updated) Resolve(string? twitter, string? bluesky)
    {
        var existing = FindExisting(twitter, bluesky);
        if (existing != null)
        {
            var updated = ApplyHandles(existing, twitter, bluesky);
            return (existing, false, updated);
        }

        var created = CreatePerson(twitter, bluesky);
        return (created, true, false);
    }

    public Person? FindExistingPublic(string? twitter, string? bluesky) => FindExisting(twitter, bluesky);

    public PersonBuildMetadata GetOrCreateMetadataPublic(Guid personId) => GetOrCreateMetadata(personId);

    private Person? FindExisting(string? twitter, string? bluesky)
    {
        Person? person = null;

        var exactTwitter = PersonHandleNormalizer.NormalizeExactHandle(twitter);
        if (!string.IsNullOrWhiteSpace(exactTwitter) &&
            _byExactTwitter.TryGetValue(exactTwitter, out var byTwitter))
        {
            person = byTwitter;
        }

        var exactBluesky = PersonHandleNormalizer.NormalizeExactHandle(bluesky);
        if (person == null &&
            !string.IsNullOrWhiteSpace(exactBluesky) &&
            _byExactBluesky.TryGetValue(exactBluesky, out var byBluesky))
        {
            person = byBluesky;
        }

        foreach (var token in CrossPlatformTokens(twitter, bluesky))
        {
            if (person != null)
            {
                break;
            }

            if (_byCrossPlatformToken.TryGetValue(token, out var byToken))
            {
                person = byToken;
            }
        }

        return person;
    }

    private Person CreatePerson(string? twitter, string? bluesky)
    {
        var person = personFactory.Create(
            PersonHandleNormalizer.DeriveDisplayName(twitter, bluesky),
            null,
            twitter,
            bluesky);
        IndexPerson(person);
        return person;
    }

    private bool ApplyHandles(Person person, string? twitter, string? bluesky)
    {
        var updated = false;

        if (!string.IsNullOrWhiteSpace(twitter) && string.IsNullOrWhiteSpace(person.TwitterHandle))
        {
            person.TwitterHandle = PersonFactory.NormalizeHandle(twitter);
            updated = true;
        }

        if (!string.IsNullOrWhiteSpace(bluesky) && string.IsNullOrWhiteSpace(person.BlueskyHandle))
        {
            person.BlueskyHandle = PersonFactory.NormalizeHandle(bluesky);
            updated = true;
        }

        if (updated)
        {
            IndexPerson(person);
        }

        return updated;
    }

    private void IndexPerson(Person person)
    {
        if (!string.IsNullOrWhiteSpace(person.TwitterHandle))
        {
            _byExactTwitter[PersonHandleNormalizer.NormalizeExactHandle(person.TwitterHandle)!] = person;
            IndexCrossPlatformToken(PersonHandleNormalizer.ToMatchToken(person.TwitterHandle), person);
        }

        if (!string.IsNullOrWhiteSpace(person.BlueskyHandle))
        {
            _byExactBluesky[PersonHandleNormalizer.NormalizeExactHandle(person.BlueskyHandle)!] = person;
            IndexCrossPlatformToken(PersonHandleNormalizer.ToMatchToken(person.BlueskyHandle), person);
        }
    }

    private void IndexCrossPlatformToken(string? token, Person person)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        _byCrossPlatformToken[token] = person;
    }

    private void MergeInto(Person target, Person source)
    {
        if (target.Id == source.Id)
        {
            return;
        }

        ApplyHandles(target, source.TwitterHandle, source.BlueskyHandle);
        ReassignIndexes(source, target);
    }

    private void ReassignIndexes(Person source, Person target)
    {
        foreach (var key in _byExactTwitter.Where(x => x.Value.Id == source.Id).Select(x => x.Key).ToArray())
        {
            _byExactTwitter[key] = target;
        }

        foreach (var key in _byExactBluesky.Where(x => x.Value.Id == source.Id).Select(x => x.Key).ToArray())
        {
            _byExactBluesky[key] = target;
        }

        foreach (var key in _byCrossPlatformToken.Where(x => x.Value.Id == source.Id).Select(x => x.Key).ToArray())
        {
            _byCrossPlatformToken[key] = target;
        }
    }

    private static IEnumerable<string> CrossPlatformTokens(string? twitter, string? bluesky)
    {
        var twitterToken = PersonHandleNormalizer.ToMatchToken(twitter);
        if (!string.IsNullOrWhiteSpace(twitterToken))
        {
            yield return twitterToken;
        }

        var blueskyToken = PersonHandleNormalizer.ToMatchToken(bluesky);
        if (!string.IsNullOrWhiteSpace(blueskyToken))
        {
            yield return blueskyToken;
        }
    }

    private PersonBuildMetadata GetOrCreateMetadata(Guid personId)
    {
        if (!_metadataByPersonId.TryGetValue(personId, out var metadata))
        {
            metadata = new PersonBuildMetadata();
            _metadataByPersonId[personId] = metadata;
        }

        return metadata;
    }

    private static bool ShouldReplaceWithDescriptionName(Person person, string candidateName)
    {
        if (string.IsNullOrWhiteSpace(candidateName))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(person.Name) ||
            person.Name.Equals("Unknown guest", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var derivedName = PersonHandleNormalizer.DeriveDisplayName(person.TwitterHandle, person.BlueskyHandle);
        if (person.Name.Equals(derivedName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !person.Name.Contains(' ') && candidateName.Contains(' ');
    }

    private static void MergeAliases(Person person, IEnumerable<string>? aliases, string primaryName)
    {
        var merged = new List<string>();
        if (person.Aliases != null)
        {
            merged.AddRange(person.Aliases);
        }

        if (aliases != null)
        {
            foreach (var alias in aliases)
            {
                if (string.IsNullOrWhiteSpace(alias))
                {
                    continue;
                }

                var trimmed = alias.Trim();
                if (PersonAliasFilter.ShouldExcludeAlias(trimmed, person.Name, person.TwitterHandle, person.BlueskyHandle) ||
                    PersonAliasFilter.IsSameName(trimmed, primaryName))
                {
                    continue;
                }

                if (!PersonDisplayNameResolver.IsUsableDisplayName(trimmed, person.TwitterHandle, person.BlueskyHandle))
                {
                    continue;
                }

                merged.Add(trimmed);
            }
        }

        var filtered = PersonAliasFilter.FilterAliases(
            merged,
            person.Name,
            person.TwitterHandle,
            person.BlueskyHandle);

        person.Aliases = filtered.Length == 0 ? null : filtered;
        CanonicalNamePromoter.ApplyToPerson(person);
    }
}

internal sealed class PersonBuildMetadata
{
    public static PersonBuildMetadata Empty { get; } = new();

    public string? DescriptionExtractedName { get; set; }

    public bool NameResolvedFromApi { get; set; }

    public string? TwitterApiName { get; set; }

    public string? BlueskyApiName { get; set; }

    public string? ApiNameSource { get; set; }

    public string? SeedNotes { get; set; }

    public HashSet<Guid> SourceEpisodeIds { get; } = [];
}
