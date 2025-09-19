using Yeek.Core.Types;

namespace Yeek.Configuration;

public class FileConfiguration
{
    public const string Name = "Files";

    /// <summary>
    /// The directory where user uploaded files are stored.
    /// </summary>
    public string UserContentDirectory { get; set; } = "data/uploads";

    /// <summary>
    /// The maximum upload size for content uploaded by users
    /// </summary>
    public FileSize MaxUploadSize { get; set; } = FileSize.Parse("1MB");

    public bool CreateMissingDirectories { get; set; } = true;
}
