using System.Threading.Channels;
using JetBrains.Annotations;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Yeek.Database;
using Yeek.FileHosting.JavaScript.Objects;
using Yeek.FileHosting.Model;
using Yeek.FileHosting.Repositories;
using Yeek.Security.Model;
using Yeek.Security.Repositories;

namespace Yeek.FileHosting.JavaScript;

public class ScriptContext
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ScriptEngine _engine;

    private readonly Dictionary<Guid, JsUser> _userCache = [];
    private readonly Dictionary<Guid, JsFile> _fileCache = [];
    internal readonly Dictionary<Guid, QueuedUpdate> Updates = [];
    private bool _isRunningGetAllFiles = false;
    private readonly CancellationToken _token;

    internal ScriptContext(ApplicationDbContext dbContext, IServiceScopeFactory serviceScopeFactory,
        ScriptEngine engine, CancellationToken timeoutCtsToken)
    {
        _dbContext = dbContext;
        _serviceScopeFactory = serviceScopeFactory;
        _engine = engine;
        _token = timeoutCtsToken;
    }

    [UsedImplicitly]
    public async Task<object> QueryForFiles(string search)
    {
        if (_isRunningGetAllFiles)
            throw new InvalidOperationException("QueryForFiles must be awaited.");

        if (string.IsNullOrWhiteSpace(search))
            throw new ArgumentNullException(nameof(search));

        _isRunningGetAllFiles = true;

        using var scope = _serviceScopeFactory.CreateScope();
        var fileRepo = scope.ServiceProvider.GetRequiredService<IFileRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var files = await fileRepo.SearchAsync(search, SearchMode.Relevance, 0, int.MaxValue, true);
        var result = new List<object>();

        foreach (var uploadedFile in files.result)
        {
            _token.ThrowIfCancellationRequested();

            if (_fileCache.TryGetValue(uploadedFile.Id, out JsFile? value))
            {
                result.Add(value);
                continue;
            }

            var file = new JsFile(uploadedFile, await GetUserPrivateAsync(uploadedFile.UploadedById, userRepo), this);

            var revisionsWithUsers = await Task.WhenAll(
                uploadedFile.FileRevisions
                    .OrderByDescending(x => x.RevisionId)
                    .Select(async x =>
                        new JsRevision(x, await GetUserPrivateAsync(x.UpdatedById, userRepo), _engine)));

            file.Revisions = revisionsWithUsers.ToScriptArray(_engine);

            _fileCache[uploadedFile.Id] = file;
            result.Add(file);
        }

        _isRunningGetAllFiles = false;
        return result.ToArray().ToScriptArray(_engine);
    }

    /// <summary>
    /// Gets all files and all of their revisions.
    /// </summary>
    [UsedImplicitly]
    public async Task<object> GetAllFiles()
    {
        if (_isRunningGetAllFiles)
            throw new InvalidOperationException("GetAllFiles must be awaited.");

        _isRunningGetAllFiles = true;

        if (_fileCache.Count > 0)
        {
            return _fileCache.Values.ToArray().ToScriptArray(_engine);
        }

        using var scope = _serviceScopeFactory.CreateScope();
        var fileRepo = scope.ServiceProvider.GetRequiredService<IFileRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var files = await fileRepo.GetAllUploadedFilesAsync();

        foreach (var uploadedFile in files)
        {
            _token.ThrowIfCancellationRequested();

            var file = new JsFile(uploadedFile, await GetUserPrivateAsync(uploadedFile.UploadedById, userRepo), this);

            var revisionsWithUsers = await Task.WhenAll(
                uploadedFile.FileRevisions
                    .OrderByDescending(x => x.RevisionId)
                    .Select(async x =>
                        new JsRevision(x, await GetUserPrivateAsync(x.UpdatedById, userRepo), _engine)));

            file.Revisions = revisionsWithUsers.ToScriptArray(_engine);

            _fileCache[uploadedFile.Id] = file;
        }

        _isRunningGetAllFiles = false;
        var values = _fileCache.Values.ToArray().ToScriptArray(_engine);
        return values;
    }

    [UsedImplicitly]
    public async Task<JsUser> GetUser(Guid userId)
    {
        return await GetUserPrivateAsync(userId);
    }

    private async Task<JsUser> GetUserPrivateAsync(Guid userId, IUserRepository? repository = null)
    {
        if (_userCache.TryGetValue(userId, out var user))
            return user;

        User? userObj = null;
        if (repository == null)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            userObj = await userRepo.GetUserAsync(userId);
        }
        else
        {
            userObj = await repository.GetUserAsync(userId);
        }

        var jsUser = new JsUser(userObj);
        _userCache[userId] = jsUser;
        return jsUser;
    }

    internal async Task ProcessChanges(Channel<string> channel, bool apply, Guid author, string jobCode)
    {
        await channel.Writer.WriteAsync("[OK] Processing changes...");
        await channel.Writer.WriteAsync($"[OK] Have {Updates.Count} updates.");
        if (!apply)
            await channel.Writer.WriteAsync($"[WARN] This is a dry run, no changes will be applied.");

        using var scope = _serviceScopeFactory.CreateScope();
        var fileRepo = scope.ServiceProvider.GetRequiredService<IFileRepository>();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var user = await userRepo.GetUserAsync(author);
        var massEditId = Guid.CreateVersion7();
        var when = DateTime.UtcNow;
        var massEditHeader = $"MASS-EDIT {massEditId}\n";


        var revisionsForFiles = new Dictionary<Guid, FileRevision>();

        foreach (var (fileId, update) in Updates)
        {
            var file = await fileRepo.GetUploadedFileAsync(fileId);
            if (file.Locked && user.TrustLevel < TrustLevel.Trusted)
                throw new InvalidOperationException($"File {file.Id} is locked!");

            var revision = new FileRevision()
            {
                AlbumName = update.AlbumName,
                ArtistNames = update.ArtistNames.ToHostArray<string>(),
                ChangeSummary = massEditHeader + update.ChangeSummary,
                Description = update.Description,
                TrackName = update.TrackName,
                UpdatedOn = when,
                UploadedFileId = file.UploadedById,
                UpdatedById = user.Id,
            };

            revisionsForFiles.Add(fileId, revision);

            await channel.Writer.WriteAsync($"[OK] {revision.GetDiff(file.MostRecentRevision)}");
        }

        await fileRepo.ApplyMassEdit(author, revisionsForFiles, jobCode, massEditId, apply);
    }
}

public static class ArrayExtensions {
    public static object ToScriptArray(this Array array, ScriptEngine engine) {
        return engine.Script.Array.from(array);
    }

    public static T[] ToHostArray<T>(this object scriptValue)
    {
        switch (scriptValue)
        {
            case ScriptObject jsArray:
            {
                var length = (int)jsArray.GetProperty("length");
                var result = new List<T>();
                for (var i = 0; i < length; i++)
                {
                    var value = jsArray.GetProperty(i);
                    if (value is T t)
                        result.Add(t);
                    else if (value != null)
                        result.Add((T)Convert.ChangeType(value, typeof(T)));
                }
                return result.ToArray();
            }

            case IEnumerable<T> enumerable:
                return enumerable.ToArray();

            default:
                return [];
        }
    }
}

public record QueuedUpdate(string TrackName, string? AlbumName, string[] ArtistNames, string? Description, string ChangeSummary);