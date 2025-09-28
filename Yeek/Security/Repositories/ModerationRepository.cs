using System.Globalization;
using Dapper;
using Npgsql;
using Yeek.Database;
using Yeek.FileHosting.Model;
using Yeek.Security.Model;

namespace Yeek.Security.Repositories;

public class ModerationRepository : IModerationRepository
{
    private readonly ApplicationDbContext _context;

    public ModerationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> CreateReportAsync(Guid reportee, string header, string body)
    {
        await using var con = await _context.DataSource.OpenConnectionAsync();
        await using var transaction = await con.BeginTransactionAsync();

        const string addTicketSql = """
                                    INSERT INTO tickets (reportee, header)
                                    VALUES (@reportee, @header)
                                    RETURNING id;
                                    """;

        try
        {
            var ticketId = await con.ExecuteScalarAsync<int>(
                addTicketSql,
                new { reportee, header },
                transaction: transaction
            );

            await InsertMessageIntoTicket(ticketId, body, reportee, transaction, con, false);
            await transaction.CommitAsync();
            return ticketId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<Ticket?> GetTicketOrNullAsync(int ticketId)
    {
        const string sql = """
                           SELECT 
                               tk.id,
                               tk.resolved,
                               tk.reportee AS ReporteeId,
                               tk.header,
                               u.id AS UserId,
                               u.displayname,
                               u.trustlevel
                           FROM tickets tk
                           INNER JOIN users u
                                ON tk.reportee = u.id
                           WHERE tk.id = @TicketId;
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();

        var result = await con.QueryAsync<Ticket, User, Ticket>(
            sql,
            (ticket, user) =>
            {
                ticket.Reportee = user;
                return ticket;
            },
            new { TicketId = ticketId },
            splitOn: "UserId"
        );

        return result.FirstOrDefault();
    }

    public async Task<List<TicketMessage>> GetMessagesForTicketAsync(int ticketId)
    {
        const string sql = """
                           SELECT 
                                tm.id, tm.sentbyid, tm.timesent, tm.content, tm.ticketid,
                                u.id AS UserId, u.displayname, u.trustlevel, u.id
                           FROM ticketmessages tm
                           INNER JOIN users u
                                ON tm.sentbyid = u.id
                           WHERE tm.ticketid = @TicketId
                           ORDER BY tm.timesent;
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();

        var result = await con.QueryAsync<TicketMessage, User, TicketMessage>(
            sql,
            (message, user) =>
            {
                message.SentBy = user;
                return message;
            },
            new { TicketId = ticketId },
            splitOn: "UserId"
        );

        return result.ToList();
    }

    public async Task ChangeTicketStatusAsync(int ticketId, bool resolved, Guid userId)
    {
        const string sql = """
                           UPDATE tickets SET resolved = @Resolved WHERE id = @Id
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();
        await using var transaction = await con.BeginTransactionAsync();

        try
        {
            await con.ExecuteAsync(
                sql,
                new { Resolved = resolved, Id = ticketId },
                transaction: transaction
            );

            var message = resolved ? "resolved" : "open";
            await InsertMessageIntoTicket(ticketId,$"Changed status to {message}!", userId, transaction, con, false);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task AddMessageToTicketAsync(int ticketId, Guid userId, string message)
    {
        await InsertMessageIntoTicket(ticketId, message, userId, notifyUser: false);
    }

    public async Task<List<Ticket>> GetAllTicketsBasicAsync(Guid? userIdFilter = null)
    {
        // TODO: This method might need paging in order to not fuck up load times for the reports page as a moderator.
        const string sqlNoFilter = """
                                   SELECT 
                                       t.id,
                                       t.resolved,
                                       t.reportee AS reporteeid,
                                       t.header,
                                       (
                                           SELECT MIN(tm.timesent)
                                           FROM ticketmessages tm
                                           WHERE tm.ticketid = t.id
                                       ) AS firstmessagetime
                                   FROM tickets t;
                                   """;
        const string sqlFilter = """
                                 SELECT 
                                     t.id,
                                     t.resolved,
                                     t.reportee AS reporteeid,
                                     t.header,
                                     (
                                         SELECT MIN(tm.timesent)
                                         FROM ticketmessages tm
                                         WHERE tm.ticketid = t.id
                                     ) AS firstmessagetime
                                 FROM tickets t
                                 WHERE t.reportee = @Reportee;
                                 """;

        await using var con = await _context.DataSource.OpenConnectionAsync();

        if (userIdFilter != null)
        {
            return (await con.QueryAsync<Ticket>(sqlFilter, new { Reportee = userIdFilter })).ToList();
        }
        else
        {
            return (await con.QueryAsync<Ticket>(sqlNoFilter)).ToList();
        }
    }

    public async Task<List<UserNote>> GetUserNotesAsync(Guid userId)
    {
        const string sql = """
                           SELECT 
                               un.id,
                               un.affecteduserid,
                               un.createdbyuserid,
                               un.content,
                               un.createdat,
                               au.id AS AffectedUserId,
                               au.displayname AS AffectedDisplayName,
                               au.trustlevel AS AffectedTrustLevel,
                               cu.id AS CreatedByUserId,
                               cu.displayname AS CreatedByDisplayName,
                               cu.trustlevel AS CreatedByTrustLevel
                           FROM usernotes un
                           INNER JOIN users au ON un.affecteduserid = au.id
                           INNER JOIN users cu ON un.createdbyuserid = cu.id
                           WHERE un.affecteduserid = @UserId
                           ORDER BY un.createdat DESC;
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();

        var result = await con.QueryAsync<UserNote, User, User, UserNote>(
            sql,
            (note, affectedUser, createdByUser) =>
            {
                note.AffectedUser = affectedUser;
                note.CreatedByUser = createdByUser;
                return note;
            },
            new { UserId = userId },
            splitOn: "AffectedUserId,CreatedByUserId"
        );

        return result.ToList();
    }

    public async Task AddUserNoteAsync(Guid actingUser, Guid userId, string note)
    {
        await AddNote(actingUser, userId, note);
    }

    public async Task AddBanAsync(Guid actingUser, Guid userId, DateTime formExpires, string formReason)
    {
        const string sql = """
                           INSERT INTO bans(AffectedUser, ExpiresAt, IssuerId)
                           VALUES (@AffectedUser, @ExpiresAt, @IssuerId)
                           """;

        await using var con = await _context.DataSource.OpenConnectionAsync();
        await using var transaction = await con.BeginTransactionAsync();

        try
        {
            await con.ExecuteAsync(
                sql,
                new
                {
                    AffectedUser = userId,
                    ExpiresAt = formExpires,
                    IssuerId = actingUser,
                },
                transaction: transaction
            );

            await AddNote(actingUser, userId, $"Banned user until {formExpires.ToString("F", CultureInfo.InvariantCulture)}", transaction, con);
            await ChangeTrustLevel(actingUser, userId, -1, transaction, con, false);
            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task ChangeTrustLevelAsync(Guid actingUser, Guid userId, int formTrustLevel)
    {
        await ChangeTrustLevel(actingUser, userId, formTrustLevel);
    }

    public async Task<DateTime?> GetLatestBanExpireOrNullForUserAsync(Guid userId)
    {
        const string sql = """
                         SELECT expiresat FROM bans WHERE affecteduser = @Id ORDER BY id LIMIT 1
                         """;

        await using var con = await _context.DataSource.OpenConnectionAsync();

        return await con.QuerySingleOrDefaultAsync<DateTime?>(sql, new { Id = userId });
    }

    private async Task ChangeTrustLevel(Guid actingUser, Guid userId, int trustLevel,
        NpgsqlTransaction? transaction = null, NpgsqlConnection? connection = null, bool addNote = true)
    {
        const string sql = """
                           UPDATE users SET trustlevel = @TrustLevel WHERE Id = @Id;
                           """;

        var suppliedCon = true;
        try
        {
            if (connection == null)
            {
                connection = await _context.DataSource.OpenConnectionAsync();
                suppliedCon = false;
            }

            var args = new
            {
                Id = userId,
                TrustLevel = trustLevel,
            };
            await connection.ExecuteAsync(sql, args, transaction: transaction);

            if (addNote)
            {
                await AddNote(actingUser, userId, $"Changed trust level to {((TrustLevel)trustLevel).ToString()}", transaction, connection);
            }
        }
        finally
        {
            if (!suppliedCon && connection != null)
                await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// Helper method for adding a note to a user which supports transactions
    /// </summary>
    private async Task AddNote(Guid actingUser, Guid userId, string note,
        NpgsqlTransaction? transaction = null, NpgsqlConnection? connection = null)
    {
        if (transaction != null && connection == null)
            throw new InvalidOperationException("When providing a transaction, provide the connection too.");

        const string insertSql = """
                                 INSERT INTO usernotes(Id, AffectedUserId, CreatedByUserId, Content, CreatedAt)
                                 VALUES (@Id, @UserId, @ActingUser, @Note, @CreatedAt)
                                 """;

        // We will need to manually dispose our connection properly. If we didn't get one
        var suppliedCon = true;
        try
        {
            if (connection == null)
            {
                connection = await _context.DataSource.OpenConnectionAsync();
                suppliedCon = false;
            }

            var args = new
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ActingUser = actingUser,
                Note = note,
                CreatedAt = DateTime.UtcNow,
            };
            await connection.ExecuteAsync(insertSql, args, transaction: transaction);
        }
        finally
        {
            if (!suppliedCon && connection != null)
                await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// Helper method which inserts a message into a ticket
    /// </summary>
    private async Task InsertMessageIntoTicket(int ticketId, string content, Guid sentBy,
        NpgsqlTransaction? transaction = null, NpgsqlConnection? connection = null, bool notifyUser = true)
    {
        if (transaction != null && connection == null)
            throw new InvalidOperationException("When providing a transaction, provide the connection too.");

        const string insertSql = """
                                 INSERT INTO ticketmessages(id, sentbyid, timesent, content, ticketid)
                                 VALUES (@id, @sentBy, @timesent, @content, @ticketid);
                                 """;

        const string insertNotifySql = """
                                       INSERT INTO notifications (created, severity, userid)
                                       VALUES (@created, @severity, @userid)
                                       """;

        // We will need to manually dispose our connection properly. If we didn't get one
        var suppliedCon = true;
        try
        {
            if (connection == null)
            {
                connection = await _context.DataSource.OpenConnectionAsync();
                suppliedCon = false;
            }

            var args = new
            {
                Id = Guid.CreateVersion7(),
                SentBy = sentBy,
                TimeSent = DateTime.UtcNow,
                Content = content,
                TicketId = ticketId
            };
            await connection.ExecuteAsync(insertSql, args, transaction: transaction);

            if (notifyUser)
            {
                throw new NotImplementedException(); // notifications are like kinda not working yet
                //await connection.ExecuteAsync(insertNotifySql, new
                //{
                //    Created = DateTime.UtcNow,
                //    Severity = 1, // Ticket severity
                //    UserId = ticketBy
                //}, transaction: transaction);
            }
        }
        finally
        {
            if (!suppliedCon && connection != null)
                await connection.DisposeAsync();
        }
    }
}