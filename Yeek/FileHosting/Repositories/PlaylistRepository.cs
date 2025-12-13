using System.Runtime.CompilerServices;
using Dapper;
using Yeek.Database;
using Yeek.FileHosting.Model;
using Yeek.Security.Repositories;

namespace Yeek.FileHosting.Repositories;

public class PlaylistRepository : IPlaylistRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FileRepository> _logger;
    private readonly IFileRepository _fileRepository;

    public PlaylistRepository(ApplicationDbContext dbContext, ILogger<FileRepository> logger, IFileRepository fileRepository)
    {
        _context = dbContext;
        _logger = logger;
        _fileRepository = fileRepository;
    }

    public async Task<List<Playlist>> GetPlaylistsForUser(Guid userId)
    {
        const string sql = """
                           SELECT * FROM Playlists
                           WHERE UserId = @userId
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();
        return (await con.QueryAsync<Playlist>(sql, new { UserId = userId })).ToList();
    }

    public async Task<List<Guid>> GetPlaylistIdsForFileForUser(Guid fileId, Guid userId)
    {
        const string sql = """
                           SELECT p.Id
                           FROM Playlists p
                           JOIN PlaylistEntry pe ON pe.PlaylistId = p.Id
                           WHERE p.UserId = @userId
                             AND pe.UploadedFileId = @fileId;
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();
        return (await con.QueryAsync<Guid>(sql, new { userId, fileId })).ToList();
    }

    public async Task CreatePlaylist(Guid userId, string name)
    {
        const string existsSql = """
                                 SELECT 1
                                 FROM Playlists
                                 WHERE UserId = @userId
                                   AND LOWER(Name) = LOWER(@name)
                                 LIMIT 1;
                                 """;

        const string insertSql = """
                                 INSERT INTO Playlists (Id, Name, UserId)
                                 VALUES (@Id, @Name, @UserId);
                                 """;

        await using var con = await _context.DataSource.OpenConnectionAsync();

        // Check for name conflict
        var exists = await con.ExecuteScalarAsync<int?>(existsSql, new { userId, name });
        if (exists.HasValue)
        {
            throw new InvalidOperationException($"A playlist named '{name}' already exists for this user.");
        }

        // Create playlist
        var playlistId = Guid.NewGuid();

        await con.ExecuteAsync(insertSql, new
        {
            Id = playlistId,
            Name = name,
            UserId = userId
        });
    }

    public async Task<bool> RemoveOrAddSongToPlaylist(Guid playlistId, Guid fileId)
    {
        const string existsSql = """
                                 SELECT Id
                                 FROM PlaylistEntry
                                 WHERE PlaylistId = @playlistId
                                    AND UploadedFileId = @fileId
                                 LIMIT 1;
                                 """;

        const string insertSql = """
                                 INSERT INTO PlaylistEntry (PlaylistId, UploadedFileId, AddedToPlaylist)
                                 VALUES (@PlaylistId, @UploadedFileId, @When);
                                 """;

        const string deleteSql = """
                                 DELETE FROM PlaylistEntry
                                 WHERE Id = @Id;
                                 """;

        await using var con = await _context.DataSource.OpenConnectionAsync();

        // Check if the song already exists in the playlist
        var existingEntryId = await con.ExecuteScalarAsync<int?>(existsSql, new
        {
            playlistId,
            fileId
        });

        if (existingEntryId.HasValue)
        {
            // Remove song
            await con.ExecuteAsync(deleteSql, new { Id = existingEntryId.Value });
            return false;
        }

        // Add song
        await con.ExecuteAsync(insertSql, new
        {
            PlaylistId = playlistId,
            UploadedFileId = fileId,
            When = DateTime.UtcNow,
        });

        return true;
    }

    public async Task<bool> CanManagePlaylist(Guid playlistId, Guid userId)
    {
        const string sql = """
                           SELECT 1
                           FROM Playlists
                           WHERE Id = @playlistId
                            AND UserId = @userId
                           LIMIT 1;
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();

        var result = await con.ExecuteScalarAsync<int?>(sql, new
        {
            playlistId,
            userId
        });

        return result.HasValue;
    }

    public async Task<Playlist?> GetPlaylistOrNull(Guid playlistId)
    {
        const string sql = """
                           SELECT *
                           FROM Playlists
                           WHERE Id = @playlistId
                           LIMIT 1;
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();

        return await con.QuerySingleOrDefaultAsync<Playlist>(sql, new
        {
            playlistId
        });
    }

    public async IAsyncEnumerable<UploadedFile> EnumeratePlaylistEntriesAsync(
        Playlist playlist,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT UploadedFileId
                           FROM PlaylistEntry
                           WHERE PlaylistId = @playlistId
                           ORDER BY AddedToPlaylist;
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync(cancellationToken);

        var fileIds = await con.QueryAsync<Guid>(sql, new
        {
            playlistId = playlist.Id
        });

        foreach (var fileId in fileIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var file = await _fileRepository.GetUploadedFileAsync(fileId);
            yield return file;
        }
    }


    public async Task DeletePlaylist(Guid playlistId)
    {
        const string sql = """
                           DELETE FROM Playlists
                           WHERE Id = @playlistId
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();
        await con.ExecuteAsync(sql, new
        {
            playlistId = playlistId
        });
    }

    public async Task<IEnumerable<Guid>> GetAllPlaylists()
    {
        const string sql = """
                           SELECT Id
                           FROM Playlists
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();

        return await con.QueryAsync<Guid>(sql);
    }
}