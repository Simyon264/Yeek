using Yeek.FileHosting.Model;

namespace Yeek.FileHosting;

public class DeletionForm
{
    public Guid FileId { get; set; }
    public string? AllowReupload { get; set; }
    public DeletionReason Reason { get; set; }

    public bool ParsedAllowReupload => AllowReupload == "on";
}