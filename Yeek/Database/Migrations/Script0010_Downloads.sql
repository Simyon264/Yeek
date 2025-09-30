ALTER TABLE uploadedfiles
    ADD COLUMN downloads BIGINT NOT NULL DEFAULT 0, -- Downloads done via the "download" button
    ADD COLUMN plays BIGINT NOT NULL DEFAULT 0; -- Downloads done via WebDAV
