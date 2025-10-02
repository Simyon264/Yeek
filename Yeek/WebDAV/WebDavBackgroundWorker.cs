using Yeek.Database;
using Yeek.FileHosting.Model;
using Yeek.FileHosting.Repositories;

namespace Yeek.WebDAV;

public class WebDavBackgroundWorker : BackgroundService
{
    private readonly ILogger<WebDavBackgroundWorker> _logger;
    private readonly WebDavManager _webDavManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    private readonly HashSet<Guid> _duplicationResolves = [];

    public WebDavBackgroundWorker(ILogger<WebDavBackgroundWorker> logger, WebDavManager webDavManager, ApplicationDbContext dbContext, IServiceScopeFactory serviceScopeFactory)
    {
        _logger = logger;
        _webDavManager = webDavManager;
        _dbContext = dbContext;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Migrator.MigrationComplete;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_webDavManager.Updates.Count > 0 || !_webDavManager.Ready || _webDavManager.Deletes.Count > 0)
            {
                await Update(stoppingToken);
            }

            await Task.Delay(5000, stoppingToken);
        }
    }

    private async Task Update(CancellationToken stoppingToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var fileRepository = scope.ServiceProvider.GetRequiredService<IFileRepository>();

        _logger.LogInformation("Updating VFS...");

        if (!_webDavManager.Ready)
        {
            _logger.LogInformation("Performing first time setup... This may take a while.");
            _webDavManager.Updates.AddRange(await fileRepository.GetAllIdsAsync());
        }

        var pendingUpdates = new Queue<Guid>(_webDavManager.Updates);
        _webDavManager.Updates.Clear();

        var pendingDeletes = new Queue<Guid>(_webDavManager.Deletes);
        _webDavManager.Deletes.Clear();

        while (pendingDeletes.Count > 0 && !stoppingToken.IsCancellationRequested)
        {
            var fileId = pendingDeletes.Dequeue();

            _logger.LogInformation("Deleting file {FileId} from VFS...", fileId);

            var root = _webDavManager.RootDirectory;
            RemoveFileFromTree(root, fileId);
        }

        // Kind of hacky, but whatever.
        // We manually put each file that had a duplicated name into the update queue in order to force it to rebuild the tree
        // So that in the case of "the file names no longer conflict" we can easily remove the counter from the name.
        _duplicationResolves.ToList().ForEach(pendingUpdates.Enqueue);

        while (pendingUpdates.Count > 0 && !stoppingToken.IsCancellationRequested)
        {
            var fileId = pendingUpdates.Dequeue();
            UploadedFile file;

            try
            {
                file = await fileRepository.GetUploadedFileAsync(fileId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch file {FileId}, skipping.", fileId);
                continue;
            }

            if (file.DeletedId != null)
            {
                _logger.LogDebug("File {id} is deleted, skipping...", fileId);
                continue;
            }

            var revision = file.MostRecentRevision;
            var root = _webDavManager.RootDirectory;

            RemoveFileFromTree(root, file.Id);

            // Unsorted (all files)
            var unsortedDir = GetOrCreateDirectory(root, "Unsorted");
            AddFileToDirectory(unsortedDir, file);

            // Single Songs
            //var singleSongsDir = GetOrCreateDirectory(root, "Single Songs");
            //if (!string.IsNullOrWhiteSpace(revision.AlbumName))
            //{
            //    var albumDir = GetOrCreateDirectory(singleSongsDir, revision.AlbumName);
            //    AddFileToDirectory(albumDir, file);
            //}
            //else
            //{
            //    var ssUnsorted = GetOrCreateDirectory(singleSongsDir, "Unsorted");
            //    AddFileToDirectory(ssUnsorted, file);
            //}

            // Albums
            if (!string.IsNullOrWhiteSpace(revision.AlbumName))
            {
                var albumsDir = GetOrCreateDirectory(root, "Albums");
                albumsDir.XmlCacheByDepth.Clear();
                var albumDir = GetOrCreateDirectory(albumsDir, revision.AlbumName);
                albumDir.XmlCacheByDepth.Clear();
                AddFileToDirectory(albumDir, file);
            }

            // Artist
            if (revision.ArtistNames.Length > 0)
            {
                var artistDir = GetOrCreateDirectory(root, "Artist");
                artistDir.XmlCacheByDepth.Clear();

                foreach (var artist in revision.ArtistNames.Where(a => !string.IsNullOrWhiteSpace(a)))
                {
                    var artistSubDir = GetOrCreateDirectory(artistDir, artist);
                    artistSubDir.XmlCacheByDepth.Clear();

                    if (!string.IsNullOrWhiteSpace(revision.AlbumName))
                    {
                        var albumDir = GetOrCreateDirectory(artistSubDir, revision.AlbumName);
                        albumDir.XmlCacheByDepth.Clear();

                        AddFileToDirectory(albumDir, file);
                    }
                    else
                    {
                        // If we don't have an album, we put it under "Singles". Technically the wrong term I belive, but I do not care.
                        var singleDir = GetOrCreateDirectory(artistSubDir, "Singles");
                        singleDir.XmlCacheByDepth.Clear();

                        AddFileToDirectory(singleDir, file);
                    }
                }
            }

            // Alphabetical
            if (!string.IsNullOrWhiteSpace(revision.TrackName))
            {
                var alphaDir = GetOrCreateDirectory(root, "Alphabetical");
                alphaDir.XmlCacheByDepth.Clear();
                var firstLetter = char.ToUpperInvariant(revision.TrackName[0]);
                var letterDir = GetOrCreateDirectory(alphaDir, firstLetter.ToString());
                letterDir.XmlCacheByDepth.Clear();
                AddFileToDirectory(letterDir, file);
            }
        }

        _webDavManager.Ready = true;
        _logger.LogInformation("VFS update completed.");
    }

    private bool RemoveFileFromTree(Directory dir, Guid fileId)
    {
        // Remove file from this directory
        var removed = dir.Files.RemoveAll(f => f.Id == fileId);
        if (removed > 0)
        {
            dir.XmlCacheByDepth.Clear();
        }

        // Recurse into children and remove empty ones
        for (var i = dir.Children.Count - 1; i >= 0; i--)
        {
            var child = dir.Children[i];
            if (RemoveFileFromTree(child, fileId))
            {
                // If child became empty after removal, drop it
                dir.Children.RemoveAt(i);
            }
        }

        // Return true if this directory is now empty and not root
        return dir is { IsRoot: false, Files.Count: 0, Children.Count: 0 };
    }

    /// <summary>
    /// Finds or creates a child directory under a parent with the given name.
    /// </summary>
    private Directory GetOrCreateDirectory(Directory parent, string name)
    {
        var dir = parent.Children.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase));
        if (dir == null)
        {
            dir = new Directory
            {
                Parent = parent,
                Name = name
            };
            parent.Children.Add(dir);
        }
        return dir;
    }

    /// <summary>
    /// Adds a file to a directory if it is not already present.
    /// </summary>
    private void AddFileToDirectory(Directory dir, UploadedFile file)
    {
        if (dir.Files.Any(f => f.Id == file.Id))
            return;

        var counter = 1;

        var clonedFile = new UploadedFile
        {
            Id = file.Id,
            RelativePath = file.RelativePath,
            OriginalName = file.OriginalName,
            FileSize = file.FileSize,
            Hash = file.Hash,
            UploadedById = file.UploadedById,
            UploadedOn = file.UploadedOn,
            FileRevisions = file.FileRevisions,
            Rating = file.Rating,
            Locked = file.Locked,
            Downloads = file.Downloads,
            Plays = file.Plays,
            DeletedId = file.DeletedId
        };

        while (dir.Files.Any(f =>
                   string.Equals(f.GetDownloadName(), file.GetDownloadName(), StringComparison.OrdinalIgnoreCase)))
        {
            var renamed = $"{file.MostRecentRevision.TrackName} ({counter++})";
            clonedFile.MostRecentRevision.TrackName = renamed;
        }

        if (counter != 1)
        {
            _duplicationResolves.Add(file.Id);
        }
        else
        {
            _duplicationResolves.Remove(file.Id);
        }

        dir.Files.Add(clonedFile);
        dir.XmlCacheByDepth.Clear();
    }
}