using System.ComponentModel.DataAnnotations;

namespace Yeek.Security.Forms;

public class CreateReportForm
{
    [Required, StringLength(400, ErrorMessage = "Header may only be 400 characters long")]
    public string Header { get; set; }

    [Required]
    public string Description { get; set; }
}