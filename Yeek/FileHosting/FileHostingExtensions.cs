using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Yeek.FileHosting;

public static class FileHostingExtensions
{
    public static void UseFileHosting(this WebApplication app)
    {
        app.MapPost("/upload/midi",
                async (ClaimsPrincipal user, FileService fileService, HttpContext context, IAntiforgery antiforgery,
                    [AsParameters] MidiUploadForm form) =>
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

                        return await fileService.UploadFile(form, user);
                    }
                    catch (AntiforgeryValidationException)
                    {
                        return TypedResults.BadRequest("Invalid anti-forgery token");
                    }
                })
            .DisableAntiforgery()
            .RequireAuthorization();

        app.MapPatch("/upload/midi",
                async (ClaimsPrincipal user, FileService fileService, HttpContext context, IAntiforgery antiforgery,
                    [AsParameters] MidiUploadForm form) =>
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
            .RequireAuthorization();

        app.MapGet("/download/{file:guid}", async (Guid file, FileService fileService)
            => await fileService.GetFileAsResult(file));

        app.MapGet("/preview/{file:guid}/{extension}",
            async (Guid file, string extension, FileService fileService)
                => await fileService.GetFilePreviewAsResult(file, extension));

        app.MapPatch("vote",
            async (FileService fileService, ClaimsPrincipal user, [FromQuery] int score, [FromQuery] Guid file)
                => await fileService.VoteAsResult(score, file, user));
    }
}