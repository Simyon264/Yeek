using System.ComponentModel.DataAnnotations;
using Yeek.FileHosting.Model;

namespace Yeek.Security.Model;

public sealed class User
{
    public Guid Id { get; set; }

    [MaxLength(300)]
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// The notifications of the user
    /// </summary>
    /// <remarks>
    /// This is null unless explicitly requested.
    /// </remarks>
    public ICollection<Notification>? Notifications { get; set; }
    /// <summary>
    /// The ratings this user has made.
    /// </summary>
    /// <remarks>
    /// This is null unless explicitly requested.
    /// </remarks>
    public ICollection<Rating>? Ratings { get; set; }

    public TrustLevel TrustLevel { get; set; }
    public DateTime LastLogin { get; set; }
}

public enum TrustLevel
{
    /// <summary>
    /// Can't do anything
    /// </summary>
    Banned = -1,
    /// <summary>
    /// Normal user, first logins will get this role.
    /// </summary>
    Normal = 0,
    /// <summary>
    /// Trusted users. People who moderators deem worthy of a title.
    /// </summary>
    Trusted = 1,
    /// <summary>
    /// Trusted people to watch over everything. Are allowed to also replace file contents of a file. Also can delete files.
    /// </summary>
    Moderator = 2,
    /// <summary>
    /// sudo make me a sandwich
    /// </summary>
    Admin = 3
}