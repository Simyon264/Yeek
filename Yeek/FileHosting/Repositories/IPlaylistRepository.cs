using System.Runtime.CompilerServices;
using Yeek.FileHosting.Model;

namespace Yeek.FileHosting.Repositories;

public interface IPlaylistRepository
{
    /// <summary>
    /// Gets the available playlists the given user can manage. Manage meaning they can add songs.
    /// </summary>
    public Task<List<Playlist>> GetPlaylistsForUser(Guid userId);

    /// <summary>
    /// Gets the playlists a certain file is in. Scoped for playlists the user made.
    /// </summary>
    public Task<List<Guid>> GetPlaylistIdsForFileForUser(Guid fileId, Guid userId);

    /// <summary>
    /// Creates a playlist
    /// </summary>
    public Task CreatePlaylist(Guid userId, string name);

    /// <summary>
    /// Removes or adds the given song to a playlist.
    /// Returns the new status of the song. So false if its removed, true if added.
    /// </summary>
    public Task<bool> RemoveOrAddSongToPlaylist(Guid playlistId, Guid fileId);

    /// <summary>
    /// Returns if a person can manage the given playlist. Manage meaning they can add songs to it.
    /// </summary>
    public Task<bool> CanManagePlaylist(Guid playlistId, Guid userId);

    public Task<Playlist?> GetPlaylistOrNull(Guid playlistId);

    public IAsyncEnumerable<UploadedFile> EnumeratePlaylistEntriesAsync(
        Playlist playlist,
        [EnumeratorCancellation] CancellationToken cancellationToken = default);

    public Task DeletePlaylist(Guid playlistId);
    Task<IEnumerable<Guid>> GetAllPlaylists();
}