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

    public async Task<List<GlobalMessage>> GetAllMessagesAsync()
    {
        const string sql = """
                           SELECT * FROM globalmessages ORDER BY id DESC;
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();

        return (await con.QueryAsync<GlobalMessage>(sql)).ToList();
    }

    public async Task<bool> ToggleGlobalMessageShowAsync(int id)
    {
        const string sql = "UPDATE globalmessages SET show = NOT show WHERE id = @Id RETURNING show;";
        await using var con = await _context.DataSource.OpenConnectionAsync();
        var result = await con.ExecuteScalarAsync<bool?>(sql, new { Id = id });
        return result != null;
    }

    public async Task<bool> EditGlobalMessageHeaderAsync(int id, string header)
    {
        const string sql = "UPDATE globalmessages SET header = @Header WHERE id = @Id;";
        await using var con = await _context.DataSource.OpenConnectionAsync();
        var rows = await con.ExecuteAsync(sql, new { Id = id, Header = header });
        return rows > 0;
    }

    public async Task<bool> EditGlobalMessageContentAsync(int id, string content)
    {
        const string sql = "UPDATE globalmessages SET content = @Content WHERE id = @Id;";
        await using var con = await _context.DataSource.OpenConnectionAsync();
        var rows = await con.ExecuteAsync(sql, new { Id = id, Content = content });
        return rows > 0;
    }

    public async Task CreateGlobalMessageAsync(string header, string content)
    {
        const string sql = "INSERT INTO globalmessages (header, content, show) VALUES (@Header, @Content, true);";
        await using var con = await _context.DataSource.OpenConnectionAsync();
        await con.ExecuteAsync(sql, new { Header = header, Content = content });
    }
}