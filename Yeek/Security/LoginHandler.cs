using System.Diagnostics;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Yeek.Configuration;
using Yeek.Database;
using Yeek.Security.Model;
using Yeek.Security.Repositories;

namespace Yeek.Security;

public class LoginHandler
{
    private readonly IUserRepository _userRepository;
    private readonly ServerConfiguration _configuration = new();

    public LoginHandler(IConfiguration configuration, IUserRepository userRepository)
    {
        _userRepository = userRepository;
        configuration.Bind(ServerConfiguration.Name, _configuration);
    }

    public async Task HandleTokenValidated(TokenValidatedContext ctx)
    {
        var identity = ctx.Principal?.Identities.FirstOrDefault(i => i.IsAuthenticated);

        if (identity == null)
            Debug.Fail("Unable to find identity.");

        var userId = identity.Claims.GetUserId();
        if (!userId.HasValue)
        {
            ctx.Fail("User id not present in principal");
            return;
        }

        if (await _userRepository.UserExists(userId.Value))
        {
            await _userRepository.NotifyLoginAsync(userId.Value);
            return;
        }

        var user = new User
        {
            Id = userId.Value,
            DisplayName = "New User"
        };

        await _userRepository.AddUserAsync(user);
        await _userRepository.NotifyLoginAsync(userId.Value);
    }

    public async Task HandleUserDataUpdate(UserInformationReceivedContext ctx)
    {
        var name = ctx.User.RootElement.GetProperty("name").GetString();
        if (name == null)
            return;

        var identity = ctx.Principal?.Identities.FirstOrDefault(i => i.IsAuthenticated);

        var userId = identity?.Claims.GetUserId();
        if (userId == null)
            return;

        var user = await _userRepository.UserExists(userId.Value);
        if (!user)
            return;

        await _userRepository.UpdateUserNameAsync(userId.Value, name);
    }
}