using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Yeek.Security.Forms;

public class NoteForm
{
    [Required]
    public string Content { get; set; }
}

public class TrustLevelForm : IValidatableObject
{
    [Required]
    public int TrustLevel { get; set; }

    public string? Reason { get; set; }

    public string? Expires { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (TrustLevel == -1 && Reason == null && string.IsNullOrEmpty(Expires))
        {
            yield return new ValidationResult("When banning, a reason and an expires must be specified.");
        }

        if (TrustLevel is < -1 or > 3)
        {
            yield return new ValidationResult("Trust level must be between -1 and 3.");
        }

        if (Expires is not null && TrustLevel == -1)
        { // Parse datetime
            if (!DateTime.TryParse(Expires, out var expires))
            {
                yield return new ValidationResult("Failed to parse expire date.");
            }

            ExpiresAt = expires;
        }
    }
}