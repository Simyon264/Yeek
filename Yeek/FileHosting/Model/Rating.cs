using System.ComponentModel.DataAnnotations.Schema;
using Yeek.Security.Model;

namespace Yeek.FileHosting.Model;

public class Rating
{
    [NotMapped]
    public (Guid, Guid) Id => (UserId, UploadedFileId);

    public Guid UserId { get; set; }
    public Guid UploadedFileId { get; set; }

    public int Score { get; set; }
}