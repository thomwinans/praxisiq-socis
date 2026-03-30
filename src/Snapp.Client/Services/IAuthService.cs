using Snapp.Shared.DTOs.Auth;
using Snapp.Shared.DTOs.Common;

namespace Snapp.Client.Services;

public interface IAuthService
{
    Task<MessageResponse> RequestMagicLinkAsync(string email);
    Task<TokenResponse> ValidateCodeAsync(string code);
    Task<TokenResponse> RefreshAsync(string refreshToken);
    Task LogoutAsync();
}
