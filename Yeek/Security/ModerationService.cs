using System.Security.Claims;
using Yeek.Configuration;
using Yeek.FileHosting;
using Yeek.FileHosting.Model;
using Yeek.FileHosting.Repositories;
using Yeek.Security.Forms;
using Yeek.Security.Model;
using Yeek.Security.Repositories;
using Yeek.WebDAV;

namespace Yeek.Security;

public class ModerationService
{
    private readonly IFileRepository _fileRepository;
    private readonly ILogger<FileService> _logger;
    private readonly WebDavManager _webDavManager;
    private readonly IModerationRepository _moderationRepository;
    private readonly IUserRepository _userRepository;

    public ModerationService(IFileRepository context, ILogger<FileService> logger, WebDavManager webDavManager, IModerationRepository moderationRepository, IUserRepository userRepository)
    {
        _logger = logger;
        _fileRepository = context;
        _webDavManager = webDavManager;
        _moderationRepository = moderationRepository;
        _userRepository = userRepository;
    }

    public async Task<IResult> CreateReport(CreateReportForm form, ClaimsPrincipal user)
    {
        var userId = user.Claims.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        var ticketId = await _moderationRepository.CreateReportAsync(userId.Value, form.Header, form.Description);
        return Results.Redirect($"/moderation/report/{ticketId}", false);
    }

    public async Task<IResult> ChangeReportStatus(ClaimsPrincipal user, int ticketId, int ticketstatus)
    {
        var userId = user.Claims.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        var userObject = await _userRepository.GetUserAsync(userId.Value);
        if (userObject!.TrustLevel < TrustLevel.Moderator)
            return Results.Unauthorized();

        var ticket = await _moderationRepository.GetTicketOrNullAsync(ticketId);
        if (ticket == null)
            return Results.NotFound();

        if (ticketstatus is > 1 or < 0)
            return Results.BadRequest("Ticket status does not resolve to status.");


        await _moderationRepository.ChangeTicketStatusAsync(ticketId, ticketstatus == 0, userId.Value);

        return Results.Redirect($"/moderation/report/{ticketId}", false);
    }

    public async Task<IResult> ReplyToTicket(ClaimsPrincipal user, int ticketId, string content)
    {
        var userId = user.Claims.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        var ticket = await _moderationRepository.GetTicketOrNullAsync(ticketId);
        if (ticket == null)
            return Results.NotFound();

        var userObject = await _userRepository.GetUserAsync(userId.Value);

        if (ticket.ReporteeId != userId.Value && userObject!.TrustLevel < TrustLevel.Moderator)
            return Results.NotFound(); // We send a not found in order to not leak any tickets

        await _moderationRepository.AddMessageToTicketAsync(ticketId, userId.Value, content);
        return Results.Redirect($"/moderation/report/{ticketId}", false);
    }

    public async Task<IResult> AddNote(ClaimsPrincipal user, Guid id, NoteForm form)
    {
        var userId = user.Claims.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        var userObject = await _userRepository.GetUserAsync(userId.Value);
        if (userObject!.TrustLevel < TrustLevel.Moderator)
            return Results.Unauthorized();

        if (!await _userRepository.UserExists(id))
            return Results.BadRequest("User does not exist.");

        await _moderationRepository.AddUserNoteAsync(userId.Value, id, form.Content);

        return Results.Redirect($"/moderation/users/{id}", false);
    }

    public async Task<IResult> ChangeTrustLevel(ClaimsPrincipal user, Guid id, TrustLevelForm form)
    {
        var userId = user.Claims.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        var userObject = await _userRepository.GetUserAsync(userId.Value);
        if (userObject!.TrustLevel < TrustLevel.Moderator)
            return Results.Unauthorized();

        if (!await _userRepository.UserExists(id))
            return Results.BadRequest("User does not exist.");

        var affectedUser = await _userRepository.GetUserAsync(id);

        if (userObject.TrustLevel <= affectedUser.TrustLevel)
        {
            return Results.Text($"You need to be of trust level {affectedUser.TrustLevel + 1} to change this users trust level.", statusCode: 403);
        }

        if ((int)userObject.TrustLevel <= form.TrustLevel)
        {
            return Results.Text("Cannot assign trust levels above or equal to your own.", statusCode: 403);
        }

        if (form.TrustLevel == -1)
        { // Ban
            await _moderationRepository.AddBanAsync(userId.Value, id, form.ExpiresAt!.Value.ToUniversalTime(), form.Reason!);
        }
        else
        {
            await _moderationRepository.ChangeTrustLevelAsync(userId.Value, id, form.TrustLevel);
        }

        return Results.Redirect($"/moderation/users/{id}", false);
    }

    public async Task<IResult> MarkNotificationAsRead(ClaimsPrincipal user, int notificationId)
    {
        var userId = user.Claims.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        await _userRepository.SetNotificationRead(userId.Value, notificationId);

        return Results.NoContent();
    }
}