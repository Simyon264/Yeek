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
            if (_webDavManager.Updates.Count > 0 || !_webDavManager.Ready)
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

            var revision = file.MostRecentRevision;
            var root = _webDavManager.RootDirectory;

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
                var albumDir = GetOrCreateDirectory(albumsDir, revision.AlbumName);
                AddFileToDirectory(albumDir, file);
            }

            // Artist
            if (!string.IsNullOrWhiteSpace(revision.ArtistName) && !string.IsNullOrWhiteSpace(revision.AlbumName))
            {
                var artistDir = GetOrCreateDirectory(root, "Artist");
                var artistSubDir = GetOrCreateDirectory(artistDir, revision.ArtistName);
                var albumDir = GetOrCreateDirectory(artistSubDir, revision.AlbumName);
                AddFileToDirectory(albumDir, file);
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
        if (dir.Files.All(f => f.Id != file.Id))
        {
            dir.Files.Add(file);
            dir.XmlCacheByDepth.Clear();
        }
    }
}