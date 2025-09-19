using Yeek.FileHosting.Model;

namespace Yeek.WebDAV;

public class WebDavManager
{
    public List<Guid> Updates { get; set; } = new List<Guid>();
    public List<Guid> Deletes { get; set; } = new List<Guid>();

    public bool Ready { get; set; } = false;

    public Directory RootDirectory { get; set; } = new Directory()
    {
        IsRoot = true,
    };
}

public class Directory
{
    public bool IsRoot { get; set; }
    public string? Name { get; set; }
    public Directory? Parent { get; set; }
    public List<Directory> Children { get; set; } = new List<Directory>();
    public List<UploadedFile> Files { get; set; } = new List<UploadedFile>();

    /// <summary>
    /// The cached WebDAV response XML.
    /// </summary>
    public Dictionary<int, string> XmlCacheByDepth { get; set; } = new();
}