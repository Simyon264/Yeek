using Yeek.FileHosting.Model;
using Yeek.Security.Model;

namespace Yeek.Security.Repositories;

public interface IModerationRepository
{
    /// <summary>
    /// Creates a ticket and return an ID for the created ticket.
    /// </summary>
    public Task<int> CreateReportAsync(Guid reportee, string header, string body);

    public Task<Ticket?> GetTicketOrNullAsync(int ticketId);
    Task<List<TicketMessage>> GetMessagesForTicketAsync(int ticketId);
    Task ChangeTicketStatusAsync(int ticketId, bool resolved, Guid userId);
    Task AddMessageToTicketAsync(int ticketId, Guid userId, string message);
    Task<List<Ticket>> GetAllTicketsBasicAsync(Guid? userIdFilter = null);
    Task<List<UserNote>> GetUserNotesAsync(Guid userId);
    Task AddUserNoteAsync(Guid actingUser, Guid userId, string note);
    Task AddBanAsync(Guid actingUser, Guid userId, DateTime formExpires, string formReason);
    Task ChangeTrustLevelAsync(Guid actingUser, Guid userId, int formTrustLevel);
    Task<DateTime?> GetLatestBanExpireOrNullForUserAsync(Guid userId);
}