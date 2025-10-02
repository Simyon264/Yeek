namespace Yeek.Security.Model;

/// <summary>
/// A notification for a specific user.
/// </summary>
public class Notification
{
    public int Id { get; set; }

    /// <summary>
    /// If the user acknowledged the notification
    /// </summary>
    public bool Read { get; set; }

    /// <summary>
    /// When this notification was created
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// The severity of the notification. Determines what color it gets in the UI
    /// </summary>
    public Severity Severity { get; set; } = Severity.Generic;
    public NotificationType ContentType { get; set; }
    public string[] Payload { get; set; } = null!;

    public Guid UserId { get; set; }
}

public enum Severity : byte
{
    /// <summary>
    /// Default. No bells no whistles.
    /// </summary>
    Generic = 0,

    /// <summary>
    /// this was a notifiaction relating to a report.
    /// </summary>
    Ticket = 1,
}

/// <summary>
/// Defines what message to use
/// </summary>
public enum NotificationType : byte
{
    Custom = 0,
    TicketAnswered = 1,
    ContentRemoved = 2,
    Banned = 3,
    TrustChanged = 4,
}