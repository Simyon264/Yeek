CREATE TABLE Playlists(
    Id UUID PRIMARY KEY, -- The ID of this playlist. Won't ever be shown to the user
    Name VARCHAR(100) NOT NULL, -- The name of the playlist, can be whatever.
    Visibility INTEGER NOT NULL DEFAULT 0, -- The visibility of the playlist. 0 is public, 1 is unlisted, 2 is private.
    UserId UUID NOT NULL, -- This is the user who is the owner of the playlist.
    FileShareMode INTEGER NOT NULL DEFAULT 0, -- How files should be displayed in the file share. 0 is just unsorted, nothing else implemented yet.

    CONSTRAINT FK_Playlists_Users FOREIGN KEY (UserId)
        REFERENCES Users (Id) ON DELETE CASCADE
);

CREATE TABLE PlaylistEntry(
    Id SERIAL PRIMARY KEY, -- Simple id counter that goes up 👍
    PlaylistId UUID NOT NULL, -- What playlist is this entry for.
    UploadedFileId UUID NOT NULL, -- What file is this playlist entry referencing.
    AddedToPlaylist TIMESTAMPTZ NOT NULL, -- When this was added to the playlist.

    CONSTRAINT FK_PlaylistEntry_Playlist FOREIGN KEY (PlaylistId)
        REFERENCES Playlists (Id) ON DELETE CASCADE,

    CONSTRAINT FK_PlaylistEntry_UploadedFile FOREIGN KEY (UploadedFileId)
        REFERENCES UploadedFiles (Id) ON DELETE CASCADE
);