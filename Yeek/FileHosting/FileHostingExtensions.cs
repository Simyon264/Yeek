using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Yeek.FileHosting;

public static class FileHostingExtensions
{
    public static void UseFileHosting(this WebApplication app)
    {
        app.MapPost("/moderation/mass-edits/{fileId:guid}/revert",
                async (Guid fileId, ClaimsPrincipal user, FileService fileService)
                    => await fileService.RevertMassEdit(fileId, user))
            .RequireAuthorization();

        app.MapPost("/upload/midi",
                async (ClaimsPrincipal user, FileService fileService, HttpContext context, IAntiforgery antiforgery,
                    [FromForm] MidiUploadForm form) =>
                {
                    try
                    {
                        await antiforgery.ValidateRequestAsync(context);
                        var results = new List<ValidationResult>();
                        var validationContext = new ValidationContext(form);
                        var isValid = Validator.TryValidateObject(form, validationContext, results,
                            validateAllProperties: true);
                        if (!isValid)
                        {
                            var errors = results
                                .GroupBy(r => r.MemberNames.FirstOrDefault() ?? string.Empty)
                                .ToDictionary(
                                    g => g.Key,
                                    g => g.Select(r => r.ErrorMessage).ToArray());

                            return Results.ValidationProblem(errors);
                        }

                        return await fileService.UploadFile(form, user);
                    }
                    catch (AntiforgeryValidationException)
                    {
                        return TypedResults.BadRequest("Invalid anti-forgery token");
                    }
                })
            .DisableAntiforgery()
            .RequireAuthorization()
            .RequireRateLimiting("UploadPolicy");

        app.MapPatch("/upload/midi",
                async (ClaimsPrincipal user, FileService fileService, HttpContext context, IAntiforgery antiforgery,
                    [FromForm] MidiUploadForm form) =>
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

                        return await fileService.PatchFile(form, user);
                    }
                    catch (AntiforgeryValidationException)
                    {
                        return TypedResults.BadRequest("Invalid anti-forgery token");
                    }
                })
            .DisableAntiforgery()
            .RequireAuthorization()
            .RequireRateLimiting("UploadPolicy");

        app.MapGet("/download/{file:guid}", async (Guid file, FileService fileService)
            => await fileService.GetFileAsResult(file))
            .RequireRateLimiting("DownloadPolicy");

        app.MapGet("/preview/{file:guid}/{extension}",
            async (Guid file, string extension, FileService fileService)
                => await fileService.GetFilePreviewAsResult(file, extension));

        app.MapPatch("vote",
            async (FileService fileService, ClaimsPrincipal user, [FromQuery] int score, [FromQuery] Guid file)
                => await fileService.VoteAsResult(score, file, user))
            .RequireRateLimiting("VotePolicy");

        app.MapPost("/mass-edit/start", async ([FromQuery] bool apply, FileService fileService, ClaimsPrincipal user, HttpRequest req)
                => await fileService.StartMassEditJob(user, req, apply))
            .RequireAuthorization();

        app.MapGet("/mass-edit/stream/{jobId:guid}", async (FileService fileService, HttpContext req, Guid jobId)
            => await fileService.GetMassJobStream(req, jobId))
            .RequireAuthorization();

        app.MapGet("/playlists", async ([FromQuery] Guid file, FileService fileService, ClaimsPrincipal user)
            => await fileService.GetPlaylistsForFile(file, user))
            .RequireAuthorization();

        app.MapPost("/playlists", async ([FromQuery] string name, FileService fileService, ClaimsPrincipal user)
                => await fileService.CreatePlaylist(name, user))
            .RequireAuthorization();

        app.MapPatch("/playlists/entry", async ([FromQuery] Guid file, [FromQuery] Guid playlist, FileService fileService, ClaimsPrincipal user)
                => await fileService.AddOrRemoveToPlaylist(playlist, file, user))
            .RequireAuthorization();

        app.MapDelete("/playlists", async ([FromQuery] Guid list, FileService fileService, ClaimsPrincipal user)
                => await fileService.DeletePlaylist(list, user))
            .RequireAuthorization();
    }
}