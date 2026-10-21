using Api.Models;

namespace Api.Services.News;

public interface INewsReportService
{
    Task<NewsReportGetResult> GetAsync(Guid reportId, CancellationToken cancellationToken);

    Task<NewsReportUpdateResult> UpdateAsync(
        NewsReportChangeRequestWrapper request,
        CancellationToken cancellationToken);
}
