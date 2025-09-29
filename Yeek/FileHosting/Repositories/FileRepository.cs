using Dapper;
using Yeek.Database;
using Yeek.FileHosting.Model;
using Yeek.Security.Model;

namespace Yeek.FileHosting.Repositories;

public class FileRepository : IFileRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FileRepository> _logger;

    public FileRepository(ApplicationDbContext dbContext, ILogger<FileRepository> logger)
    {
        _context = dbContext;
        _logger = logger;
    }

    public async Task<(List<UploadedFile> result, int allCount, int pageCount)> SearchAsync(
        string query, SearchMode mode, int page = 0, int itemsPerPage = 50)
    {
        // God this method keeps getting longer
        // TODO: Deduplicate all this shit jesus fuck

        var isEmptySearch = string.IsNullOrWhiteSpace(query);

        // Check for special "uploadedby:<guid>" query
        Guid? uploadedByFilter = null;
        const string uploadedByPrefix = "uploadedby:";
        if (!isEmptySearch && query.StartsWith(uploadedByPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var guidPart = query.Substring(uploadedByPrefix.Length);
            if (Guid.TryParse(guidPart, out var parsedGuid))
            {
                uploadedByFilter = parsedGuid;
                isEmptySearch = false; // we still have a filter, just a special one
            }
        }

        var orderBy = mode switch
        {
            SearchMode.Relevance => isEmptySearch ? "RANDOM()" : "rank DESC",
            SearchMode.Top => "COALESCE(r.rating, 0) DESC",
            SearchMode.Recent => "uf.uploadedon DESC",
            _ => isEmptySearch ? "RANDOM()" : "rank DESC"
        };

        string countSql;
        string searchSql;

        if (uploadedByFilter.HasValue)
        {
            // Special uploadedby:<guid> filter
            countSql = "SELECT COUNT(*) FROM uploadedfiles WHERE uploadedby = @UploadedById;";

            searchSql = $"""
                         WITH latest_revisions AS (
                             SELECT DISTINCT ON (fr.uploadedfileid)
                                 fr.uploadedfileid,
                                 fr.revisionid,
                                 fr.updatedbyid,
                                 fr.updatedon,
                                 fr.trackname,
                                 fr.albumname,
                                 fr.artistname,
                                 fr.changesummary
                             FROM filerevisions fr
                             ORDER BY fr.uploadedfileid, fr.revisionid DESC
                         ),
                         ratings AS (
                             SELECT uploadedfileid, SUM(score) AS rating
                             FROM ratings
                             GROUP BY uploadedfileid
                         )
                         SELECT uf.id,
                                uf.relativepath,
                                uf.hash,
                                uf.uploadedon,
                                uf.uploadedby AS uploadedbyid,
                                COALESCE(r.rating, 0) AS rating,
                                lr.revisionid,
                                lr.updatedbyid,
                                lr.updatedon,
                                lr.trackname,
                                lr.albumname,
                                lr.artistname,
                                uf.originalname,
                                uf.filesize,
                                lr.changesummary,
                                uf.locked
                         FROM uploadedfiles uf
                         INNER JOIN latest_revisions lr ON uf.id = lr.uploadedfileid
                         LEFT JOIN ratings r ON uf.id = r.uploadedfileid
                         WHERE uf.uploadedby = @UploadedById
                         ORDER BY {orderBy}
                         OFFSET @Offset
                         LIMIT @Limit;
                         """;
        }  else if (isEmptySearch)
        {
            // No search filter -> just count everything
            countSql = "SELECT COUNT(*) FROM uploadedfiles;";

            searchSql = $"""
                             WITH latest_revisions AS (
                                 SELECT DISTINCT ON (fr.uploadedfileid)
                                     fr.uploadedfileid,
                                     fr.revisionid,
                                     fr.updatedbyid,
                                     fr.updatedon,
                                     fr.trackname,
                                     fr.albumname,
                                     fr.artistname,
                                     fr.changesummary
                                 FROM filerevisions fr
                                 ORDER BY fr.uploadedfileid, fr.revisionid DESC
                             ),
                             ratings AS (
                                 SELECT uploadedfileid, SUM(score) AS rating
                                 FROM ratings
                                 GROUP BY uploadedfileid
                             )
                             SELECT uf.id,
                                    uf.relativepath,
                                    uf.hash,
                                    uf.uploadedon,
                                    uf.uploadedby AS uploadedbyid,
                                    COALESCE(r.rating, 0) AS rating,
                                    lr.revisionid,
                                    lr.updatedbyid,
                                    lr.updatedon,
                                    lr.trackname,
                                    lr.albumname,
                                    lr.artistname,
                                    uf.originalname,
                                    uf.filesize,
                                    lr.changesummary,
                                    uf.locked
                             FROM uploadedfiles uf
                             INNER JOIN latest_revisions lr ON uf.id = lr.uploadedfileid
                             LEFT JOIN ratings r ON uf.id = r.uploadedfileid
                             ORDER BY {orderBy}
                             OFFSET @Offset
                             LIMIT @Limit;
                         """;
        }
        else
        {
            // Normal search with tsvector
            countSql = """
                       WITH search_results AS (
                           SELECT DISTINCT ON (fr.uploadedfileid)
                               fr.uploadedfileid
                           FROM filerevisions fr
                           WHERE fr.search_tsvector @@ plainto_tsquery('english', @Query)
                       )
                       SELECT COUNT(*) FROM search_results;
                       """;

            searchSql = $"""
                             WITH latest_revisions AS (
                                 SELECT DISTINCT ON (fr.uploadedfileid)
                                     fr.uploadedfileid,
                                     fr.revisionid,
                                     fr.updatedbyid,
                                     fr.updatedon,
                                     fr.trackname,
                                     fr.albumname,
                                     fr.artistname,
                                     fr.search_tsvector,
                                     fr.changesummary
                                 FROM filerevisions fr
                                 WHERE fr.search_tsvector @@ plainto_tsquery('english', @Query)
                                 ORDER BY fr.uploadedfileid, fr.revisionid DESC
                             ),
                             ratings AS (
                                 SELECT uploadedfileid, SUM(score) AS rating
                                 FROM ratings
                                 GROUP BY uploadedfileid
                             )
                             SELECT uf.id,
                                    uf.relativepath,
                                    uf.hash,
                                    uf.uploadedon,
                                    uf.uploadedby AS uploadedbyid,
                                    COALESCE(r.rating, 0) AS rating,
                                    lr.revisionid,
                                    lr.updatedbyid,
                                    lr.updatedon,
                                    lr.trackname,
                                    lr.albumname,
                                    lr.artistname,
                                    uf.originalname,
                                    uf.filesize,
                                    lr.changesummary,
                                    uf.locked,
                                    ts_rank_cd(lr.search_tsvector, plainto_tsquery('english', @Query)) AS rank
                             FROM uploadedfiles uf
                             INNER JOIN latest_revisions lr ON uf.id = lr.uploadedfileid
                             LEFT JOIN ratings r ON uf.id = r.uploadedfileid
                             ORDER BY {orderBy}
                             OFFSET @Offset
                             LIMIT @Limit;
                         """;
        }

        await using var con = await _context.DataSource.OpenConnectionAsync();

        var allCount = await con.ExecuteScalarAsync<int>(countSql, new
        {
            Query = query,
            UploadedById = uploadedByFilter
        });

        var rows = await FetchUploadedFilesAsync(searchSql, new
        {
            Query = query,
            UploadedById = uploadedByFilter,
            Offset = page * itemsPerPage,
            Limit = itemsPerPage
        });

        var pageCount = (int)Math.Ceiling(allCount / (double)itemsPerPage);

        return (rows, allCount, pageCount);
    }

    public async Task<List<UploadedFile>> GetRandomMidis(int amount = 6)
    {
        const string sql = """
                      WITH latest_revisions AS (
                          SELECT DISTINCT ON (fr.uploadedfileid)
                              fr.uploadedfileid,
                              fr.revisionid,
                              fr.updatedbyid,
                              fr.updatedon,
                              fr.trackname,
                              fr.albumname,
                              fr.artistname,
                              fr.changesummary
                          FROM filerevisions fr
                          ORDER BY fr.uploadedfileid, fr.revisionid DESC
                      ),
                      ratings AS (
                          SELECT uploadedfileid, SUM(score) AS rating
                          FROM ratings
                          GROUP BY uploadedfileid
                      )
                      SELECT uf.id,
                             uf.relativepath,
                             uf.hash,
                             uf.uploadedon,
                             uf.uploadedby AS uploadedbyid,
                             COALESCE(r.rating, 0) AS rating,
                             lr.revisionid,
                             lr.updatedbyid,
                             lr.updatedon,
                             lr.trackname,
                             lr.albumname,
                             lr.artistname,
                             uf.originalname,
                             uf.filesize,
                             lr.changesummary,
                             uf.locked
                      FROM uploadedfiles uf
                      INNER JOIN latest_revisions lr ON uf.id = lr.uploadedfileid
                      LEFT JOIN ratings r ON uf.id = r.uploadedfileid
                      ORDER BY RANDOM()
                      LIMIT @Limit;
                      """;

        return await FetchUploadedFilesAsync(sql, new { Limit = amount });
    }

    public async Task<List<UploadedFile>> GetRecentMidisAsync(int amount = 50)
    {
        const string sql = """
                           WITH latest_revisions AS (
                               SELECT DISTINCT ON (fr.uploadedfileid)
                                   fr.uploadedfileid,
                                   fr.revisionid,
                                   fr.updatedbyid,
                                   fr.updatedon,
                                   fr.trackname,
                                   fr.albumname,
                                   fr.artistname,
                                   fr.changesummary
                               FROM filerevisions fr
                               ORDER BY fr.uploadedfileid, fr.revisionid DESC
                           ),
                           ratings AS (
                               SELECT uploadedfileid, SUM(score) AS rating
                               FROM ratings
                               GROUP BY uploadedfileid
                           )
                           SELECT uf.id,
                                  uf.relativepath,
                                  uf.hash,
                                  uf.uploadedon,
                                  uf.uploadedby AS uploadedbyid,
                                  COALESCE(r.rating, 0) AS rating,
                                  lr.revisionid,
                                  lr.updatedbyid,
                                  lr.updatedon,
                                  lr.trackname,
                                  lr.albumname,
                                  lr.artistname,
                                  uf.originalname,
                                  uf.filesize,
                                  lr.changesummary,
                                  uf.locked
                           FROM uploadedfiles uf
                           INNER JOIN latest_revisions lr ON uf.id = lr.uploadedfileid
                           LEFT JOIN ratings r ON uf.id = r.uploadedfileid
                           ORDER BY uf.uploadedon DESC
                           LIMIT 50;
                           """;

        return await FetchUploadedFilesAsync(sql, new { Limit = amount });
    }

    public async Task<int> GetAllCountAsync()
    {
        const string command = """
                               SELECT count(*) FROM public.uploadedfiles
                               """;

        await using var con = await _context.DataSource.OpenConnectionAsync();
        return await con.QueryFirstAsync<int>(command);
    }

    public async Task<(bool foundMatch, Guid? fileId)> FindFileByShaAsync(string sha)
    {
        const string sql = """
                           SELECT Id
                           FROM UploadedFiles
                           WHERE Hash = @Sha
                           LIMIT 1;
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();
        var fileId = await con.QueryFirstOrDefaultAsync<Guid?>(sql, new { Sha = sha });

        return (fileId.HasValue, fileId);
    }

    public async Task UploadFileAsync(UploadedFile uploadedFile, FileRevision fileRevision)
    {
        const string insertFileSql = """
                                     INSERT INTO UploadedFiles (Id, RelativePath, Hash, UploadedOn, UploadedBy, OriginalName, FileSize)
                                     VALUES (@Id, @RelativePath, @Hash, @UploadedOn, @UploadedById, @OriginalName, @FileSize)
                                     ON CONFLICT (Id) DO NOTHING;
                                     """;

        const string insertRevisionSql = """
                                         INSERT INTO FileRevisions (
                                             UploadedFileId, RevisionId, UpdatedById, UpdatedOn,
                                             TrackName, AlbumName, ArtistName, Description, ChangeSummary
                                         )
                                         VALUES (
                                             @UploadedFileId, @RevisionId, @UpdatedById, @UpdatedOn,
                                             @TrackName, @AlbumName, @ArtistName, @Description, 'Initial upload.'
                                         );
                                         """;

        await using var con = await _context.DataSource.OpenConnectionAsync();
        await using var transaction = await con.BeginTransactionAsync();

        try
        {
            // Insert file (if it doesn't exist already)
            await con.ExecuteAsync(insertFileSql, uploadedFile, transaction: transaction);

            // Insert revision (must always be inserted)
            await con.ExecuteAsync(insertRevisionSql, fileRevision, transaction: transaction);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<UploadedFile> GetUploadedFileAsync(Guid fileId)
    {
        const string sql = """
                            WITH latest_revisions AS (
                                SELECT DISTINCT ON (fr.uploadedfileid)
                                    fr.uploadedfileid,
                                    fr.revisionid,
                                    fr.updatedbyid,
                                    fr.updatedon,
                                    fr.trackname,
                                    fr.albumname,
                                    fr.artistname,
                           	        fr.description,
                           	        fr.changesummary
                                FROM filerevisions fr
                                ORDER BY fr.uploadedfileid, fr.revisionid DESC
                            ),
                            ratings AS (
                                SELECT uploadedfileid, SUM(score) AS rating
                                FROM ratings
                                GROUP BY uploadedfileid
                            )
                            SELECT uf.id,
                                   uf.relativepath,
                                   uf.hash,
                                   uf.uploadedon,
                                   uf.uploadedby AS uploadedbyid,
                                   COALESCE(r.rating, 0) AS rating,
                                   lr.revisionid,
                                   lr.updatedbyid,
                                   lr.updatedon,
                                   lr.trackname,
                                   lr.albumname,
                                   lr.artistname,
                           	       lr.description,
                           	       uf.originalname,
                           	       uf.filesize,
                           	       lr.changesummary,
                           	       uf.locked
                           	       FROM uploadedfiles uf
                            INNER JOIN latest_revisions lr ON uf.id = lr.uploadedfileid
                            LEFT JOIN ratings r ON uf.id = r.uploadedfileid
                            WHERE uf.id = @Id
                            ORDER BY uf.uploadedon DESC
                           """;

        return (await FetchUploadedFilesAsync(sql, new { Id = fileId })).First();
    }

    public async Task<UploadedFile> GetUploadedFileWithSpecificRevisionAsync(Guid fileId, int revision)
    {
        const string sql = """
                               WITH ratings AS (
                                   SELECT uploadedfileid, SUM(score) AS rating
                                   FROM ratings
                                   GROUP BY uploadedfileid
                               )
                               SELECT uf.id,
                                      uf.relativepath,
                                      uf.hash,
                                      uf.uploadedon,
                                      uf.uploadedby AS uploadedbyid,
                                      COALESCE(r.rating, 0) AS rating,
                                      fr.revisionid,
                                      fr.updatedbyid,
                                      fr.updatedon,
                                      fr.trackname,
                                      fr.albumname,
                                      fr.artistname,
                                      fr.changesummary,
                                      uf.originalname,
                                      uf.filesize,
                                      fr.description,
                                      uf.locked
                               FROM uploadedfiles uf
                               INNER JOIN filerevisions fr 
                                   ON uf.id = fr.uploadedfileid AND fr.revisionid = @Revision
                               LEFT JOIN ratings r ON uf.id = r.uploadedfileid
                               WHERE uf.id = @FileId;
                           """;

        var rows = await FetchUploadedFilesAsync(sql, new
        {
            FileId = fileId,
            Revision = revision
        });

        var file = rows.FirstOrDefault();
        if (file is null)
        {
            throw new ArgumentOutOfRangeException(
                nameof(revision),
                $"Revision {revision} does not exist for file {fileId}");
        }

        return file;
    }

    public async Task<List<SummarizedRevision>> GetRevisionsAsync(Guid fileId)
    {
        const string sql = """
                           SELECT 
                               fr.revisionid,
                               fr.updatedon,
                               fr.changesummary,
                               u.id AS Id,
                               u.displayname AS DisplayName,
                               u.trustlevel AS TrustLevel
                           FROM filerevisions fr
                           INNER JOIN users u
                               ON fr.updatedbyid = u.id
                           WHERE fr.uploadedfileid = @Id;
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();

        var result = await con.QueryAsync<SummarizedRevision, User, SummarizedRevision>(
            sql,
            (revision, user) =>
            {
                revision.UpdatedBy = user;
                return revision;
            },
            new { Id = fileId },
            splitOn: "Id" // tells Dapper where the User object starts
        );

        return result.ToList();
    }

    public async Task<UploadedFile?> GetUploadedFilePureAsync(Guid fileId)
    {
        const string sql = """
                           SELECT * FROM uploadedfiles
                           WHERE uploadedfiles.id = @Id
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();
        var result = (await con.QueryAsync<UploadedFileRowPure>(sql, new { Id = fileId })).ToList();
        if (result.Count == 0)
            return null;

        var first = result.First();

        return new UploadedFile()
        {
            Hash = first.Hash,
            RelativePath = first.RelativePath,
            UploadedOn = first.UploadedOn,
            Id = first.Id,
            UploadedById = first.UploadedById,
        };
    }

    public async Task<bool> FileExistsAsync(Guid fileId)
    {
        const string sql = """
                           SELECT 1 FROM uploadedfiles
                           WHERE uploadedfiles.id = @Id
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();
        var result = await con.ExecuteScalarAsync<int?>(sql, new { Id = fileId });
        return result.HasValue;
    }

    public async Task<int?> GetRatingForUserForFileAsync(Guid fileId, Guid userId)
    {
        const string sql = """
                           SELECT score FROM ratings
                           WHERE ratings.userid = @UserId AND ratings.uploadedfileid = @FileId
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();
        var score = await con.ExecuteScalarAsync<int?>(sql, new { UserId = userId, FileId = fileId });
        return score;
    }

    public async Task RateFileAsync(Guid fileId, Guid userId, int score)
    {
        const string sql = """
                           INSERT INTO ratings (userid, uploadedfileid, score)
                           VALUES (@UserId, @FileId, @Score)
                           ON CONFLICT (userid, uploadedfileid)
                           DO UPDATE SET score = EXCLUDED.score;
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();
        await con.ExecuteAsync(sql, new { UserId = userId, FileId = fileId, Score = score });
    }

    public async Task RemoveRatingAsync(Guid fileId, Guid userId)
    {
        const string sql = """
                           DELETE FROM ratings
                           WHERE userid = @UserId AND uploadedfileid = @FileId;
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();
        await con.ExecuteAsync(sql, new { UserId = userId, FileId = fileId });
    }

    public async Task<Guid[]> GetAllIdsAsync()
    {
        const string sql = """
                           SELECT id FROM uploadedfiles;
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();
        return (await con.QueryAsync<Guid>(sql)).ToArray();
    }

    public async Task EditFileAsync(Guid fileId, FileRevision fileRevision)
    {
        const string nextRevisionSql = """
                                       SELECT MAX(revisionid)
                                       FROM filerevisions
                                       WHERE uploadedfileid = @FileId;
                                       """;

        await using var con = await _context.DataSource.OpenConnectionAsync();

        var nextRevisionId = await con.ExecuteScalarAsync<int?>(nextRevisionSql, new { FileId = fileId }) ?? 0;
        nextRevisionId++; // increment for the new revision

        const string insertSql = """
                                 INSERT INTO filerevisions 
                                 (uploadedfileid, revisionid, updatedbyid, updatedon, trackname, albumname, artistname, description, changesummary)
                                 VALUES (@UploadedFileId, @RevisionId, @UpdatedById, @UpdatedOn, @TrackName, @AlbumName, @ArtistName, @Description, @ChangeSummary);
                                 """;

        await con.ExecuteAsync(insertSql, new
        {
            UploadedFileId = fileId,
            RevisionId = nextRevisionId,
            UpdatedById = fileRevision.UpdatedById,
            UpdatedOn = fileRevision.UpdatedOn,
            TrackName = fileRevision.TrackName,
            AlbumName = fileRevision.AlbumName,
            ArtistName = fileRevision.ArtistName,
            Description = fileRevision.Description ?? string.Empty,
            ChangeSummary = fileRevision.ChangeSummary,
        });
    }

    public async Task<FilePreview?> GetFilePreviewOrNullAsync(Guid fileId)
    {
        const string sql = """
                           SELECT *
                           FROM filepreviews
                           WHERE uploadedfileid = @FileId;
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();
        return await con.QueryFirstOrDefaultAsync<FilePreview>(sql, new { FileId = fileId });
    }

    public async Task AddFilePreviewAsync(Guid fileId, FilePreview preview)
    {
        const string sql = """
                           INSERT INTO filepreviews (uploadedfileid, supportedextensions, generatedat, regenerate)
                           VALUES (@UploadedFileId, @SupportedExtensions, @GeneratedAt, @Regenerate)
                           ON CONFLICT (uploadedfileid) 
                           DO UPDATE SET supportedextensions = (
                               SELECT ARRAY(
                                   SELECT DISTINCT unnest(fp.supportedextensions || EXCLUDED.supportedextensions)
                                   ORDER BY 1
                               )
                               FROM filepreviews fp
                               WHERE fp.uploadedfileid = EXCLUDED.uploadedfileid
                           ),
                           generatedat = EXCLUDED.generatedat,
                           regenerate = EXCLUDED.regenerate;
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();
        await con.ExecuteAsync(sql, new
        {
            UploadedFileId = fileId,
            SupportedExtensions = preview.SupportedExtensions,
            GeneratedAt = preview.GeneratedAt,
            Regenerate = preview.Regenerate
        });
    }

    public async Task<Guid[]> GetMissingPreviews(string[] requiredExtensions)
    {
        const string sql = """
                           SELECT uf.id
                           FROM uploadedfiles uf
                           LEFT JOIN filepreviews fp 
                                  ON uf.id = fp.uploadedfileid
                           WHERE fp.uploadedfileid IS NULL
                              OR NOT (fp.supportedextensions @> @RequiredExtensions);
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();
        return (await con.QueryAsync<Guid>(sql, new
        {
            RequiredExtensions = requiredExtensions
        })).ToArray();
    }

    public async Task<Guid[]> GetFilesNeedingRegenerationAsync()
    {
        const string sql = """
                           SELECT uploadedfileid
                           FROM filepreviews
                           WHERE array_length(regenerate, 1) > 0;
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();
        return (await con.QueryAsync<Guid>(sql)).ToArray();
    }

    /// <summary>
    /// Returns a "score" of sorts for a user.
    /// The math works out like so:
    ///  - a file is worth 5
    ///  - a file revision is worth 1
    ///  - a ban placed on the user is worth -50
    /// </summary>
    public async Task<int> GetContributionsForUserAsync(Guid userId)
    {
        const string sql = """
                           WITH file_count AS (
                               SELECT COUNT(*) AS count
                               FROM uploadedfiles
                               WHERE uploadedby = @UserId
                           ),
                           revision_count AS (
                               SELECT COUNT(*) AS count
                               FROM filerevisions
                               WHERE updatedbyid = @UserId
                           ),
                           ban_count AS (
                               SELECT COUNT(*) AS count
                               FROM bans
                               WHERE affecteduser = @UserId
                           )
                           SELECT 
                               (fc.count * 5) + (rc.count * 1) - (bc.count * 50) AS total
                           FROM file_count fc, revision_count rc, ban_count bc;
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();
        var total = await con.ExecuteScalarAsync<int>(sql, new { UserId = userId });
        return total;
    }

    // Private helper method
    private async Task<List<UploadedFile>> FetchUploadedFilesAsync(string sql, object? parameters = null)
    {
        await using var con = await _context.DataSource.OpenConnectionAsync();

        var rows = await con.QueryAsync<UploadedFileRow>(sql, parameters);

        var result = rows.Select(r => new UploadedFile
        {
            Id = r.Id,
            RelativePath = r.RelativePath,
            Hash = r.Hash,
            UploadedOn = r.UploadedOn,
            UploadedById = r.UploadedById,
            Rating = r.Rating,
            FileSize = r.FileSize,
            OriginalName = r.OriginalName,
            Locked = r.Locked,
            FileRevisions = new List<FileRevision>
            {
                new FileRevision
                {
                    UploadedFileId = r.Id,
                    RevisionId = r.RevisionId,
                    UpdatedById = r.UpdatedById,
                    UpdatedOn = r.UpdatedOn,
                    TrackName = r.TrackName,
                    AlbumName = r.AlbumName,
                    ArtistName = r.ArtistName,
                    Description = r.Description ?? string.Empty,
                    ChangeSummary = r.ChangeSummary,
                }
            }
        }).ToList();

        return result;
    }

    private class UploadedFileRowPure
    {
        public Guid Id { get; set; }
        public string RelativePath { get; set; } = null!;
        public string Hash { get; set; } = null!;
        public DateTime UploadedOn { get; set; }
        public Guid UploadedById { get; set; }
    }

    private class UploadedFileRow
    {
        public Guid Id { get; set; }
        public string RelativePath { get; set; } = null!;
        public string Hash { get; set; } = null!;
        public DateTime UploadedOn { get; set; }
        public Guid UploadedById { get; set; }
        public int Rating { get; set; }
        public bool Locked { get; set; }

        public int FileSize { get; set; }
        public string OriginalName { get; set; } = null!;

        public int RevisionId { get; set; }
        public Guid UpdatedById { get; set; }
        public DateTime UpdatedOn { get; set; }
        public string TrackName { get; set; } = null!;
        public string? AlbumName { get; set; }
        public string? ArtistName { get; set; }
        public string? Description { get; set; }

        public string ChangeSummary { get; set; } = null!;
    }
}