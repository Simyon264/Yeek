using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace Yeek.FileHosting;

public partial class MidiUploadForm : IValidatableObject
{
    [FromForm]
    public Guid? Id { get; set; }

    [FromForm]
    [StringLength(400, ErrorMessage = "Change Summary may only contain 400 characters.")]
    public string? ChangeSummary { get; set; }

    [FromForm]
    [Required, StringLength(200, ErrorMessage = "Track Name may only contain 200 characters.")]
    public string Trackname { get; set; }
    [FromForm]
    [StringLength(200, ErrorMessage = "Album Name may only contain 200 characters.")]
    public string? Albumname { get; set; }
    [FromForm]
    [StringLength(200, ErrorMessage = "Artist Name may only contain 200 characters.")]
    public string? Authorname { get; set; }
    [FromForm]
    [StringLength(4000, ErrorMessage = "Description may only contain 4000 characters.")]
    public string? Description { get; set; }

    public IFormFile? File { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var invalidCharsRegex = InvalidCharactersRegex();

        // Check Trackname
        if (invalidCharsRegex.IsMatch(Trackname ?? string.Empty))
        {
            yield return new ValidationResult(
                "Track Name contains invalid characters (\\ / : * ? \" < > |).",
                [nameof(Trackname)]);
        }

        // Check Albumname
        if (!string.IsNullOrEmpty(Albumname) && invalidCharsRegex.IsMatch(Albumname))
        {
            yield return new ValidationResult(
                "Album Name contains invalid characters (\\ / : * ? \" < > |).",
                [nameof(Albumname)]);
        }

        // Check Authorname
        if (!string.IsNullOrEmpty(Authorname) && invalidCharsRegex.IsMatch(Authorname))
        {
            yield return new ValidationResult(
                "Artist Name contains invalid characters (\\ / : * ? \" < > |).",
                [nameof(Authorname)]);
        }

        if (Id != null) // this is a patch
        {
            if (string.IsNullOrEmpty(ChangeSummary))
            {
                yield return new ValidationResult(
                    "Summary is required",
                    [nameof(ChangeSummary)]
                );
            }
        } else if (File == null)
        {
            yield return new ValidationResult(
                "File is required",
                [nameof(File)]
            );
        }
    }

    [GeneratedRegex(@"[\\/:*?""<>|]")]
    private static partial Regex InvalidCharactersRegex();
}