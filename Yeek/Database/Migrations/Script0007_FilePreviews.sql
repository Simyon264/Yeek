CREATE TABLE FilePreviews(
    UploadedFileId UUID PRIMARY KEY,
    SupportedExtensions TEXT [], -- With the '.', so ".mp3" ".webm" ".aac"
    GeneratedAt TIMESTAMPTZ NOT NULL,

    CONSTRAINT FK_FilePreviews_UploadedFiles FOREIGN KEY (UploadedFileId)
        REFERENCES UploadedFiles (Id) ON DELETE CASCADE
);