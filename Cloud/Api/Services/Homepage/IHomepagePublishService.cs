using Api.Models;

namespace Api.Services.Homepage;

public interface IHomepagePublishService
{
    Task<HomepagePublishResult> PublishAsync(CancellationToken cancellationToken);
}
