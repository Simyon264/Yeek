using System.ComponentModel.DataAnnotations;
using System.Text;
using Yeek.Security.Model;

namespace Yeek.FileHosting.Model;

public class UploadedFile
{
    public Guid Id { get; set; }

    [MaxLength(260)]
    public required string RelativePath { get; set; }

    public string OriginalName { get; set; } = string.Empty;
    public long FileSize { get; set; } = 0;

    /// <summary>
    /// The contents of this file, hashed.
    /// </summary>
    public required string Hash { get; set; }

    public Guid UploadedById { get; set; }

    public DateTime UploadedOn { get; set; }

    public ICollection<FileRevision> FileRevisions { get; set; } = new List<FileRevision>();
    public int? Rating { get; set; }

    public FileRevision MostRecentRevision => FileRevisions.First();

    public string GetDownloadName()
    {
        if (FileRevisions.Count == 0)
            throw new InvalidOperationException("Can't get download name on incomplete file.");

        var sb = new List<string>();
        if (MostRecentRevision.ArtistName != null)
            sb.Add(MostRecentRevision.ArtistName);
        if (MostRecentRevision.AlbumName != null)
            sb.Add(MostRecentRevision.AlbumName);

        sb.Add(MostRecentRevision.TrackName);

        return $"{string.Join('_', sb)}.midi";
    }
}