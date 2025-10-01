using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Yeek.FileHosting.Model;

public class FileRevision
{
    [NotMapped]
    public (Guid, int) Id => (UploadedFileId, RevisionId);

    public Guid UploadedFileId { get; set; }
    public int RevisionId { get; set; }
    public Guid UpdatedById { get; set; }
    public DateTime UpdatedOn { get; set; }

    /// <summary>
    /// The name of the track
    /// </summary>
    [MaxLength(200)]
    public string TrackName { get; set; } = null!;

    /// <summary>
    /// Optionally, the album name.
    /// </summary>
    public string? AlbumName { get; set; } = null!;

    /// <summary>
    /// Optionally, the artist / band name who made this.
    /// </summary>
    public string? ArtistName => ArtistNames.Length == 0 ? null : string.Join(", ", ArtistNames) ;
    public string[] ArtistNames { get; set; } = null!;

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; set; } = string.Empty;

    /// <summary>
    /// What did this change do?
    /// </summary>
    public string ChangeSummary { get; set; } = null!;
}