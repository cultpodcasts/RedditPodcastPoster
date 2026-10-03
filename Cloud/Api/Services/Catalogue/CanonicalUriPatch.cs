namespace Api.Services.Catalogue;

public enum CanonicalUriPatchKind
{
    Omit,
    Set,
    Clear,
    Invalid
}

public readonly record struct CanonicalUriPatchResult(
    CanonicalUriPatchKind Kind,
    Uri? Uri,
    string? Error);

public static class CanonicalUriPatch
{
    public static CanonicalUriPatchResult Read(string? value)
    {
        if (value is null)
        {
            return new CanonicalUriPatchResult(CanonicalUriPatchKind.Omit, null, null);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return new CanonicalUriPatchResult(CanonicalUriPatchKind.Clear, null, null);
        }

        if (Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
        {
            return new CanonicalUriPatchResult(CanonicalUriPatchKind.Set, uri, null);
        }

        return new CanonicalUriPatchResult(
            CanonicalUriPatchKind.Invalid,
            null,
            "Must be an absolute http(s) URL.");
    }

    public static bool TryApply(string? value, Action<Uri?> assign, out string? error)
    {
        var patch = Read(value);
        error = patch.Error;
        switch (patch.Kind)
        {
            case CanonicalUriPatchKind.Omit:
                return true;
            case CanonicalUriPatchKind.Clear:
                assign(null);
                return true;
            case CanonicalUriPatchKind.Set:
                assign(patch.Uri);
                return true;
            default:
                return false;
        }
    }
}
