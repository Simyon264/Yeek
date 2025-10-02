namespace Yeek.FileHosting.Model;

public class Deletion
{
    public int Id { get; set; }
    public required string Hash { get; set; }
    public bool AllowReupload { get; set; } = false;
    public DeletionReason Reason { get; set; }
    /// <summary>
    /// The user who originally uploaded this file
    /// </summary>
    public Guid UploadedById { get; set; }
    public Guid DeletedBy { get; set; }
    public DateTime DeletionTime { get; set; }
}

public enum DeletionReason : byte
{
    Unspecified = 0,
    UserRequest = 1,
    Takedown = 2,
}

public static class DeletionExtensions
{
    public static string GetNotificationReason(this DeletionReason deletion)
    {
        return deletion switch
        {
            DeletionReason.Unspecified => "Unspecified",
            DeletionReason.UserRequest => "Content removed upon user request.",
            DeletionReason.Takedown => "Takedown request by copyright holder.",
            _ => throw new ArgumentOutOfRangeException(nameof(deletion), deletion, null)
        };
    }
}