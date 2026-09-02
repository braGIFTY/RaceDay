/* ================================================================
   RaceDay Database Schema
   Part 1, Section C — SQL Database Script
   Target: Microsoft SQL Server (run in SSMS)

   This script matches the ERD exactly:
   Organisers -> Events -> Categories -\
                                         -> Enrolments -> Results
                            Participants /
   ================================================================ */

IF DB_ID('RaceDayDB') IS NULL
BEGIN
    CREATE DATABASE RaceDayDB;
END
GO

USE RaceDayDB;
GO

/* ----------------------------------------------------------------
   Drop tables if they already exist, child -> parent order,
   so this script can be re-run safely in SSMS.
   ---------------------------------------------------------------- */
DROP TABLE IF EXISTS dbo.Results;
DROP TABLE IF EXISTS dbo.Enrolments;
DROP TABLE IF EXISTS dbo.Categories;
DROP TABLE IF EXISTS dbo.Events;
DROP TABLE IF EXISTS dbo.Participants;
DROP TABLE IF EXISTS dbo.Organisers;
GO

/* ----------------------------------------------------------------
   CREATE TABLE statements
   ---------------------------------------------------------------- */

CREATE TABLE dbo.Organisers (
    Id              INT IDENTITY(1,1)  NOT NULL,
    FullName        NVARCHAR(100)      NOT NULL,
    Email           NVARCHAR(150)      NOT NULL,
    PasswordHash    NVARCHAR(255)      NOT NULL,
    Phone           NVARCHAR(20)       NULL,
    CreatedAt       DATETIME2          NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT PK_Organisers PRIMARY KEY (Id),
    CONSTRAINT UQ_Organisers_Email UNIQUE (Email)
);
GO

CREATE TABLE dbo.Participants (
    Id              INT IDENTITY(1,1)  NOT NULL,
    FullName        NVARCHAR(100)      NOT NULL,
    Email           NVARCHAR(150)      NOT NULL,
    PasswordHash    NVARCHAR(255)      NOT NULL,
    Phone           NVARCHAR(20)       NULL,
    CreatedAt       DATETIME2          NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT PK_Participants PRIMARY KEY (Id),
    CONSTRAINT UQ_Participants_Email UNIQUE (Email)
);
GO

CREATE TABLE dbo.Events (
    Id              INT IDENTITY(1,1)  NOT NULL,
    OrganiserId     INT                NOT NULL,
    Name            NVARCHAR(150)      NOT NULL,
    Description     NVARCHAR(MAX)      NULL,
    EventDate       DATE               NOT NULL,
    Location        NVARCHAR(150)      NOT NULL,
    DistanceKm      DECIMAL(6,2)       NOT NULL,
    EventType       NVARCHAR(10)       NOT NULL,
    CreatedAt       DATETIME2          NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT PK_Events PRIMARY KEY (Id),
    CONSTRAINT FK_Events_Organisers FOREIGN KEY (OrganiserId) REFERENCES dbo.Organisers(Id),
    CONSTRAINT CK_Events_EventType CHECK (EventType IN ('Run', 'Walk', 'Cycle'))
);
GO

CREATE TABLE dbo.Categories (
    Id              INT IDENTITY(1,1)  NOT NULL,
    EventId         INT                NOT NULL,
    Name            NVARCHAR(50)       NOT NULL,
    Description     NVARCHAR(255)      NULL,
    CONSTRAINT PK_Categories PRIMARY KEY (Id),
    CONSTRAINT FK_Categories_Events FOREIGN KEY (EventId) REFERENCES dbo.Events(Id),
    CONSTRAINT UQ_Categories_Event_Name UNIQUE (EventId, Name)
);
GO

CREATE TABLE dbo.Enrolments (
    Id              INT IDENTITY(1,1)  NOT NULL,
    ParticipantId   INT                NOT NULL,
    EventId         INT                NOT NULL,
    CategoryId      INT                NOT NULL,
    EnrolmentDate   DATETIME2          NOT NULL DEFAULT SYSDATETIME(),
    Status          NVARCHAR(20)       NOT NULL DEFAULT 'Confirmed',
    CONSTRAINT PK_Enrolments PRIMARY KEY (Id),
    CONSTRAINT FK_Enrolments_Participants FOREIGN KEY (ParticipantId) REFERENCES dbo.Participants(Id),
    CONSTRAINT FK_Enrolments_Events FOREIGN KEY (EventId) REFERENCES dbo.Events(Id),
    CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories(Id),
    CONSTRAINT UQ_Enrolments_Participant_Event UNIQUE (ParticipantId, EventId),
    CONSTRAINT CK_Enrolments_Status CHECK (Status IN ('Confirmed', 'Cancelled'))
);
GO

