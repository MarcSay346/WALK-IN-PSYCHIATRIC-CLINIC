/* =====================================================================
   Walk-In Psychiatric Clinic (QueueSmart) - A4 database schema
   Microsoft SQL Server (T-SQL). Run the whole file to reset the database.

   Tables (A4 required -> ours):
     1. UserCredentials       -> UserCredentials
     2. UserProfile           -> UserProfile (+ notification preferences)
     3. Service               -> Service
     4. Queue                 -> Queue
     5. QueueEntry            -> QueueEntry
     6. Notification/History  -> Notification, VisitHistory
   ===================================================================== */

-- Drop old tables (children first), including the A3-era tables
DROP TABLE IF EXISTS VisitHistory;
DROP TABLE IF EXISTS Notification;
DROP TABLE IF EXISTS QueueEntry;
DROP TABLE IF EXISTS Queue;
DROP TABLE IF EXISTS Service;
DROP TABLE IF EXISTS UserProfile;
DROP TABLE IF EXISTS UserCredentials;
DROP TABLE IF EXISTS CheckIn;
DROP TABLE IF EXISTS Staff;
DROP TABLE IF EXISTS Patient;

/* ---------- 1. UserCredentials: login info ---------- */
CREATE TABLE UserCredentials (
    UserId       INT IDENTITY(1,1) CONSTRAINT PK_UserCredentials PRIMARY KEY,
    Email        NVARCHAR(100) NOT NULL CONSTRAINT UQ_UserCredentials_Email UNIQUE,
    PasswordHash NVARCHAR(256) NOT NULL,              -- ASP.NET Core PasswordHasher (PBKDF2), never plain text
    Role         NVARCHAR(10)  NOT NULL CONSTRAINT DF_UserCredentials_Role DEFAULT 'User'
        CONSTRAINT CK_UserCredentials_Role CHECK (Role IN ('User','Admin')),
    CreatedAt    DATETIME2     NOT NULL CONSTRAINT DF_UserCredentials_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_UserCredentials_Email CHECK (Email LIKE '%_@_%._%')
);

/* ---------- 2. UserProfile: personal details + preferences ---------- */
CREATE TABLE UserProfile (
    UserId           INT NOT NULL CONSTRAINT PK_UserProfile PRIMARY KEY,
    Email            NVARCHAR(100) NOT NULL CONSTRAINT UQ_UserProfile_Email UNIQUE,
    FirstName        NVARCHAR(50)  NOT NULL,
    LastName         NVARCHAR(50)  NOT NULL,
    DateOfBirth      DATE          NULL,
    Phone            NVARCHAR(20)  NULL,
    -- notification preferences (Notifications page settings)
    QueueUpdates     BIT NOT NULL CONSTRAINT DF_UserProfile_QueueUpdates  DEFAULT 1,
    StatusChanges    BIT NOT NULL CONSTRAINT DF_UserProfile_StatusChanges DEFAULT 1,
    AlertWithinSpots INT NOT NULL CONSTRAINT DF_UserProfile_AlertWithin   DEFAULT 3
        CONSTRAINT CK_UserProfile_AlertWithin CHECK (AlertWithinSpots BETWEEN 1 AND 10),
    Popups           BIT NOT NULL CONSTRAINT DF_UserProfile_Popups        DEFAULT 1,
    CONSTRAINT FK_UserProfile_User  FOREIGN KEY (UserId) REFERENCES UserCredentials(UserId),
    CONSTRAINT FK_UserProfile_Email FOREIGN KEY (Email)  REFERENCES UserCredentials(Email)
);

/* ---------- 3. Service: what the clinic offers ---------- */
CREATE TABLE Service (
    ServiceId       INT IDENTITY(1,1) CONSTRAINT PK_Service PRIMARY KEY,
    Name            NVARCHAR(100) NOT NULL CONSTRAINT UQ_Service_Name UNIQUE,
    Description     NVARCHAR(500) NOT NULL,
    DurationMinutes INT           NOT NULL
        CONSTRAINT CK_Service_Duration CHECK (DurationMinutes BETWEEN 1 AND 480),
    Priority        NVARCHAR(10)  NOT NULL
        CONSTRAINT CK_Service_Priority CHECK (Priority IN ('Low','Medium','High'))
);

