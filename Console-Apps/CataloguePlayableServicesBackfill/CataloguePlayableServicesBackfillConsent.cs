namespace CataloguePlayableServicesBackfill;

/// <summary>
/// Human consent gate for writes. <c>--apply</c> alone is refused (exit 2).
/// Agents must never supply <c>--consent</c>.
/// </summary>
public static class CataloguePlayableServicesBackfillConsent
{
    public const string ApplyPhrase = "I approve CataloguePlayableServicesBackfill production writes";

    public static bool IsApplyConsented(CataloguePlayableServicesBackfillRequest request) =>
        request.Apply
        && request.Consent is { } consent
        && string.Equals(consent.Trim(), ApplyPhrase, StringComparison.Ordinal);
}
