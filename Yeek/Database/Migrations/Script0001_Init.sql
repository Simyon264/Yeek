CREATE TABLE Users (
    Id UUID PRIMARY KEY,
    DisplayName VARCHAR(300) NOT NULL
);

-- Basically our fallback so that we don't have to nuke used midis
INSERT INTO Users(Id, DisplayName)
VALUES ('00000000-0000-0000-0000-000000000000', 'Deleted User');

CREATE TABLE Notifications (
    Id SERIAL PRIMARY KEY,
    Read BOOLEAN DEFAULT false,
    Created TIMESTAMPTZ NOT NULL,
    Severity SMALLINT NOT NULL,
    UserId UUID NOT NULL, -- This is the user the notif is for

    CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId)
        REFERENCES Users (Id) ON DELETE CASCADE
);

-- Useful indexes for Notifications
CREATE INDEX IX_Notifications_UserId ON Notifications(UserId);
CREATE INDEX IX_Notifications_Created ON Notifications(Created);
CREATE INDEX IX_Notifications_Read ON Notifications(Read);
CREATE INDEX IX_Notifications_UserId_Created ON Notifications(UserId, Created DESC);
CREATE INDEX IX_Notifications_Read_Created ON Notifications(Read, Created DESC);

-- The Ratings table needs to come later as that references the files

CREATE TABLE UploadedFiles (
    Id UUID PRIMARY KEY,
    RelativePath VARCHAR(260) NOT NULL,
    Hash VARCHAR(64) NOT NULL, -- A SHA-265 returns 265 bytes, those in ascii are 64 characters
    UploadedOn TIMESTAMPTZ NOT NULL,

    UploadedBy UUID NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000', -- The user who originally uploaded this file
    CONSTRAINT FK_UploadedFiles_Users FOREIGN KEY (UploadedBy)
        REFERENCES Users (Id) ON DELETE SET DEFAULT
);

-- Useful indexes for UploadedFiles
CREATE INDEX IX_UploadedFiles_UploadedBy ON UploadedFiles(UploadedBy);
CREATE UNIQUE INDEX UX_UploadedFiles_Hash ON UploadedFiles(Hash);

CREATE TABLE FileRevisions (
    UploadedFileId UUID NOT NULL,
    RevisionId INT NOT NULL,
    UpdatedById UUID NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
    UpdatedOn TIMESTAMPTZ NOT NULL,
    TrackName VARCHAR(200) NOT NULL,
    AlbumName TEXT,
    ArtistName TEXT,
    Description TEXT NOT NULL DEFAULT '',

    -- Combined full-text search column for TrackName, ArtistName, Description
    -- This should hopefully work well enough
    Search_tsvector tsvector GENERATED ALWAYS AS (
        setweight(to_tsvector('english', coalesce(TrackName, '')), 'A') ||
        setweight(to_tsvector('english', coalesce(ArtistName, '')), 'B') ||
        setweight(to_tsvector('english', coalesce(AlbumName, '')), 'B') ||
        setweight(to_tsvector('english', coalesce(Description, '')), 'C')
        ) STORED,

    CONSTRAINT PK_FileRevisions PRIMARY KEY (UploadedFileId, RevisionId),
    CONSTRAINT FK_FileRevisions_UploadedFile FOREIGN KEY (UploadedFileId)
        REFERENCES UploadedFiles (Id) ON DELETE CASCADE,
    CONSTRAINT FK_FileRevisions_UpdatedBy FOREIGN KEY (UpdatedById)
        REFERENCES Users (Id) ON DELETE SET DEFAULT
);

-- Compound index: get latest revisions for a file quickly
CREATE INDEX IX_FileRevisions_UploadedFileId_UpdatedOn
    ON FileRevisions(UploadedFileId, UpdatedOn DESC);

-- Compound index: get revisions updated by a specific user quickly
CREATE INDEX IX_FileRevisions_UpdatedById_UpdatedOn
    ON FileRevisions(UpdatedById, UpdatedOn DESC);

-- Indexes for search/sorting
CREATE INDEX IX_FileRevisions_TrackName ON FileRevisions(TrackName);
CREATE INDEX IX_FileRevisions_ArtistName ON FileRevisions(ArtistName);

-- Full-text search index
CREATE INDEX IX_FileRevisions_Search_tsvector
    ON FileRevisions USING GIN(Search_tsvector);

CREATE TABLE Ratings (
    UserId UUID NOT NULL,
    UploadedFileId UUID NOT NULL,
    Score INT NOT NULL,

    CONSTRAINT PK_Ratings PRIMARY KEY (UserId, UploadedFileId),

    CONSTRAINT FK_Ratings_User FOREIGN KEY (UserId)
        REFERENCES Users (Id) ON DELETE CASCADE,

    CONSTRAINT FK_Ratings_UploadedFile FOREIGN KEY (UploadedFileId)
        REFERENCES UploadedFiles (Id) ON DELETE CASCADE

    /*
    I explicitly remove ratings for deleted users, since otherwise you could just:
    Login -> rate -> delete account -> log back in -> rate
    And i dont want that :godo:
    */
);

CREATE INDEX IX_Ratings_UploadedFileId ON Ratings(UploadedFileId);