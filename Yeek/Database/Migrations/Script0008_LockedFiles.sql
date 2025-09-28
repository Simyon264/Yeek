ALTER TABLE uploadedfiles
    ADD COLUMN locked BOOLEAN NOT NULL DEFAULT false;
