namespace CatalogueMigrate;

/// <summary>
/// Human consent gate for writes. <c>--apply</c> alone is refused (exit 2).
/// The phrase names the kind (or the evidence file) so consent for one batch
/// cannot be replayed for another. Agents must never supply it
/// (<c>.cursor/rules/catalogue-migrate-cli-dry-run.mdc</c>).
/// </summary>
public static class CatalogueMigrateConsent
{
    public static string ApplyPhrase(string kind) =>
        $"I approve CatalogueMigrate --kind {kind} production writes";

    public static string RollbackPhrase(string evidenceFileName) =>
        $"I approve CatalogueMigrate rollback of {evidenceFileName} in production";

    public static bool IsApplyConsented(CatalogueMigrateRequest request) =>
        request.Apply
        && request.Consent is { } consent
        && string.Equals(consent.Trim(), ApplyPhrase(request.Kind), StringComparison.Ordinal);

    public static bool IsRollbackConsented(CatalogueMigrateRequest request) =>
        request.Apply
        && request.Rollback is { } path
        && request.Consent is { } consent
        && string.Equals(consent.Trim(), RollbackPhrase(Path.GetFileName(path)), StringComparison.Ordinal);
}
