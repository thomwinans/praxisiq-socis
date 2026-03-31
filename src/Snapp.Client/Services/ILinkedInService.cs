using Snapp.Shared.DTOs.LinkedIn;

namespace Snapp.Client.Services;

public interface ILinkedInService
{
    Task<LinkedInAuthUrlResponse> GetAuthUrlAsync();
    Task<LinkedInStatusResponse> GetStatusAsync();
    Task UnlinkAsync();
    Task<LinkedInShareResponse> ShareAsync(LinkedInShareRequest request);
}