CREATE TABLE dbo.Results (
    Id              INT IDENTITY(1,1)  NOT NULL,
    EnrolmentId     INT                NOT NULL,
    FinishTime      TIME               NOT NULL,
    Position        INT                NOT NULL,
    CreatedAt       DATETIME2          NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT PK_Results PRIMARY KEY (Id),
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentId) REFERENCES dbo.Enrolments(Id),
    CONSTRAINT UQ_Results_Enrolment UNIQUE (EnrolmentId)
);
GO

/* ----------------------------------------------------------------
   SEED DATA
   IDs below assume a freshly created database, since IDENTITY
   starts at 1 and increments in the order rows are inserted.
   ---------------------------------------------------------------- */

-- Organisers (Id 1, 2)
-- PasswordHash values are placeholders only; Part 2's auth
-- endpoints will replace these with real hashed passwords.
INSERT INTO dbo.Organisers (FullName, Email, PasswordHash, Phone) VALUES
('Thabo Nkosi', 'thabo.nkosi@raceday.co.za', '$2a$11$PLACEHOLDERHASHVALUE0001', '0821234567'),
('Lindiwe Dlamini', 'lindiwe.dlamini@raceday.co.za', '$2a$11$PLACEHOLDERHASHVALUE0002', '0837654321');
GO

-- Participants (Id 1, 2)
INSERT INTO dbo.Participants (FullName, Email, PasswordHash, Phone) VALUES
('Sipho Mokoena', 'sipho.mokoena@example.com', '$2a$11$PLACEHOLDERHASHVALUE0003', '0731112222'),
('Anja van der Merwe', 'anja.vdm@example.com', '$2a$11$PLACEHOLDERHASHVALUE0004', '0793334444');
GO

-- Events: 3 events (Id 1,2,3), owned by the two organisers above
INSERT INTO dbo.Events (OrganiserId, Name, Description, EventDate, Location, DistanceKm, EventType) VALUES
(1, 'Tshwane Half Marathon', 'A scenic half marathon through the streets of Pretoria.', '2026-09-12', 'Pretoria, Gauteng', 21.10, 'Run'),
(2, 'Cape Town Cycle Classic', 'One of the largest timed cycle races in the world.', '2026-10-04', 'Cape Town, Western Cape', 109.00, 'Cycle'),
(1, 'Jozi Fun Walk', 'A family-friendly fun walk through Johannesburg.', '2026-08-30', 'Johannesburg, Gauteng', 5.00, 'Walk');
GO

-- Categories: two per event (Id 1-6)
INSERT INTO dbo.Categories (EventId, Name, Description) VALUES
(1, '21km Individual', 'Standard individual entry for the full half marathon distance.'),
(1, 'Under 20', 'Age category for participants under 20 years old.'),
(2, '109km Individual', 'Full-distance individual entry.'),
(2, 'Seniors (50+)', 'Age category for participants 50 years and older.'),
(3, '5km Fun Walk', 'Standard entry for the fun walk.'),
(3, 'Family', 'Category for participants entering as a family group.');
GO

-- Enrolments: sample entries linking Participants to Events + Categories (Id 1-4)
INSERT INTO dbo.Enrolments (ParticipantId, EventId, CategoryId) VALUES
(1, 1, 1),  -- Sipho enters Tshwane Half Marathon, 21km Individual
(2, 2, 3),  -- Anja enters Cape Town Cycle Classic, 109km Individual
(1, 3, 5),  -- Sipho enters Jozi Fun Walk, 5km Fun Walk
(2, 1, 1);  -- Anja enters Tshwane Half Marathon, 21km Individual
GO

-- Results: sample finish data for two completed enrolments (Id 1-2)
INSERT INTO dbo.Results (EnrolmentId, FinishTime, Position) VALUES
(3, '00:28:14', 12),   -- Sipho's Jozi Fun Walk result
(1, '01:38:47', 45);   -- Sipho's Tshwane Half Marathon result
GO
