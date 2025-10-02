using Yeek.FileHosting.Model;
using Yeek.Security.Model;

namespace Yeek.FileHosting.Repositories;

public interface IFileRepository
{
    public Task<(List<UploadedFile> result, int allCount, int pageCount)> SearchAsync(string query, SearchMode mode, int page = 0, int itemsPerPage = 50);
    public Task<List<UploadedFile>> GetRandomMidis(int amount = 6);
    public Task<List<UploadedFile>> GetRecentMidisAsync(int amount = 50);

    public Task<int> GetAllCountAsync();

    public Task<(bool foundMatch, Guid? fileId)> FindFileByShaAsync(string sha);
    public Task UploadFileAsync(UploadedFile uploadedFile, FileRevision fileRevision);

    public Task<UploadedFile> GetUploadedFileAsync(Guid fileId);
    public Task<UploadedFile> GetUploadedFileWithSpecificRevisionAsync(Guid fileId, int revision);

    public Task<List<SummarizedRevision>> GetRevisionsAsync(Guid fileId);
    /// <summary>
    /// Gets an uploaded file without fetching the latest revision
    /// </summary>
    public Task<UploadedFile?> GetUploadedFilePureAsync(Guid fileId);

    public Task<bool> FileExistsAsync(Guid fileId);

    public Task<int?> GetRatingForUserForFileAsync(Guid fileId, Guid userId);

    public Task RateFileAsync(Guid fileId, Guid userId, int score);

    public Task RemoveRatingAsync(Guid fileId, Guid userId);

    public Task<Guid[]> GetAllIdsAsync();
    Task EditFileAsync(Guid fileId, FileRevision fileRevision);

    public Task<FilePreview?> GetFilePreviewOrNullAsync(Guid fileId);
    public Task AddFilePreviewAsync(Guid fileId, FilePreview preview);
    public Task<Guid[]> GetMissingPreviews(string[] requiredExtensions);
    public Task<Guid[]> GetFilesNeedingRegenerationAsync();
    public Task<int> GetContributionsForUserAsync(Guid userId);
    public Task AddDownload(Guid fileId, DownloadType type);
    public Task<DeletionReason?> GetDeletionStatusAsync(Guid fileId);
    public Task DeleteFile(Guid fileId, bool allowReupload, DeletionReason reason, Guid user);
    public Task<bool> GetReuploadStatusForHash(string sha);
}

public enum DownloadType
{
    Website,
    // ReSharper disable once InconsistentNaming
    WebDAV,
}

public enum SearchMode
{
    Relevance = 1,
    Top = 2,
    Recent = 3,
}

public class SummarizedRevision
{
    public int RevisionId { get; set; }
    public DateTime UpdatedOn { get; set; }
    public string ChangeSummary { get; set; }
    public User UpdatedBy { get; set; }
}