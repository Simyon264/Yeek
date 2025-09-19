using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Net.Http.Headers;
using Yeek.Configuration;
using Yeek.FileHosting.Model;
using Yeek.FileHosting.Repositories;
using Yeek.Security;
using Yeek.WebDAV;

namespace Yeek.FileHosting;

public class FileService
{
    private readonly IFileRepository _fileRepository;
    private readonly FileConfiguration _fileConfiguration = new();
    private readonly ILogger<FileService> _logger;
    private readonly WebDavManager _webDavManager;

    public FileService(IFileRepository context, IConfiguration configuration, ILogger<FileService> logger, WebDavManager webDavManager)
    {
        _logger = logger;
        _fileRepository = context;
        _webDavManager = webDavManager;
        configuration.Bind(FileConfiguration.Name, _fileConfiguration);
    }

    public async Task<IResult> GetFileAsResult(Guid fileId)
    {
        if (!await _fileRepository.FileExistsAsync(fileId))
            return Results.NotFound();

        var fileResult = await _fileRepository.GetUploadedFileAsync(fileId);

        var file = Path.Combine(_fileConfiguration.UserContentDirectory, fileResult.RelativePath);
        if (!File.Exists(file))
        {
            _logger.LogError("File exists in DB but not in file system. Path {File}", fileResult.RelativePath);
            return Results.InternalServerError("File does not exist in file system, but exists in DB.");
        }

        var etag = new EntityTagHeaderValue($"\"{fileResult.Hash}\"");

        return TypedResults.PhysicalFile(Path.GetFullPath(file), entityTag: etag, fileDownloadName: fileResult.GetDownloadName());
    }

    public async Task<IResult> PatchFile(MidiUploadForm form, ClaimsPrincipal user)
    {
        var userId = user.Claims.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        // Normalizing the fields
        if (string.IsNullOrWhiteSpace(form.Albumname))
            form.Albumname = null;
        if (string.IsNullOrWhiteSpace(form.Authorname))
            form.Authorname = null;
        if (string.IsNullOrWhiteSpace(form.Description))
            form.Description = string.Empty;

        var fileExists = await _fileRepository.FileExistsAsync(form.Id!.Value);
        if (!fileExists)
            return Results.NotFound();

        await _fileRepository.EditFileAsync(form.Id!.Value, new FileRevision()
        {
            TrackName = form.Trackname,
            ArtistName = form.Authorname,
            Description = form.Description,
            AlbumName = form.Albumname,
            UpdatedOn = DateTime.UtcNow,
            UploadedFileId = form.Id!.Value,
            UpdatedById = userId.Value,
            ChangeSummary = form.ChangeSummary
        });

        _webDavManager.Updates.Add(form.Id!.Value);

        return Results.Ok();
    }

    public async Task<IResult> UploadFile(MidiUploadForm form, ClaimsPrincipal user)
    {
        var userId = user.Claims.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        // Normalizing the fields
        if (string.IsNullOrWhiteSpace(form.Albumname))
            form.Albumname = null;
        if (string.IsNullOrWhiteSpace(form.Authorname))
            form.Authorname = null;
        if (string.IsNullOrWhiteSpace(form.Description))
            form.Description = string.Empty;

        if (form.File.Length > _fileConfiguration.MaxUploadSize)
            return Results.BadRequest($"Maximum upload size exceeded. > {_fileConfiguration.MaxUploadSize}");

        await using var ms = new MemoryStream();
        await using (var stream = form.File.OpenReadStream())
        {
            await stream.CopyToAsync(ms);
        }

        var sha = CalculateHash(ms);
        var existingFile = await _fileRepository.FindFileByShaAsync(sha);
        if (existingFile.foundMatch)
        {
            return Results.Conflict(existingFile.fileId!.Value);
        }

        var fileId = Guid.NewGuid();
        var fileName = $"{fileId}{Path.GetExtension(form.File.FileName)}";

        try
        {
            var resolvedPath = Path.Combine(_fileConfiguration.UserContentDirectory, fileName);
            await using FileStream fs = new(resolvedPath, FileMode.Create);
            await ms.CopyToAsync(fs);
        }
        catch (IOException e)
        {
            _logger.LogError("Failed uploading file: {error}", e);
            return Results.InternalServerError();
        }

        await _fileRepository.UploadFileAsync(new UploadedFile()
        {
            Hash = sha,
            RelativePath = fileName,
            FileRevisions = new List<FileRevision>(),
            Id = fileId,
            UploadedOn = DateTime.UtcNow,
            UploadedById = userId.Value,
            FileSize = ms.Length,
            OriginalName = form.File.FileName,
        }, new FileRevision()
        {
            UploadedFileId = fileId,
            RevisionId = 0,
            AlbumName = form.Albumname,
            ArtistName = form.Authorname,
            Description = form.Description,
            TrackName = form.Trackname,
            UpdatedById = userId.Value,
            UpdatedOn = DateTime.UtcNow,
        });

        _webDavManager.Updates.Add(fileId);
        _logger.LogInformation("Uploaded new MIDI {fileName} ({fileId})", form.File.FileName, fileId);

        return Results.Text(fileId.ToString(), statusCode: 201);
    }

    public async Task<IResult> VoteAsResult(int score, Guid fileId, ClaimsPrincipal user)
    {
        var userId = user.Claims.GetUserId();
        if (userId == null)
            return Results.Unauthorized();

        if (score is > 1 or < -1)
            return Results.BadRequest("Exceeded vote bounds. Nerd.");

        if (!await _fileRepository.FileExistsAsync(fileId))
            return Results.NotFound();

        if (score == 0)
        {
            await _fileRepository.RemoveRatingAsync(fileId, userId.Value);
        }
        else
        {
            await _fileRepository.RateFileAsync(fileId, userId.Value, score);
        }

        return Results.Ok();
    }

    private static string CalculateHash(MemoryStream stream)
    {
        stream.Position = 0;

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(stream);

        // Convert bytes to hex string
        var sb = new StringBuilder(hashBytes.Length * 2);
        foreach (var b in hashBytes)
            sb.Append(b.ToString("x2"));

        // Reset again if stream will be reused (e.g., for saving to disk)
        stream.Position = 0;

        return sb.ToString();
    }
}