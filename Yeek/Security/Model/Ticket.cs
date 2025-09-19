using Yeek.Security.Repositories;

namespace Yeek.Security.Model;

public class Ticket
{
    public int Id { get; set; }
    public bool Resolved { get; set; }
    public Guid ReporteeId { get; set; }
    public User? Reportee { get; set; }

    public string Header { get; set; }

    /// <summary>
    /// The time of the first message. Only set in <see cref="IModerationRepository.GetAllTicketsBasicAsync"/>
    /// </summary>
    public DateTime? FirstMessageTime { get; set; }
}

public class TicketMessage
{
    public Guid Id { get; set; }

    public Guid SentById { get; set; }
    public User? SentBy { get; set; }

    public DateTime TimeSent { get; set; }
    public string Content { get; set; }
    public int TicketId { get; set; }
}