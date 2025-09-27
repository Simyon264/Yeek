using System.Security.Claims;
using Yeek.Core.Repositories;
using Yeek.Security;
using Yeek.Security.Model;
using Yeek.Security.Repositories;

namespace Yeek.Core;

public class AdministrationService
{
    private readonly IAdminRepository _adminRepository;
    private readonly IUserRepository _userRepository;

    public AdministrationService(IAdminRepository adminRepository, IUserRepository userRepository)
    {
        _adminRepository = adminRepository;
        _userRepository = userRepository;
    }

    public async Task<IResult> ToggleMessage(ClaimsPrincipal user, int id)
    {
        var userId = user.Claims.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        var userObject = await _userRepository.GetUserAsync(userId.Value);
        if (userObject!.TrustLevel < TrustLevel.Admin)
            return Results.Unauthorized();

        if (!await _adminRepository.ToggleGlobalMessageShowAsync(id))
            return Results.NotFound();

        return Results.Ok();
    }

    public async Task<IResult> EditMessageHeader(ClaimsPrincipal user, int id, string header)
    {
        var userId = user.Claims.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        var userObject = await _userRepository.GetUserAsync(userId.Value);
        if (userObject!.TrustLevel < TrustLevel.Admin)
            return Results.Unauthorized();

        if (!await _adminRepository.EditGlobalMessageHeaderAsync(id, header))
            return Results.NotFound();

        return Results.Ok();
    }

    public async Task<IResult> EditMessageContent(ClaimsPrincipal user, int id, string content)
    {
        var userId = user.Claims.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        var userObject = await _userRepository.GetUserAsync(userId.Value);
        if (userObject!.TrustLevel < TrustLevel.Admin)
            return Results.Unauthorized();

        if (!await _adminRepository.EditGlobalMessageContentAsync(id, content))
            return Results.NotFound();

        return Results.Ok();
    }

    public async Task<IResult> CreateMessage(ClaimsPrincipal user, string header, string content)
    {
        var userId = user.Claims.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        var userObject = await _userRepository.GetUserAsync(userId.Value);
        if (userObject!.TrustLevel < TrustLevel.Admin)
            return Results.Unauthorized();

        await _adminRepository.CreateGlobalMessageAsync(header, content);

        return Results.Redirect("/admin/messages");
    }

}