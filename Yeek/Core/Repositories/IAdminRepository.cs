using Yeek.Core.Models;

namespace Yeek.Core.Repositories;

/// <summary>
/// Holds methods that have side wide changes.
/// </summary>
public interface IAdminRepository
{
    Task<List<GlobalMessage>> GetAllActiveMessagesAsync();
}