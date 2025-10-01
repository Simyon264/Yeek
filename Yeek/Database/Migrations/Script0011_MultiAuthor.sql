ALTER TABLE FileRevisions
    ADD COLUMN ArtistNames TEXT[] NOT NULL DEFAULT '{}';

UPDATE FileRevisions
SET ArtistNames = ARRAY[ArtistName]
WHERE ArtistName IS NOT NULL AND ArtistName <> '';

ALTER TABLE FileRevisions
    DROP COLUMN Search_tsvector;

ALTER TABLE FileRevisions
    ADD COLUMN Search_tsvector tsvector;

CREATE OR REPLACE FUNCTION file_revisions_search_update() RETURNS trigger AS $$
BEGIN
    NEW.Search_tsvector :=
            setweight(to_tsvector('english', coalesce(NEW.TrackName, '')), 'A') ||
            setweight(to_tsvector('english', coalesce(array_to_string(NEW.ArtistNames, ' '), '')), 'B') ||
            setweight(to_tsvector('english', coalesce(NEW.AlbumName, '')), 'B') ||
            setweight(to_tsvector('english', coalesce(NEW.Description, '')), 'C');
    RETURN NEW;
END
$$ LANGUAGE plpgsql;

CREATE TRIGGER file_revisions_search_update_trigger
    BEFORE INSERT OR UPDATE ON FileRevisions
    FOR EACH ROW
EXECUTE FUNCTION file_revisions_search_update();

UPDATE FileRevisions
SET Search_tsvector =
        setweight(to_tsvector('english', coalesce(TrackName, '')), 'A') ||
        setweight(to_tsvector('english', coalesce(array_to_string(ArtistNames, ' '), '')), 'B') ||
        setweight(to_tsvector('english', coalesce(AlbumName, '')), 'B') ||
        setweight(to_tsvector('english', coalesce(Description, '')), 'C');

CREATE INDEX file_revisions_search_idx
    ON FileRevisions
        USING GIN (Search_tsvector);


ALTER TABLE FileRevisions
    DROP COLUMN ArtistName;
