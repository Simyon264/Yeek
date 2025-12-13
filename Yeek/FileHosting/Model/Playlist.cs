using System.ComponentModel.DataAnnotations;
using Yeek.Security.Model;

namespace Yeek.FileHosting.Model;

public class Playlist
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; }

    public PlaylistVisibility Visibility { get; set; } = PlaylistVisibility.Public;

    [Required]
    public Guid UserId { get; set; }

    public PlaylistFileShareMode FileShareMode { get; set; } = 0;

    // Navigation properties
    public User User { get; set; }
    public ICollection<PlaylistEntry> Entries { get; set; }
}

public enum PlaylistVisibility
{
    Public = 0,
    Unlisted = 1,
    Private = 2
}

public enum PlaylistFileShareMode
{
    Unsorted = 0,
}

public class PlaylistEntry
{
    public int Id { get; set; }

    public Guid PlaylistId { get; set; }

    public Guid UploadedFileId { get; set; }

    public DateTime AddedToPlaylist { get; set; }
}