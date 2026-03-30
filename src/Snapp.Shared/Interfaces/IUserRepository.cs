using Snapp.Shared.Models;

namespace Snapp.Shared.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(string userId);

    Task<UserPii?> GetPiiAsync(string userId);

    Task<string?> GetUserIdByEmailHashAsync(string emailHash);

    Task CreateAsync(User user, UserPii pii, string emailHash);

    Task UpdateAsync(User user);

    Task UpdatePiiAsync(UserPii pii);

    Task<List<User>> SearchBySpecialtyGeoAsync(string specialty, string geo, string? nextToken);
}
