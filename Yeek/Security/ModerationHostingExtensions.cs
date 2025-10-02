using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Yeek.FileHosting;
using Yeek.Security.Forms;

namespace Yeek.Security;

public static class ModerationHostingExtensions
{
    public static void UseModerationHosting(this WebApplication app)
    {
        app.MapPost("/moderation/takedown/new",
                async (ClaimsPrincipal user, FileService moderationService, [FromForm] DeletionForm deletionForm) =>
                    await moderationService.DeleteFile(user, deletionForm))
            .RequireAuthorization();

        app.MapPatch("/notifications/{notificationId:int}/read",
            async (ClaimsPrincipal user, ModerationService moderationService, int notificationId) =>
                await moderationService.MarkNotificationAsRead(user, notificationId))
            .RequireAuthorization();

        app.MapPost("/moderation/users/{id:guid}/trust",
                async (ClaimsPrincipal user, ModerationService moderationService, HttpContext context, IAntiforgery antiforgery, Guid id, [FromForm] TrustLevelForm trustform) =>
                {
                    try
                    {
                        await antiforgery.ValidateRequestAsync(context);
                        var results = new List<ValidationResult>();
                        var validationContext = new ValidationContext(trustform);
                        var isValid = Validator.TryValidateObject(trustform, validationContext, results, validateAllProperties: true);
                        if (!isValid)
                        {
                            var errors = results
                                .GroupBy(r => r.MemberNames.FirstOrDefault() ?? string.Empty)
                                .ToDictionary(
                                    g => g.Key,
                                    g => g.Select(r => r.ErrorMessage).ToArray());

                            return Results.ValidationProblem(errors);
                        }

                        return await moderationService.ChangeTrustLevel(user, id, trustform);
                    }
                    catch (AntiforgeryValidationException)
                    {
                        return TypedResults.BadRequest("Invalid anti-forgery token");
                    }
                })
            .DisableAntiforgery()
            .RequireAuthorization();

        app.MapPost("/moderation/users/{id:guid}/note",
                async (ClaimsPrincipal user, ModerationService moderationService, HttpContext context, IAntiforgery antiforgery, Guid id, [FromForm] NoteForm noteform) =>
                {
                    try
                    {
                        await antiforgery.ValidateRequestAsync(context);
                        return await moderationService.AddNote(user, id, noteform);
                    }
                    catch (AntiforgeryValidationException)
                    {
                        return TypedResults.BadRequest("Invalid anti-forgery token");
                    }
                })
            .DisableAntiforgery()
            .RequireAuthorization();

        app.MapPost("/moderation/report/{id:int}/status",
            async (ClaimsPrincipal user, ModerationService moderationService, HttpContext context, IAntiforgery antiforgery, int id, [FromForm] int ticketstatus) =>
            {
                try
                {
                    await antiforgery.ValidateRequestAsync(context);
                    return await moderationService.ChangeReportStatus(user, id, ticketstatus);
                }
                catch (AntiforgeryValidationException)
                {
                    return TypedResults.BadRequest("Invalid anti-forgery token");
                }
            })
            .DisableAntiforgery()
            .RequireAuthorization();

        app.MapPost("/moderation/report/{id:int}",
            async (ClaimsPrincipal user, ModerationService moderationService, HttpContext context, IAntiforgery antiforgery, int id, [FromForm] string content) =>
            {
                try
                {
                    await antiforgery.ValidateRequestAsync(context);
                    return await moderationService.ReplyToTicket(user, id, content);
                }
                catch (AntiforgeryValidationException)
                {
                    return TypedResults.BadRequest("Invalid anti-forgery token");
                }
            })
            .DisableAntiforgery()
            .RequireAuthorization();

        app.MapPost("/moderation/report/new",
                async (ClaimsPrincipal user, ModerationService moderationService, HttpContext context, IAntiforgery antiforgery,
                    [FromForm] CreateReportForm form) =>
                {
                    try
                    {
                        await antiforgery.ValidateRequestAsync(context);
                        var results = new List<ValidationResult>();
                        var validationContext = new ValidationContext(form);
                        var isValid = Validator.TryValidateObject(form, validationContext, results, validateAllProperties: true);
                        if (!isValid)
                        {
                            var errors = results
                                .GroupBy(r => r.MemberNames.FirstOrDefault() ?? string.Empty)
                                .ToDictionary(
                                    g => g.Key,
                                    g => g.Select(r => r.ErrorMessage).ToArray());

                            return Results.ValidationProblem(errors);
                        }

                        return await moderationService.CreateReport(form, user);
                    }
                    catch (AntiforgeryValidationException)
                    {
                        return TypedResults.BadRequest("Invalid anti-forgery token");
                    }
                })
            .DisableAntiforgery()
            .RequireAuthorization();
    }
}
