ALTER TABLE Notifications
    ADD COLUMN ContentType SMALLINT NOT NULL DEFAULT 0,
    ADD COLUMN Payload TEXT[] NOT NULL DEFAULT '{}';

CREATE TABLE Deletions (
    Id SERIAL PRIMARY KEY,
    Hash VARCHAR(64) NOT NULL,
    AllowReupload BOOLEAN NOT NULL DEFAULT false,
    Reason SMALLINT NOT NULL DEFAULT 0,
    UploadedBy UUID NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000', -- The user who originally uploaded this file
    DeletedBy UUID NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000', -- The admin who deleted it
    DeletionTime TIMESTAMPTZ NOT NULL
);


ALTER TABLE UploadedFiles
    ADD COLUMN DeletedId BIGINT,
    ADD CONSTRAINT FK_UploadedFiles_Deletions FOREIGN KEY (DeletedId)
        REFERENCES Deletions (Id) ON DELETE CASCADE;

DROP INDEX UX_UploadedFiles_Hash;

CREATE UNIQUE INDEX UX_UploadedFiles_Hash
    ON UploadedFiles(Hash)
    WHERE DeletedId IS NULL;