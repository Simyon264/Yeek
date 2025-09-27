namespace Yeek.FileHosting.Model;

public class FilePreview
{
    public Guid UploadedFileId { get; set; }
    public string[] SupportedExtensions { get; set; }
    public DateTime GeneratedAt { get; set; }
}