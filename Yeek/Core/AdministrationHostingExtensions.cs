using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Yeek.Core.Models;
using Yeek.Core.Repositories;

namespace Yeek.Core;

public static class AdministrationHostingExtensions
{
    public static void UseAdministrationHosting(this WebApplication app)
    {
        app.MapPatch("/admin/messages/{id:int}/toggle", async (
                AdministrationService adminService,
                ClaimsPrincipal user,
                int id) => await adminService.ToggleMessage(user, id))
            .RequireAuthorization();

        app.MapPatch("/admin/messages/{id:int}/header", async (
                    AdministrationService adminService,
                    ClaimsPrincipal user,
                    int id,
                    [FromBody] string header) =>
                await adminService.EditMessageHeader(user, id, header))
            .RequireAuthorization();

        app.MapPatch("/admin/messages/{id:int}/content", async (
                    AdministrationService adminService,
                    ClaimsPrincipal user,
                    int id,
                    [FromBody] string content) =>
                await adminService.EditMessageContent(user, id, content))
            .RequireAuthorization();

        app.MapPost("/admin/messages", async (
                    AdministrationService adminService,
                    ClaimsPrincipal user,
                    [FromForm] string header,
                    [FromForm] string content) =>
                await adminService.CreateMessage(user, header, content))
            .RequireAuthorization();
    }
}