/* ---------- 4. Queue: one queue per service ---------- */
CREATE TABLE Queue (
    QueueId   INT IDENTITY(1,1) CONSTRAINT PK_Queue PRIMARY KEY,
    ServiceId INT          NOT NULL,
    Status    NVARCHAR(10) NOT NULL CONSTRAINT DF_Queue_Status DEFAULT 'Open'
        CONSTRAINT CK_Queue_Status CHECK (Status IN ('Open','Closed')),
    CreatedAt DATETIME2    NOT NULL CONSTRAINT DF_Queue_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Queue_Service FOREIGN KEY (ServiceId) REFERENCES Service(ServiceId)
);
-- only one open queue per service at a time
CREATE UNIQUE INDEX UX_Queue_OpenPerService ON Queue(ServiceId) WHERE Status = 'Open';

/* ---------- 5. QueueEntry: users waiting in a queue ---------- */
CREATE TABLE QueueEntry (
    EntryId     INT IDENTITY(1,1) CONSTRAINT PK_QueueEntry PRIMARY KEY,
    QueueId     INT           NOT NULL,
    UserId      INT           NOT NULL,
    DisplayName NVARCHAR(100) NOT NULL,      -- name shown on the admin queue page
    Contact     NVARCHAR(100) NULL,
    Priority    TINYINT       NOT NULL CONSTRAINT DF_QueueEntry_Priority DEFAULT 3
        CONSTRAINT CK_QueueEntry_Priority CHECK (Priority BETWEEN 1 AND 3),   -- 1 Urgent, 2 High, 3 Standard
    Position    INT           NOT NULL CONSTRAINT CK_QueueEntry_Position CHECK (Position >= 1),
    JoinTime    DATETIME2     NOT NULL CONSTRAINT DF_QueueEntry_JoinTime DEFAULT SYSUTCDATETIME(),
    Status      NVARCHAR(10)  NOT NULL CONSTRAINT DF_QueueEntry_Status DEFAULT 'Waiting'
        CONSTRAINT CK_QueueEntry_Status CHECK (Status IN ('Waiting','Served','Canceled')),
    EndedAt     DATETIME2     NULL,
    CONSTRAINT FK_QueueEntry_Queue FOREIGN KEY (QueueId) REFERENCES Queue(QueueId),
    CONSTRAINT FK_QueueEntry_User  FOREIGN KEY (UserId)  REFERENCES UserCredentials(UserId)
);
-- a user can only be waiting once in the same queue
CREATE UNIQUE INDEX UX_QueueEntry_WaitingOnce ON QueueEntry(QueueId, UserId) WHERE Status = 'Waiting';

/* ---------- 6a. Notification ---------- */
CREATE TABLE Notification (
    NotificationId INT IDENTITY(1,1) CONSTRAINT PK_Notification PRIMARY KEY,
    UserId         INT           NOT NULL,
    Type           NVARCHAR(10)  NOT NULL CONSTRAINT DF_Notification_Type DEFAULT 'queue'
        CONSTRAINT CK_Notification_Type CHECK (Type IN ('queue','status')),
    Title          NVARCHAR(100) NOT NULL,
    Message        NVARCHAR(500) NOT NULL,
    Status         NVARCHAR(10)  NOT NULL CONSTRAINT DF_Notification_Status DEFAULT 'Sent'
        CONSTRAINT CK_Notification_Status CHECK (Status IN ('Sent','Viewed')),
    CreatedAt      DATETIME2     NOT NULL CONSTRAINT DF_Notification_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Notification_User FOREIGN KEY (UserId) REFERENCES UserCredentials(UserId)
);

