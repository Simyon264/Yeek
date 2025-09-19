using Yeek.Security.Model;

namespace Yeek.FileHosting.Model;

public class UserNote
{
    public Guid Id { get; set; }

    public Guid AffectedUserId { get; set; }
    public User? AffectedUser { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public string Content { get; set; }
    public DateTime CreatedAt { get; set; }
}