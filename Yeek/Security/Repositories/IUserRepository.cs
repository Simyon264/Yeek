using Yeek.FileHosting.Model;
using Yeek.Security.Model;

namespace Yeek.Security.Repositories;

public interface IUserRepository
{
    public Task<bool> UserExists(Guid userId);

    public Task<User> GetUserAsync(Guid userId, bool getNotifs = false, bool getRatings = false);

    public Task AddUserAsync(User user);

    public Task UpdateUserNameAsync(Guid userId, string name);
    public Task NotifyLoginAsync(Guid userId);
    public Task<List<User>> GetAllUsersAsync();
    Task SetNotificationRead(Guid userId, int notificationId);
}