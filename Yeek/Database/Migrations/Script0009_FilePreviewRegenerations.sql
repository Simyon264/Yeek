ALTER TABLE FilePreviews
    ADD COLUMN Regenerate TEXT [] NOT NULL DEFAULT '{}'; -- Files extensions that need to be regenerated. Includes the . for the extension
