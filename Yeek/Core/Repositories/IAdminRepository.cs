using Yeek.Core.Models;

namespace Yeek.Core.Repositories;

/// <summary>
/// Holds methods that have side wide changes.
/// </summary>
public interface IAdminRepository
{
    Task<List<GlobalMessage>> GetAllActiveMessagesAsync();
    Task<List<GlobalMessage>> GetAllMessagesAsync();
    Task<bool> ToggleGlobalMessageShowAsync(int id);
    Task<bool> EditGlobalMessageHeaderAsync(int id, string header);
    Task<bool> EditGlobalMessageContentAsync(int id, string content);
    Task CreateGlobalMessageAsync(string header, string content);
}