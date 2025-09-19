using Dapper;
using Yeek.Database;
using Yeek.FileHosting.Model;
using Yeek.Security.Model;

namespace Yeek.Security.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> UserExists(Guid userId)
    {
        await using var con = await _context.DataSource.OpenConnectionAsync();

        const string commandText = """
                                   SELECT 1 FROM users WHERE id = @Id;
                                   """;

        var args = new { Id = userId };
        var result = await con.QuerySingleOrDefaultAsync<int?>(commandText, args);

        return result != null;
    }

    public async Task<User> GetUserAsync(Guid userId, bool getNotifs = false, bool getRatings = false)
    {
        await using var con = await _context.DataSource.OpenConnectionAsync();

        const string getUserSql = """
                                  SELECT id, displayname, trustlevel, lastlogin FROM users WHERE Id = @Id;
                                  """;
        var getUserArgs = new { Id = userId };
        var userResult = await con.QuerySingleOrDefaultAsync<(Guid id, string displayname, int trustlevel, DateTime lastlogin)?>(getUserSql, getUserArgs);

        if (!userResult.HasValue)
            throw new InvalidOperationException("User not found");

        var user = new User
        {
            Id = userResult.Value.id,
            DisplayName = userResult.Value.displayname,
            TrustLevel = (TrustLevel)userResult.Value.trustlevel,
            LastLogin = userResult.Value.lastlogin
        };

        if (getNotifs)
        {
            throw new NotImplementedException();
        }

        if (getRatings)
        {
            throw new NotImplementedException();
        }

        return user;
    }

    public async Task AddUserAsync(User user)
    {
        await using var con = await _context.DataSource.OpenConnectionAsync();
        const string addUserSql = """
                                  INSERT INTO users (id, displayname) VALUES (@Id, @DisplayName);
                                  """;

        var args = new { Id = user.Id, DisplayName = user.DisplayName };
        var result = await con.ExecuteAsync(addUserSql, args);

        if (result != 1)
            throw new InvalidOperationException("Failed to add user");
    }

    public async Task UpdateUserNameAsync(Guid userId, string name)
    {
        if (!await UserExists(userId))
            throw new InvalidOperationException("User not found");

        await using var con = await _context.DataSource.OpenConnectionAsync();
        const string updateNameSql = """
                                     UPDATE users SET displayname = @DisplayName WHERE id = @Id;
                                     """;
        var args = new { Id = userId, DisplayName = name };
        var result = await con.ExecuteAsync(updateNameSql, args);

        if (result != 1)
            throw new InvalidOperationException("Failed to update user.");
    }

    public async Task NotifyLoginAsync(Guid userId)
    {
        if (!await UserExists(userId))
            throw new InvalidOperationException("User not found");

        await using var con = await _context.DataSource.OpenConnectionAsync();
        const string updateLoginSql = """
                                     UPDATE users SET lastlogin = @Time WHERE id = @Id;
                                     """;

        await con.ExecuteAsync(updateLoginSql, new { Time = DateTime.UtcNow, Id = userId });
    }

    public async Task<List<User>> GetAllUsersAsync()
    {
        await using var con = await _context.DataSource.OpenConnectionAsync();
        const string sql = """
                           SELECT * FROM users;
                           """;

        return (await con.QueryAsync<User>(sql)).ToList();
    }
}