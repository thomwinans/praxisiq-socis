using Snapp.Shared.DTOs.User;

namespace Snapp.Client.Services;

public interface IUserService
{
    Task<ProfileResponse> GetMyProfileAsync();
    Task<ProfileResponse> GetProfileAsync(string userId);
    Task<ProfileResponse> UpdateProfileAsync(UpdateProfileRequest request);
    Task<ProfileResponse> OnboardAsync(OnboardingRequest request);
    Task<PiiResponse> GetMyPiiAsync();
}
