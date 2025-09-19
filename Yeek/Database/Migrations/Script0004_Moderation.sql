ALTER TABLE users
    ADD COLUMN trustlevel bigint NOT NULL DEFAULT 0; -- -1 banned, 0 normal user, 1 trusted 2 mod 3 admin

CREATE TABLE tickets(
    Id SERIAL PRIMARY KEY,
    Resolved BOOL NOT NULL DEFAULT false, -- If this ticket / report is done. If true, the reportee won't be able to send messages in the ticket.
    Reportee UUID NOT NULL, -- Who reported this content?
    Header VARCHAR(400) NOT NULL -- What content did the person report?
);

CREATE TABLE ticketmessages(
    Id UUID PRIMARY KEY,
    SentById UUID NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000', -- Person who sent this message
    TimeSent TIMESTAMPTZ NOT NULL, -- When this message was sent
    Content TEXT NOT NULL,
    TicketId BIGINT NOT NULL,

    CONSTRAINT FK_TicketMessages_Ticket FOREIGN KEY (TicketId)
        REFERENCES tickets (Id) ON DELETE CASCADE,

    CONSTRAINT FK_TicketMessages_SentBy FOREIGN KEY (SentById)
        REFERENCES Users (Id) ON DELETE SET DEFAULT
);

CREATE TABLE usernotes(
    Id UUID PRIMARY KEY, -- the note id
    AffectedUserId UUID, -- the user this note is for
    CreatedByUserId UUID DEFAULT '00000000-0000-0000-0000-000000000000', -- the user who added this note
    Content TEXT NOT NULL,


    CONSTRAINT FK_UserNotes_CreatedBy FOREIGN KEY (CreatedByUserId)
        REFERENCES Users (Id) ON DELETE SET DEFAULT

    -- Explicitly no FK for the AffectedUserId. Notes should stay no matter what.
);

-- Global notifications, yippee.
CREATE TABLE globalmessages(
    Id SERIAL PRIMARY KEY,
    Show BOOL NOT NULL DEFAULT true,
    Content TEXT NOT NULL, -- The contents of the message
    Header TEXT -- Optional header.
);

CREATE TABLE bans(
    Id SERIAL PRIMARY KEY,
    AffectedUser UUID NOT NULL,
    ExpiresAt TIMESTAMPTZ NOT NULL,
    IssuerId UUID NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',

    CONSTRAINT FK_Bans_CreatedBy FOREIGN KEY (IssuerId)
        REFERENCES Users (Id) ON DELETE SET DEFAULT
);