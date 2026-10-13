/* ===== Walk-In Psychiatric Clinic: full reset ===== */

-- Drop old tables (children first)
DROP TABLE IF EXISTS Notification;
DROP TABLE IF EXISTS CheckIn;
DROP TABLE IF EXISTS Staff;
DROP TABLE IF EXISTS Patient;

-- Patient
CREATE TABLE Patient (
    PatientId             INT IDENTITY(1,1) CONSTRAINT PK_Patient PRIMARY KEY,
    FirstName             NVARCHAR(50)  NOT NULL,
    LastName              NVARCHAR(50)  NOT NULL,
    DateOfBirth           DATE          NOT NULL,
    Phone                 NVARCHAR(20)  NULL,
    Email                 NVARCHAR(100) NULL,
    EmergencyContactName  NVARCHAR(100) NULL,
    EmergencyContactPhone NVARCHAR(20)  NULL,
    CreatedAt             DATETIME2     NOT NULL CONSTRAINT DF_Patient_CreatedAt DEFAULT SYSUTCDATETIME()
);

-- Staff
CREATE TABLE Staff (
    StaffId     INT IDENTITY(1,1) CONSTRAINT PK_Staff PRIMARY KEY,
    FirstName   NVARCHAR(50)  NOT NULL,
    LastName    NVARCHAR(50)  NOT NULL,
    Role        NVARCHAR(20)  NOT NULL
        CONSTRAINT CK_Staff_Role CHECK (Role IN ('Receptionist','Psychiatrist','Therapist','Nurse','Admin')),
    Email       NVARCHAR(100) NOT NULL CONSTRAINT UQ_Staff_Email UNIQUE,
    IsAvailable BIT           NOT NULL CONSTRAINT DF_Staff_IsAvailable DEFAULT 1
);

-- CheckIn
CREATE TABLE CheckIn (
    CheckInId      INT IDENTITY(1,1) CONSTRAINT PK_CheckIn PRIMARY KEY,
    PatientId      INT NOT NULL,
    ReceptionistId INT NOT NULL,
    ProviderId     INT NULL,
    ArrivedAt      DATETIME2 NOT NULL CONSTRAINT DF_CheckIn_ArrivedAt DEFAULT SYSUTCDATETIME(),
    ReasonForVisit NVARCHAR(500) NULL,
    Urgency        NVARCHAR(10) NOT NULL CONSTRAINT DF_CheckIn_Urgency DEFAULT 'Routine'
        CONSTRAINT CK_CheckIn_Urgency CHECK (Urgency IN ('Routine','Urgent','Crisis')),
    Status         NVARCHAR(20) NOT NULL CONSTRAINT DF_CheckIn_Status DEFAULT 'Waiting'
        CONSTRAINT CK_CheckIn_Status CHECK (Status IN ('Waiting','InSession','Completed','LeftWithoutBeingSeen')),
    SeenAt         DATETIME2 NULL,
    CompletedAt    DATETIME2 NULL,
    CONSTRAINT FK_CheckIn_Patient      FOREIGN KEY (PatientId)      REFERENCES Patient(PatientId),
    CONSTRAINT FK_CheckIn_Receptionist FOREIGN KEY (ReceptionistId) REFERENCES Staff(StaffId),
    CONSTRAINT FK_CheckIn_Provider     FOREIGN KEY (ProviderId)     REFERENCES Staff(StaffId)
);

-- Notification
CREATE TABLE Notification (
    NotificationId INT IDENTITY(1,1) CONSTRAINT PK_Notification PRIMARY KEY,
    PatientId      INT NULL,
    StaffId        INT NULL,
    CheckInId      INT NULL,
    Message        NVARCHAR(500) NOT NULL,
    IsRead         BIT       NOT NULL CONSTRAINT DF_Notification_IsRead DEFAULT 0,
    CreatedAt      DATETIME2 NOT NULL CONSTRAINT DF_Notification_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Notification_Patient FOREIGN KEY (PatientId) REFERENCES Patient(PatientId),
    CONSTRAINT FK_Notification_Staff   FOREIGN KEY (StaffId)   REFERENCES Staff(StaffId),
    CONSTRAINT FK_Notification_CheckIn FOREIGN KEY (CheckInId) REFERENCES CheckIn(CheckInId),
    CONSTRAINT CK_Notification_Recipient CHECK (PatientId IS NOT NULL OR StaffId IS NOT NULL)
);

-- Indexes
CREATE INDEX IX_CheckIn_PatientId      ON CheckIn(PatientId);
CREATE INDEX IX_CheckIn_ReceptionistId ON CheckIn(ReceptionistId);
CREATE INDEX IX_CheckIn_ProviderId     ON CheckIn(ProviderId);
CREATE INDEX IX_CheckIn_Queue          ON CheckIn(Status, ArrivedAt);
CREATE INDEX IX_Notification_PatientId ON Notification(PatientId, IsRead);
CREATE INDEX IX_Notification_StaffId   ON Notification(StaffId, IsRead);
CREATE INDEX IX_Notification_CheckInId ON Notification(CheckInId);

-- Staff
INSERT INTO Staff (FirstName, LastName, Role, Email) VALUES
('Staff', '1', 'Receptionist', 'staff1@clinic.test'),
('Staff', '2', 'Receptionist', 'staff2@clinic.test'),
('Staff', '3', 'Psychiatrist', 'staff3@clinic.test'),
('Staff', '4', 'Psychiatrist', 'staff4@clinic.test');

-- Patients (team)
INSERT INTO Patient (FirstName, LastName, DateOfBirth, Phone, Email, EmergencyContactName, EmergencyContactPhone) VALUES
('Kayson',  'Nyugen', '2003-01-01', '713-555-0101', 'knyugen@uh.edu', 'Emergency Contact', '713-555-0102'),
('Sara',    'Montes', '2003-01-01', '713-555-0201', 'smontes@uh.edu', 'Emergency Contact', '713-555-0202'),
('Chelsea', 'Dissen', '2003-01-01', '713-555-0301', 'cdissen@uh.edu', 'Emergency Contact', '713-555-0302'),
('Marc',    'Say',    '2003-01-01', '713-555-0401', 'msay@uh.edu',    'Emergency Contact', '713-555-0402');

-- Check-ins
INSERT INTO CheckIn (PatientId, ReceptionistId, ProviderId, ReasonForVisit, Urgency, Status, SeenAt, CompletedAt) VALUES
(1, 1, 3,    'Test visit - routine', 'Routine', 'Completed', DATEADD(MINUTE,-90,SYSUTCDATETIME()), DATEADD(MINUTE,-60,SYSUTCDATETIME())),
(2, 1, 4,    'Test visit - urgent',  'Urgent',  'InSession', DATEADD(MINUTE,-15,SYSUTCDATETIME()), NULL),
(3, 2, NULL, 'Test visit - routine', 'Routine', 'Waiting',   NULL, NULL),
(4, 2, NULL, 'Test visit - crisis',  'Crisis',  'Waiting',   NULL, NULL);

-- Notifications
INSERT INTO Notification (PatientId, StaffId, CheckInId, Message) VALUES
(3,    NULL, 3, 'You are checked in. Estimated wait: 25 minutes.'),
(4,    NULL, 4, 'A provider will be with you shortly.'),
(NULL, 3,    4, 'Crisis walk-in waiting: please see patient in Room 2.'),
(2,    NULL, 2, 'Staff 4 is ready for you.');

-- Check results
SELECT * FROM Patient;
SELECT * FROM Staff;
SELECT * FROM CheckIn;
SELECT * FROM Notification;