/* ---------- 6b. VisitHistory: one row per queue visit ---------- */
CREATE TABLE VisitHistory (
    HistoryId   INT IDENTITY(1,1) CONSTRAINT PK_VisitHistory PRIMARY KEY,
    UserId      INT           NOT NULL,
    EntryId     INT           NULL,          -- the QueueEntry this visit came from
    ServiceName NVARCHAR(100) NOT NULL,      -- kept as text so history survives a renamed/deleted service
    JoinedAt    DATETIME2     NOT NULL,
    EndedAt     DATETIME2     NULL,
    Outcome     NVARCHAR(10)  NOT NULL CONSTRAINT DF_VisitHistory_Outcome DEFAULT 'Waiting'
        CONSTRAINT CK_VisitHistory_Outcome CHECK (Outcome IN ('Waiting','Served','Left')),
    CONSTRAINT FK_VisitHistory_User  FOREIGN KEY (UserId)  REFERENCES UserCredentials(UserId),
    CONSTRAINT FK_VisitHistory_Entry FOREIGN KEY (EntryId) REFERENCES QueueEntry(EntryId)
);

/* ---------- Indexes for the common lookups ---------- */
CREATE INDEX IX_QueueEntry_Queue     ON QueueEntry(QueueId, Status, Priority, JoinTime);
CREATE INDEX IX_QueueEntry_User      ON QueueEntry(UserId);
CREATE INDEX IX_Notification_User    ON Notification(UserId, Status, CreatedAt);
CREATE INDEX IX_VisitHistory_User    ON VisitHistory(UserId, JoinedAt);

/* =====================================================================
   Seed data
   Demo logins: patient@test.com / password1   admin@test.com / admin123
   (hashes are ASP.NET Core PasswordHasher V3 format)
   ===================================================================== */
INSERT INTO UserCredentials (Email, PasswordHash, Role) VALUES
('patient@test.com', 'AQAAAAEAAYagAAAAEDfENzcAj+yNLWjH22BIz08Hh1vaDEYMgr0uBn2iuuQ7SF5ize94Gi3hfvb2ArohwQ==', 'User'),
('admin@test.com',   'AQAAAAEAAYagAAAAEBXcolZp1Q8ZP7DrL0ue63X2+d3SbxTP87Py920olNDkT++kyKNdzx0w16vTc4a4IQ==', 'Admin');

INSERT INTO UserProfile (UserId, Email, FirstName, LastName, Phone) VALUES
(1, 'patient@test.com', 'Test',   'Patient', '713-555-0101'),
(2, 'admin@test.com',   'Clinic', 'Admin',   NULL);

INSERT INTO Service (Name, Description, DurationMinutes, Priority) VALUES
('Dr. Nate',   'General counseling visit', 60, 'High'),
('Dr. Valdez', 'General counseling visit', 45, 'Medium'),
('Dr. Cole',   'General counseling visit', 30, 'Low');

INSERT INTO Queue (ServiceId) VALUES (1), (2), (3);   -- one open queue per service

INSERT INTO QueueEntry (QueueId, UserId, DisplayName, Contact, Priority, Position, JoinTime, Status, EndedAt) VALUES
(2, 1, 'Test Patient', '713-555-0101', 3, 1, DATEADD(DAY,-7,SYSUTCDATETIME()), 'Served', DATEADD(MINUTE,45,DATEADD(DAY,-7,SYSUTCDATETIME())));

INSERT INTO VisitHistory (UserId, EntryId, ServiceName, JoinedAt, EndedAt, Outcome)
SELECT UserId, EntryId, 'Dr. Valdez', JoinTime, EndedAt, 'Served' FROM QueueEntry WHERE EntryId = 1;

INSERT INTO Notification (UserId, Type, Title, Message, Status) VALUES
(1, 'queue',  'Joined queue',   'You joined the queue for Dr. Valdez.', 'Viewed'),
(1, 'status', 'Visit complete', 'Your visit with Dr. Valdez is complete.', 'Sent');

/* ---------- Check results ---------- */
SELECT * FROM UserCredentials;
SELECT * FROM UserProfile;
SELECT * FROM Service;
SELECT * FROM Queue;
SELECT * FROM QueueEntry;
SELECT * FROM Notification;
SELECT * FROM VisitHistory;
