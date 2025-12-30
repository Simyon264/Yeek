using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using JetBrains.Annotations;
using Yeek.Core;

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
    public bool Locked { get; set; }
    public int Downloads { get; set; }
    public int Plays { get; set; }

    public int TotalDownloads =>
        Downloads + Plays;

    public int? DeletedId { get; set; }

    public FileRevision MostRecentRevision => FileRevisions.First();

    /// <summary>
    /// Gets a "short" id based of the actual ID. This is guaranteed to be unique based on the actual ID.
    /// </summary>
    public string GetShortId()
        => UploadedFile.GetShortId(Id);

    /// <inheritdoc cref="GetShortId()"/>
    public static string GetShortId(Guid id)
    {
        const int length = 7;

        var bytes = id.ToByteArray();

        var base64 = Convert.ToBase64String(bytes);

        base64 = base64
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        return base64[..length];
    }

    public string GetMetaUrl(int rev = -1)
    {
        if (FileRevisions.Count == 0)
            throw new InvalidOperationException("Can't get meta url on incomplete file");

        var revision = MostRecentRevision;
        if (rev != -1)
        {
            revision = FileRevisions.ElementAt(rev);
        }

        var sb = new List<string>();
        sb.Add(GetShortId());
        if (revision.ArtistName != null)
            sb.Add(SlugHelper.GenerateSlug(revision.ArtistNames[0]));
        else
            sb.Add("-");

        if (revision.AlbumName != null)
            sb.Add(SlugHelper.GenerateSlug(revision.AlbumName, 40));
        else
            sb.Add("-");

        sb.Add(SlugHelper.GenerateSlug(revision.TrackName));

        return string.Join('/', sb);
    }


    public string GetDownloadName(bool includeId = false)
    {
        if (FileRevisions.Count == 0)
            throw new InvalidOperationException("Can't get download name on incomplete file.");

        var sb = new List<string>();
        if (MostRecentRevision.ArtistName != null)
            sb.Add(MostRecentRevision.ArtistName);
        if (MostRecentRevision.AlbumName != null)
            sb.Add(MostRecentRevision.AlbumName);

        sb.Add(MostRecentRevision.TrackName);

        var id = string.Empty;
        if (includeId)
            id = $" ({GetShortId()})";
        return $"{string.Join('_', sb)}{id}.mid";
    }
}