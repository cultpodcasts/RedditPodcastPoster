using Api.Dtos;

namespace Api.Services.Catalogue;

public interface ICatalogueIdentityLookupService
{
    Task<CatalogueIdentityLookupServiceResult> LookupAsync(
        CatalogueIdentityLookupRequest request,
        CancellationToken cancellationToken = default);
}

public enum CatalogueIdentityLookupServiceStatus
{
    Ok,
    BadRequest
}

public sealed record CatalogueIdentityLookupServiceResult(
    CatalogueIdentityLookupServiceStatus Status,
    CatalogueIdentityLookupResponse? Response);
