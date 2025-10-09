CREATE TABLE ScriptHistory(
    Id UUID PRIMARY KEY, -- The ID of this "mass-edit run"
    WasApplied BOOLEAN NOT NULL DEFAULT false, -- If we actually applied this run.
    Script TEXT NOT NULL, -- The code that was run.
    RunBy UUID NOT NULL, -- The user that ran this script.
    ExecutedOn TIMESTAMPTZ NOT NULL
);

CREATE INDEX IX_ScriptHistory_UserId ON ScriptHistory(RunBy);
CREATE INDEX IX_ScriptHistory_ExecutedOn ON ScriptHistory(ExecutedOn DESC);

ALTER TABLE FileRevisions
    ADD COLUMN ScriptHistoryId UUID NULL,
    ADD CONSTRAINT FK_FileRevisions_ScriptHistory FOREIGN KEY (ScriptHistoryId)
        REFERENCES ScriptHistory (Id) ON DELETE SET NULL;

CREATE INDEX IX_FileRevisions_ScriptHistoryId ON FileRevisions(ScriptHistoryId);