using Dapper;
using Yeek.Core.Models;
using Yeek.Database;

namespace Yeek.Core.Repositories;

public class AdminRepository : IAdminRepository
{
    private readonly ApplicationDbContext _context;

    public AdminRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<GlobalMessage>> GetAllActiveMessagesAsync()
    {
        const string sql = """
                           SELECT * FROM globalmessages WHERE show = true ORDER BY id DESC;
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();

        return (await con.QueryAsync<GlobalMessage>(sql)).ToList();
    }
}