/* =====================================================================
   Walk-In Psychiatric Clinic (QueueSmart) - DEMO DATA for the final project
   Run AFTER database/schema.sql (which resets everything).

   Adds: 6 more patients, ~3 weeks of past visits for every doctor, and a few
   people waiting right now, so the Reports page and the smart wait estimate
   have real numbers to show.

   Every demo patient's password is: password1
   Admin login: admin@test.com / admin123

   Real pace in this history (what the smart estimate should learn):
     Dr. Nate   ~48 min per patient (scheduled 60)
     Dr. Valdez ~50 min per patient (scheduled 45)
     Dr. Cole   ~35 min per patient (scheduled 30)
   Right now: 3 waiting for Dr. Nate, 1 for Dr. Valdez, nobody for Dr. Cole,
   so a patient joining Dr. Nate gets the "Dr. Cole has a shorter wait" suggestion.
   ===================================================================== */

SET NOCOUNT ON;
DECLARE @today DATE = CAST(SYSUTCDATETIME() AS DATE);   -- times below are UTC (9 AM Houston = 15:00 UTC)

/* ---------- patients ---------- */
INSERT INTO UserCredentials (Email, PasswordHash, Role) VALUES
('maria.garcia@test.com', 'AQAAAAEAAYagAAAAEDfENzcAj+yNLWjH22BIz08Hh1vaDEYMgr0uBn2iuuQ7SF5ize94Gi3hfvb2ArohwQ==', 'User'),
('james.nguyen@test.com', 'AQAAAAEAAYagAAAAEDfENzcAj+yNLWjH22BIz08Hh1vaDEYMgr0uBn2iuuQ7SF5ize94Gi3hfvb2ArohwQ==', 'User'),
('aisha.johnson@test.com', 'AQAAAAEAAYagAAAAEDfENzcAj+yNLWjH22BIz08Hh1vaDEYMgr0uBn2iuuQ7SF5ize94Gi3hfvb2ArohwQ==', 'User'),
('daniel.reyes@test.com', 'AQAAAAEAAYagAAAAEDfENzcAj+yNLWjH22BIz08Hh1vaDEYMgr0uBn2iuuQ7SF5ize94Gi3hfvb2ArohwQ==', 'User'),
('emily.chen@test.com', 'AQAAAAEAAYagAAAAEDfENzcAj+yNLWjH22BIz08Hh1vaDEYMgr0uBn2iuuQ7SF5ize94Gi3hfvb2ArohwQ==', 'User'),
('marcus.brown@test.com', 'AQAAAAEAAYagAAAAEDfENzcAj+yNLWjH22BIz08Hh1vaDEYMgr0uBn2iuuQ7SF5ize94Gi3hfvb2ArohwQ==', 'User');

INSERT INTO UserProfile (UserId, Email, FirstName, LastName, Phone)
SELECT c.UserId, c.Email, v.FirstName, v.LastName, v.Phone FROM UserCredentials c JOIN (VALUES
    ('maria.garcia@test.com', 'Maria', 'Garcia', '713-555-0102'),
    ('james.nguyen@test.com', 'James', 'Nguyen', '713-555-0103'),
    ('aisha.johnson@test.com', 'Aisha', 'Johnson', '713-555-0104'),
    ('daniel.reyes@test.com', 'Daniel', 'Reyes', '713-555-0105'),
    ('emily.chen@test.com', 'Emily', 'Chen', '713-555-0106'),
    ('marcus.brown@test.com', 'Marcus', 'Brown', '713-555-0107')
) AS v(Email, FirstName, LastName, Phone) ON v.Email = c.Email;

/* ---------- the open queue of each doctor ---------- */
DECLARE @qNate   INT = (SELECT q.QueueId FROM Queue q JOIN Service s ON s.ServiceId = q.ServiceId WHERE s.Name = 'Dr. Nate'   AND q.Status = 'Open');
DECLARE @qValdez INT = (SELECT q.QueueId FROM Queue q JOIN Service s ON s.ServiceId = q.ServiceId WHERE s.Name = 'Dr. Valdez' AND q.Status = 'Open');
DECLARE @qCole   INT = (SELECT q.QueueId FROM Queue q JOIN Service s ON s.ServiceId = q.ServiceId WHERE s.Name = 'Dr. Cole'   AND q.Status = 'Open');

/* ---------- past visits (served + a few canceled) ---------- */
INSERT INTO QueueEntry (QueueId, UserId, DisplayName, Contact, Priority, Position, JoinTime, Status, EndedAt)
SELECT v.QueueId, c.UserId, v.DisplayName, c.Email, v.Priority, 1, v.JoinTime, v.Status, v.EndedAt
FROM (VALUES
    (@qNate, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 905, CAST(DATEADD(DAY, -1, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 919, CAST(DATEADD(DAY, -1, @today) AS DATETIME2))),
    (@qNate, 'aisha.johnson@test.com', 'Aisha Johnson', 2, DATEADD(MINUTE, 944, CAST(DATEADD(DAY, -1, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 971, CAST(DATEADD(DAY, -1, @today) AS DATETIME2))),
    (@qNate, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 985, CAST(DATEADD(DAY, -1, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1021, CAST(DATEADD(DAY, -1, @today) AS DATETIME2))),
    (@qValdez, 'james.nguyen@test.com', 'James Nguyen', 2, DATEADD(MINUTE, 903, CAST(DATEADD(DAY, -1, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 926, CAST(DATEADD(DAY, -1, @today) AS DATETIME2))),
    (@qValdez, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 945, CAST(DATEADD(DAY, -1, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 974, CAST(DATEADD(DAY, -1, @today) AS DATETIME2))),
    (@qValdez, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 972, CAST(DATEADD(DAY, -1, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1021, CAST(DATEADD(DAY, -1, @today) AS DATETIME2))),
    (@qValdez, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 1009, CAST(DATEADD(DAY, -1, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1076, CAST(DATEADD(DAY, -1, @today) AS DATETIME2))),
    (@qValdez, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 1060, CAST(DATEADD(DAY, -1, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1121, CAST(DATEADD(DAY, -1, @today) AS DATETIME2))),
    (@qValdez, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 965, CAST(DATEADD(DAY, -1, @today) AS DATETIME2)), 'Canceled', DATEADD(MINUTE, 978, CAST(DATEADD(DAY, -1, @today) AS DATETIME2))),
    (@qCole, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 890, CAST(DATEADD(DAY, -1, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 905, CAST(DATEADD(DAY, -1, @today) AS DATETIME2))),
    (@qCole, 'daniel.reyes@test.com', 'Daniel Reyes', 2, DATEADD(MINUTE, 908, CAST(DATEADD(DAY, -1, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 943, CAST(DATEADD(DAY, -1, @today) AS DATETIME2))),
    (@qCole, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 926, CAST(DATEADD(DAY, -1, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 982, CAST(DATEADD(DAY, -1, @today) AS DATETIME2))),
    (@qCole, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 944, CAST(DATEADD(DAY, -1, @today) AS DATETIME2)), 'Canceled', DATEADD(MINUTE, 976, CAST(DATEADD(DAY, -1, @today) AS DATETIME2))),
    (@qNate, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 914, CAST(DATEADD(DAY, -2, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 927, CAST(DATEADD(DAY, -2, @today) AS DATETIME2))),
    (@qNate, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 949, CAST(DATEADD(DAY, -2, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 970, CAST(DATEADD(DAY, -2, @today) AS DATETIME2))),
    (@qNate, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 969, CAST(DATEADD(DAY, -2, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1022, CAST(DATEADD(DAY, -2, @today) AS DATETIME2))),
    (@qNate, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 1022, CAST(DATEADD(DAY, -2, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1074, CAST(DATEADD(DAY, -2, @today) AS DATETIME2))),
    (@qValdez, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 889, CAST(DATEADD(DAY, -2, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 908, CAST(DATEADD(DAY, -2, @today) AS DATETIME2))),
    (@qValdez, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 938, CAST(DATEADD(DAY, -2, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 959, CAST(DATEADD(DAY, -2, @today) AS DATETIME2))),
    (@qValdez, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 976, CAST(DATEADD(DAY, -2, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1014, CAST(DATEADD(DAY, -2, @today) AS DATETIME2))),
    (@qValdez, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 989, CAST(DATEADD(DAY, -2, @today) AS DATETIME2)), 'Canceled', DATEADD(MINUTE, 1006, CAST(DATEADD(DAY, -2, @today) AS DATETIME2))),
    (@qCole, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 903, CAST(DATEADD(DAY, -2, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 915, CAST(DATEADD(DAY, -2, @today) AS DATETIME2))),
    (@qCole, 'aisha.johnson@test.com', 'Aisha Johnson', 3, DATEADD(MINUTE, 912, CAST(DATEADD(DAY, -2, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 949, CAST(DATEADD(DAY, -2, @today) AS DATETIME2))),
    (@qCole, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 923, CAST(DATEADD(DAY, -2, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 980, CAST(DATEADD(DAY, -2, @today) AS DATETIME2))),
    (@qCole, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 936, CAST(DATEADD(DAY, -2, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1020, CAST(DATEADD(DAY, -2, @today) AS DATETIME2))),
    (@qCole, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 998, CAST(DATEADD(DAY, -2, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1051, CAST(DATEADD(DAY, -2, @today) AS DATETIME2))),
    (@qNate, 'aisha.johnson@test.com', 'Aisha Johnson', 3, DATEADD(MINUTE, 883, CAST(DATEADD(DAY, -5, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 906, CAST(DATEADD(DAY, -5, @today) AS DATETIME2))),
    (@qNate, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 922, CAST(DATEADD(DAY, -5, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 958, CAST(DATEADD(DAY, -5, @today) AS DATETIME2))),
    (@qNate, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 959, CAST(DATEADD(DAY, -5, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1003, CAST(DATEADD(DAY, -5, @today) AS DATETIME2))),
    (@qNate, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 977, CAST(DATEADD(DAY, -5, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1052, CAST(DATEADD(DAY, -5, @today) AS DATETIME2))),
    (@qValdez, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 900, CAST(DATEADD(DAY, -5, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 914, CAST(DATEADD(DAY, -5, @today) AS DATETIME2))),
    (@qValdez, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 936, CAST(DATEADD(DAY, -5, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 961, CAST(DATEADD(DAY, -5, @today) AS DATETIME2))),
    (@qValdez, 'aisha.johnson@test.com', 'Aisha Johnson', 3, DATEADD(MINUTE, 978, CAST(DATEADD(DAY, -5, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1012, CAST(DATEADD(DAY, -5, @today) AS DATETIME2))),
    (@qValdez, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 1012, CAST(DATEADD(DAY, -5, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1059, CAST(DATEADD(DAY, -5, @today) AS DATETIME2))),
    (@qCole, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 913, CAST(DATEADD(DAY, -5, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 923, CAST(DATEADD(DAY, -5, @today) AS DATETIME2))),
    (@qCole, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 936, CAST(DATEADD(DAY, -5, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 964, CAST(DATEADD(DAY, -5, @today) AS DATETIME2))),
    (@qCole, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 966, CAST(DATEADD(DAY, -5, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1001, CAST(DATEADD(DAY, -5, @today) AS DATETIME2))),
    (@qCole, 'aisha.johnson@test.com', 'Aisha Johnson', 3, DATEADD(MINUTE, 968, CAST(DATEADD(DAY, -5, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1031, CAST(DATEADD(DAY, -5, @today) AS DATETIME2))),
    (@qNate, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 920, CAST(DATEADD(DAY, -6, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 934, CAST(DATEADD(DAY, -6, @today) AS DATETIME2))),
    (@qNate, 'aisha.johnson@test.com', 'Aisha Johnson', 2, DATEADD(MINUTE, 951, CAST(DATEADD(DAY, -6, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 983, CAST(DATEADD(DAY, -6, @today) AS DATETIME2))),
    (@qNate, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 995, CAST(DATEADD(DAY, -6, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1034, CAST(DATEADD(DAY, -6, @today) AS DATETIME2))),
    (@qNate, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 1037, CAST(DATEADD(DAY, -6, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1079, CAST(DATEADD(DAY, -6, @today) AS DATETIME2))),
    (@qNate, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 1054, CAST(DATEADD(DAY, -6, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1127, CAST(DATEADD(DAY, -6, @today) AS DATETIME2))),
    (@qNate, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 920, CAST(DATEADD(DAY, -6, @today) AS DATETIME2)), 'Canceled', DATEADD(MINUTE, 947, CAST(DATEADD(DAY, -6, @today) AS DATETIME2))),
    (@qValdez, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 889, CAST(DATEADD(DAY, -6, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 913, CAST(DATEADD(DAY, -6, @today) AS DATETIME2))),
    (@qValdez, 'daniel.reyes@test.com', 'Daniel Reyes', 2, DATEADD(MINUTE, 935, CAST(DATEADD(DAY, -6, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 965, CAST(DATEADD(DAY, -6, @today) AS DATETIME2))),
    (@qValdez, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 959, CAST(DATEADD(DAY, -6, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1019, CAST(DATEADD(DAY, -6, @today) AS DATETIME2))),
    (@qValdez, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 1005, CAST(DATEADD(DAY, -6, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1069, CAST(DATEADD(DAY, -6, @today) AS DATETIME2))),
    (@qValdez, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 1056, CAST(DATEADD(DAY, -6, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1118, CAST(DATEADD(DAY, -6, @today) AS DATETIME2))),
    (@qCole, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 888, CAST(DATEADD(DAY, -6, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 905, CAST(DATEADD(DAY, -6, @today) AS DATETIME2))),
    (@qCole, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 905, CAST(DATEADD(DAY, -6, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 938, CAST(DATEADD(DAY, -6, @today) AS DATETIME2))),
    (@qCole, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 921, CAST(DATEADD(DAY, -6, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 971, CAST(DATEADD(DAY, -6, @today) AS DATETIME2))),
    (@qCole, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 965, CAST(DATEADD(DAY, -6, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1011, CAST(DATEADD(DAY, -6, @today) AS DATETIME2))),
    (@qCole, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 949, CAST(DATEADD(DAY, -6, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1043, CAST(DATEADD(DAY, -6, @today) AS DATETIME2))),
    (@qNate, 'maria.garcia@test.com', 'Maria Garcia', 2, DATEADD(MINUTE, 904, CAST(DATEADD(DAY, -7, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 920, CAST(DATEADD(DAY, -7, @today) AS DATETIME2))),
    (@qNate, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 937, CAST(DATEADD(DAY, -7, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 967, CAST(DATEADD(DAY, -7, @today) AS DATETIME2))),
    (@qNate, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 972, CAST(DATEADD(DAY, -7, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1019, CAST(DATEADD(DAY, -7, @today) AS DATETIME2))),
    (@qValdez, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 915, CAST(DATEADD(DAY, -7, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 938, CAST(DATEADD(DAY, -7, @today) AS DATETIME2))),
    (@qValdez, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 960, CAST(DATEADD(DAY, -7, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 991, CAST(DATEADD(DAY, -7, @today) AS DATETIME2))),
    (@qValdez, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 978, CAST(DATEADD(DAY, -7, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1037, CAST(DATEADD(DAY, -7, @today) AS DATETIME2))),
    (@qValdez, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 1008, CAST(DATEADD(DAY, -7, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1087, CAST(DATEADD(DAY, -7, @today) AS DATETIME2))),
    (@qValdez, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 1081, CAST(DATEADD(DAY, -7, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1141, CAST(DATEADD(DAY, -7, @today) AS DATETIME2))),
    (@qCole, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 930, CAST(DATEADD(DAY, -7, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 939, CAST(DATEADD(DAY, -7, @today) AS DATETIME2))),
    (@qCole, 'maria.garcia@test.com', 'Maria Garcia', 2, DATEADD(MINUTE, 949, CAST(DATEADD(DAY, -7, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 973, CAST(DATEADD(DAY, -7, @today) AS DATETIME2))),
    (@qCole, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 961, CAST(DATEADD(DAY, -7, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1006, CAST(DATEADD(DAY, -7, @today) AS DATETIME2))),
    (@qCole, 'aisha.johnson@test.com', 'Aisha Johnson', 3, DATEADD(MINUTE, 981, CAST(DATEADD(DAY, -7, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1042, CAST(DATEADD(DAY, -7, @today) AS DATETIME2))),
    (@qNate, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 900, CAST(DATEADD(DAY, -8, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 924, CAST(DATEADD(DAY, -8, @today) AS DATETIME2))),
    (@qNate, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 933, CAST(DATEADD(DAY, -8, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 966, CAST(DATEADD(DAY, -8, @today) AS DATETIME2))),
    (@qNate, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 961, CAST(DATEADD(DAY, -8, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1016, CAST(DATEADD(DAY, -8, @today) AS DATETIME2))),
    (@qNate, 'marcus.brown@test.com', 'Marcus Brown', 2, DATEADD(MINUTE, 998, CAST(DATEADD(DAY, -8, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1061, CAST(DATEADD(DAY, -8, @today) AS DATETIME2))),
    (@qNate, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 1025, CAST(DATEADD(DAY, -8, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1103, CAST(DATEADD(DAY, -8, @today) AS DATETIME2))),
    (@qValdez, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 904, CAST(DATEADD(DAY, -8, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 927, CAST(DATEADD(DAY, -8, @today) AS DATETIME2))),
    (@qValdez, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 958, CAST(DATEADD(DAY, -8, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 978, CAST(DATEADD(DAY, -8, @today) AS DATETIME2))),
    (@qValdez, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 993, CAST(DATEADD(DAY, -8, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1024, CAST(DATEADD(DAY, -8, @today) AS DATETIME2))),
    (@qValdez, 'marcus.brown@test.com', 'Marcus Brown', 2, DATEADD(MINUTE, 1033, CAST(DATEADD(DAY, -8, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1080, CAST(DATEADD(DAY, -8, @today) AS DATETIME2))),
    (@qValdez, 'aisha.johnson@test.com', 'Aisha Johnson', 3, DATEADD(MINUTE, 964, CAST(DATEADD(DAY, -8, @today) AS DATETIME2)), 'Canceled', DATEADD(MINUTE, 984, CAST(DATEADD(DAY, -8, @today) AS DATETIME2))),
    (@qCole, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 916, CAST(DATEADD(DAY, -8, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 940, CAST(DATEADD(DAY, -8, @today) AS DATETIME2))),
    (@qCole, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 954, CAST(DATEADD(DAY, -8, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 979, CAST(DATEADD(DAY, -8, @today) AS DATETIME2))),
    (@qCole, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 960, CAST(DATEADD(DAY, -8, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1014, CAST(DATEADD(DAY, -8, @today) AS DATETIME2))),
    (@qCole, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 1002, CAST(DATEADD(DAY, -8, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1049, CAST(DATEADD(DAY, -8, @today) AS DATETIME2))),
    (@qCole, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 997, CAST(DATEADD(DAY, -8, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1080, CAST(DATEADD(DAY, -8, @today) AS DATETIME2))),
    (@qNate, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 887, CAST(DATEADD(DAY, -9, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 908, CAST(DATEADD(DAY, -9, @today) AS DATETIME2))),
    (@qNate, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 923, CAST(DATEADD(DAY, -9, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 955, CAST(DATEADD(DAY, -9, @today) AS DATETIME2))),
    (@qNate, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 950, CAST(DATEADD(DAY, -9, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1005, CAST(DATEADD(DAY, -9, @today) AS DATETIME2))),
    (@qNate, 'emily.chen@test.com', 'Emily Chen', 2, DATEADD(MINUTE, 994, CAST(DATEADD(DAY, -9, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1054, CAST(DATEADD(DAY, -9, @today) AS DATETIME2))),
    (@qValdez, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 907, CAST(DATEADD(DAY, -9, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 932, CAST(DATEADD(DAY, -9, @today) AS DATETIME2))),
    (@qValdez, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 959, CAST(DATEADD(DAY, -9, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 987, CAST(DATEADD(DAY, -9, @today) AS DATETIME2))),
    (@qValdez, 'james.nguyen@test.com', 'James Nguyen', 2, DATEADD(MINUTE, 992, CAST(DATEADD(DAY, -9, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1043, CAST(DATEADD(DAY, -9, @today) AS DATETIME2))),
    (@qCole, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 900, CAST(DATEADD(DAY, -9, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 914, CAST(DATEADD(DAY, -9, @today) AS DATETIME2))),
    (@qCole, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 916, CAST(DATEADD(DAY, -9, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 947, CAST(DATEADD(DAY, -9, @today) AS DATETIME2))),
    (@qCole, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 941, CAST(DATEADD(DAY, -9, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 984, CAST(DATEADD(DAY, -9, @today) AS DATETIME2))),
    (@qCole, 'aisha.johnson@test.com', 'Aisha Johnson', 3, DATEADD(MINUTE, 961, CAST(DATEADD(DAY, -9, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1025, CAST(DATEADD(DAY, -9, @today) AS DATETIME2))),
    (@qCole, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 997, CAST(DATEADD(DAY, -9, @today) AS DATETIME2)), 'Canceled', DATEADD(MINUTE, 1037, CAST(DATEADD(DAY, -9, @today) AS DATETIME2))),
    (@qNate, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 891, CAST(DATEADD(DAY, -12, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 911, CAST(DATEADD(DAY, -12, @today) AS DATETIME2))),
    (@qNate, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 929, CAST(DATEADD(DAY, -12, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 962, CAST(DATEADD(DAY, -12, @today) AS DATETIME2))),
    (@qNate, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 969, CAST(DATEADD(DAY, -12, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1004, CAST(DATEADD(DAY, -12, @today) AS DATETIME2))),
    (@qNate, 'aisha.johnson@test.com', 'Aisha Johnson', 3, DATEADD(MINUTE, 967, CAST(DATEADD(DAY, -12, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1047, CAST(DATEADD(DAY, -12, @today) AS DATETIME2))),
    (@qNate, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 1022, CAST(DATEADD(DAY, -12, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1096, CAST(DATEADD(DAY, -12, @today) AS DATETIME2))),
    (@qValdez, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 911, CAST(DATEADD(DAY, -12, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 919, CAST(DATEADD(DAY, -12, @today) AS DATETIME2))),
    (@qValdez, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 938, CAST(DATEADD(DAY, -12, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 970, CAST(DATEADD(DAY, -12, @today) AS DATETIME2))),
    (@qValdez, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 970, CAST(DATEADD(DAY, -12, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1019, CAST(DATEADD(DAY, -12, @today) AS DATETIME2))),
    (@qValdez, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 1005, CAST(DATEADD(DAY, -12, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1070, CAST(DATEADD(DAY, -12, @today) AS DATETIME2))),
    (@qValdez, 'emily.chen@test.com', 'Emily Chen', 2, DATEADD(MINUTE, 1027, CAST(DATEADD(DAY, -12, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1120, CAST(DATEADD(DAY, -12, @today) AS DATETIME2))),
    (@qCole, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 925, CAST(DATEADD(DAY, -12, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 935, CAST(DATEADD(DAY, -12, @today) AS DATETIME2))),
    (@qCole, 'emily.chen@test.com', 'Emily Chen', 2, DATEADD(MINUTE, 930, CAST(DATEADD(DAY, -12, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 968, CAST(DATEADD(DAY, -12, @today) AS DATETIME2))),
    (@qCole, 'james.nguyen@test.com', 'James Nguyen', 2, DATEADD(MINUTE, 969, CAST(DATEADD(DAY, -12, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1003, CAST(DATEADD(DAY, -12, @today) AS DATETIME2))),
    (@qCole, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 968, CAST(DATEADD(DAY, -12, @today) AS DATETIME2)), 'Canceled', DATEADD(MINUTE, 995, CAST(DATEADD(DAY, -12, @today) AS DATETIME2))),
    (@qNate, 'aisha.johnson@test.com', 'Aisha Johnson', 2, DATEADD(MINUTE, 898, CAST(DATEADD(DAY, -13, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 908, CAST(DATEADD(DAY, -13, @today) AS DATETIME2))),
    (@qNate, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 917, CAST(DATEADD(DAY, -13, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 953, CAST(DATEADD(DAY, -13, @today) AS DATETIME2))),
    (@qNate, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 958, CAST(DATEADD(DAY, -13, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1005, CAST(DATEADD(DAY, -13, @today) AS DATETIME2))),
    (@qNate, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 1018, CAST(DATEADD(DAY, -13, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1059, CAST(DATEADD(DAY, -13, @today) AS DATETIME2))),
    (@qNate, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 926, CAST(DATEADD(DAY, -13, @today) AS DATETIME2)), 'Canceled', DATEADD(MINUTE, 952, CAST(DATEADD(DAY, -13, @today) AS DATETIME2))),
    (@qValdez, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 924, CAST(DATEADD(DAY, -13, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 937, CAST(DATEADD(DAY, -13, @today) AS DATETIME2))),
    (@qValdez, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 959, CAST(DATEADD(DAY, -13, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 992, CAST(DATEADD(DAY, -13, @today) AS DATETIME2))),
    (@qValdez, 'aisha.johnson@test.com', 'Aisha Johnson', 2, DATEADD(MINUTE, 996, CAST(DATEADD(DAY, -13, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1041, CAST(DATEADD(DAY, -13, @today) AS DATETIME2))),
    (@qValdez, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 977, CAST(DATEADD(DAY, -13, @today) AS DATETIME2)), 'Canceled', DATEADD(MINUTE, 1003, CAST(DATEADD(DAY, -13, @today) AS DATETIME2))),
    (@qCole, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 900, CAST(DATEADD(DAY, -13, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 909, CAST(DATEADD(DAY, -13, @today) AS DATETIME2))),
    (@qCole, 'aisha.johnson@test.com', 'Aisha Johnson', 3, DATEADD(MINUTE, 924, CAST(DATEADD(DAY, -13, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 950, CAST(DATEADD(DAY, -13, @today) AS DATETIME2))),
    (@qCole, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 944, CAST(DATEADD(DAY, -13, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 979, CAST(DATEADD(DAY, -13, @today) AS DATETIME2))),
    (@qCole, 'maria.garcia@test.com', 'Maria Garcia', 2, DATEADD(MINUTE, 945, CAST(DATEADD(DAY, -13, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1009, CAST(DATEADD(DAY, -13, @today) AS DATETIME2))),
    (@qNate, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 912, CAST(DATEADD(DAY, -14, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 933, CAST(DATEADD(DAY, -14, @today) AS DATETIME2))),
    (@qNate, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 960, CAST(DATEADD(DAY, -14, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 984, CAST(DATEADD(DAY, -14, @today) AS DATETIME2))),
    (@qNate, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 983, CAST(DATEADD(DAY, -14, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1038, CAST(DATEADD(DAY, -14, @today) AS DATETIME2))),
    (@qNate, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 1036, CAST(DATEADD(DAY, -14, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1092, CAST(DATEADD(DAY, -14, @today) AS DATETIME2))),
    (@qValdez, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 906, CAST(DATEADD(DAY, -14, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 923, CAST(DATEADD(DAY, -14, @today) AS DATETIME2))),
    (@qValdez, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 949, CAST(DATEADD(DAY, -14, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 977, CAST(DATEADD(DAY, -14, @today) AS DATETIME2))),
    (@qValdez, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 981, CAST(DATEADD(DAY, -14, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1033, CAST(DATEADD(DAY, -14, @today) AS DATETIME2))),
    (@qCole, 'patient@test.com', 'Test Patient', 2, DATEADD(MINUTE, 894, CAST(DATEADD(DAY, -14, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 917, CAST(DATEADD(DAY, -14, @today) AS DATETIME2))),
    (@qCole, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 907, CAST(DATEADD(DAY, -14, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 950, CAST(DATEADD(DAY, -14, @today) AS DATETIME2))),
    (@qCole, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 939, CAST(DATEADD(DAY, -14, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 989, CAST(DATEADD(DAY, -14, @today) AS DATETIME2))),
    (@qCole, 'aisha.johnson@test.com', 'Aisha Johnson', 3, DATEADD(MINUTE, 957, CAST(DATEADD(DAY, -14, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1024, CAST(DATEADD(DAY, -14, @today) AS DATETIME2))),
    (@qCole, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 956, CAST(DATEADD(DAY, -14, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1056, CAST(DATEADD(DAY, -14, @today) AS DATETIME2))),
    (@qCole, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 983, CAST(DATEADD(DAY, -14, @today) AS DATETIME2)), 'Canceled', DATEADD(MINUTE, 996, CAST(DATEADD(DAY, -14, @today) AS DATETIME2))),
    (@qNate, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 919, CAST(DATEADD(DAY, -15, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 928, CAST(DATEADD(DAY, -15, @today) AS DATETIME2))),
    (@qNate, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 959, CAST(DATEADD(DAY, -15, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 979, CAST(DATEADD(DAY, -15, @today) AS DATETIME2))),
    (@qNate, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 987, CAST(DATEADD(DAY, -15, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1028, CAST(DATEADD(DAY, -15, @today) AS DATETIME2))),
    (@qNate, 'maria.garcia@test.com', 'Maria Garcia', 2, DATEADD(MINUTE, 998, CAST(DATEADD(DAY, -15, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1072, CAST(DATEADD(DAY, -15, @today) AS DATETIME2))),
    (@qNate, 'aisha.johnson@test.com', 'Aisha Johnson', 2, DATEADD(MINUTE, 1065, CAST(DATEADD(DAY, -15, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1114, CAST(DATEADD(DAY, -15, @today) AS DATETIME2))),
    (@qValdez, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 887, CAST(DATEADD(DAY, -15, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 908, CAST(DATEADD(DAY, -15, @today) AS DATETIME2))),
    (@qValdez, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 929, CAST(DATEADD(DAY, -15, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 956, CAST(DATEADD(DAY, -15, @today) AS DATETIME2))),
    (@qValdez, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 967, CAST(DATEADD(DAY, -15, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1005, CAST(DATEADD(DAY, -15, @today) AS DATETIME2))),
    (@qValdez, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 1002, CAST(DATEADD(DAY, -15, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1054, CAST(DATEADD(DAY, -15, @today) AS DATETIME2))),
    (@qCole, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 884, CAST(DATEADD(DAY, -15, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 909, CAST(DATEADD(DAY, -15, @today) AS DATETIME2))),
    (@qCole, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 918, CAST(DATEADD(DAY, -15, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 946, CAST(DATEADD(DAY, -15, @today) AS DATETIME2))),
    (@qCole, 'aisha.johnson@test.com', 'Aisha Johnson', 2, DATEADD(MINUTE, 948, CAST(DATEADD(DAY, -15, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 978, CAST(DATEADD(DAY, -15, @today) AS DATETIME2))),
    (@qCole, 'patient@test.com', 'Test Patient', 2, DATEADD(MINUTE, 950, CAST(DATEADD(DAY, -15, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1013, CAST(DATEADD(DAY, -15, @today) AS DATETIME2))),
    (@qCole, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 960, CAST(DATEADD(DAY, -15, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1047, CAST(DATEADD(DAY, -15, @today) AS DATETIME2))),
    (@qCole, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 997, CAST(DATEADD(DAY, -15, @today) AS DATETIME2)), 'Canceled', DATEADD(MINUTE, 1016, CAST(DATEADD(DAY, -15, @today) AS DATETIME2))),
    (@qNate, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 909, CAST(DATEADD(DAY, -16, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 932, CAST(DATEADD(DAY, -16, @today) AS DATETIME2))),
    (@qNate, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 939, CAST(DATEADD(DAY, -16, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 977, CAST(DATEADD(DAY, -16, @today) AS DATETIME2))),
    (@qNate, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 982, CAST(DATEADD(DAY, -16, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1030, CAST(DATEADD(DAY, -16, @today) AS DATETIME2))),
    (@qNate, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 1032, CAST(DATEADD(DAY, -16, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1075, CAST(DATEADD(DAY, -16, @today) AS DATETIME2))),
    (@qNate, 'aisha.johnson@test.com', 'Aisha Johnson', 3, DATEADD(MINUTE, 1043, CAST(DATEADD(DAY, -16, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1125, CAST(DATEADD(DAY, -16, @today) AS DATETIME2))),
    (@qValdez, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 910, CAST(DATEADD(DAY, -16, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 918, CAST(DATEADD(DAY, -16, @today) AS DATETIME2))),
    (@qValdez, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 935, CAST(DATEADD(DAY, -16, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 964, CAST(DATEADD(DAY, -16, @today) AS DATETIME2))),
    (@qValdez, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 981, CAST(DATEADD(DAY, -16, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1017, CAST(DATEADD(DAY, -16, @today) AS DATETIME2))),
    (@qCole, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 901, CAST(DATEADD(DAY, -16, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 920, CAST(DATEADD(DAY, -16, @today) AS DATETIME2))),
    (@qCole, 'aisha.johnson@test.com', 'Aisha Johnson', 3, DATEADD(MINUTE, 929, CAST(DATEADD(DAY, -16, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 958, CAST(DATEADD(DAY, -16, @today) AS DATETIME2))),
    (@qCole, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 946, CAST(DATEADD(DAY, -16, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 994, CAST(DATEADD(DAY, -16, @today) AS DATETIME2))),
    (@qCole, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 970, CAST(DATEADD(DAY, -16, @today) AS DATETIME2)), 'Canceled', DATEADD(MINUTE, 993, CAST(DATEADD(DAY, -16, @today) AS DATETIME2))),
    (@qNate, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 896, CAST(DATEADD(DAY, -19, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 917, CAST(DATEADD(DAY, -19, @today) AS DATETIME2))),
    (@qNate, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 939, CAST(DATEADD(DAY, -19, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 969, CAST(DATEADD(DAY, -19, @today) AS DATETIME2))),
    (@qNate, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 967, CAST(DATEADD(DAY, -19, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1016, CAST(DATEADD(DAY, -19, @today) AS DATETIME2))),
    (@qValdez, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 922, CAST(DATEADD(DAY, -19, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 932, CAST(DATEADD(DAY, -19, @today) AS DATETIME2))),
    (@qValdez, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 949, CAST(DATEADD(DAY, -19, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 978, CAST(DATEADD(DAY, -19, @today) AS DATETIME2))),
    (@qValdez, 'aisha.johnson@test.com', 'Aisha Johnson', 3, DATEADD(MINUTE, 978, CAST(DATEADD(DAY, -19, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1029, CAST(DATEADD(DAY, -19, @today) AS DATETIME2))),
    (@qCole, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 920, CAST(DATEADD(DAY, -19, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 937, CAST(DATEADD(DAY, -19, @today) AS DATETIME2))),
    (@qCole, 'aisha.johnson@test.com', 'Aisha Johnson', 3, DATEADD(MINUTE, 945, CAST(DATEADD(DAY, -19, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 968, CAST(DATEADD(DAY, -19, @today) AS DATETIME2))),
    (@qCole, 'james.nguyen@test.com', 'James Nguyen', 2, DATEADD(MINUTE, 968, CAST(DATEADD(DAY, -19, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1007, CAST(DATEADD(DAY, -19, @today) AS DATETIME2))),
    (@qNate, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 928, CAST(DATEADD(DAY, -20, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 938, CAST(DATEADD(DAY, -20, @today) AS DATETIME2))),
    (@qNate, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 954, CAST(DATEADD(DAY, -20, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 983, CAST(DATEADD(DAY, -20, @today) AS DATETIME2))),
    (@qNate, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 972, CAST(DATEADD(DAY, -20, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1026, CAST(DATEADD(DAY, -20, @today) AS DATETIME2))),
    (@qNate, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 998, CAST(DATEADD(DAY, -20, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1071, CAST(DATEADD(DAY, -20, @today) AS DATETIME2))),
    (@qValdez, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 901, CAST(DATEADD(DAY, -20, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 917, CAST(DATEADD(DAY, -20, @today) AS DATETIME2))),
    (@qValdez, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 940, CAST(DATEADD(DAY, -20, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 964, CAST(DATEADD(DAY, -20, @today) AS DATETIME2))),
    (@qValdez, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 981, CAST(DATEADD(DAY, -20, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1014, CAST(DATEADD(DAY, -20, @today) AS DATETIME2))),
    (@qValdez, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 1016, CAST(DATEADD(DAY, -20, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1069, CAST(DATEADD(DAY, -20, @today) AS DATETIME2))),
    (@qValdez, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 934, CAST(DATEADD(DAY, -20, @today) AS DATETIME2)), 'Canceled', DATEADD(MINUTE, 969, CAST(DATEADD(DAY, -20, @today) AS DATETIME2))),
    (@qCole, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 894, CAST(DATEADD(DAY, -20, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 912, CAST(DATEADD(DAY, -20, @today) AS DATETIME2))),
    (@qCole, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 921, CAST(DATEADD(DAY, -20, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 953, CAST(DATEADD(DAY, -20, @today) AS DATETIME2))),
    (@qCole, 'aisha.johnson@test.com', 'Aisha Johnson', 3, DATEADD(MINUTE, 924, CAST(DATEADD(DAY, -20, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 983, CAST(DATEADD(DAY, -20, @today) AS DATETIME2))),
    (@qCole, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 946, CAST(DATEADD(DAY, -20, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1016, CAST(DATEADD(DAY, -20, @today) AS DATETIME2))),
    (@qCole, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 1014, CAST(DATEADD(DAY, -20, @today) AS DATETIME2)), 'Canceled', DATEADD(MINUTE, 1048, CAST(DATEADD(DAY, -20, @today) AS DATETIME2))),
    (@qNate, 'james.nguyen@test.com', 'James Nguyen', 3, DATEADD(MINUTE, 903, CAST(DATEADD(DAY, -21, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 912, CAST(DATEADD(DAY, -21, @today) AS DATETIME2))),
    (@qNate, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 938, CAST(DATEADD(DAY, -21, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 961, CAST(DATEADD(DAY, -21, @today) AS DATETIME2))),
    (@qNate, 'aisha.johnson@test.com', 'Aisha Johnson', 3, DATEADD(MINUTE, 972, CAST(DATEADD(DAY, -21, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1008, CAST(DATEADD(DAY, -21, @today) AS DATETIME2))),
    (@qNate, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 1019, CAST(DATEADD(DAY, -21, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1062, CAST(DATEADD(DAY, -21, @today) AS DATETIME2))),
    (@qNate, 'marcus.brown@test.com', 'Marcus Brown', 3, DATEADD(MINUTE, 1045, CAST(DATEADD(DAY, -21, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1112, CAST(DATEADD(DAY, -21, @today) AS DATETIME2))),
    (@qValdez, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 908, CAST(DATEADD(DAY, -21, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 930, CAST(DATEADD(DAY, -21, @today) AS DATETIME2))),
    (@qValdez, 'aisha.johnson@test.com', 'Aisha Johnson', 2, DATEADD(MINUTE, 943, CAST(DATEADD(DAY, -21, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 985, CAST(DATEADD(DAY, -21, @today) AS DATETIME2))),
    (@qValdez, 'daniel.reyes@test.com', 'Daniel Reyes', 3, DATEADD(MINUTE, 988, CAST(DATEADD(DAY, -21, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1032, CAST(DATEADD(DAY, -21, @today) AS DATETIME2))),
    (@qValdez, 'maria.garcia@test.com', 'Maria Garcia', 2, DATEADD(MINUTE, 1020, CAST(DATEADD(DAY, -21, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 1077, CAST(DATEADD(DAY, -21, @today) AS DATETIME2))),
    (@qCole, 'emily.chen@test.com', 'Emily Chen', 3, DATEADD(MINUTE, 901, CAST(DATEADD(DAY, -21, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 911, CAST(DATEADD(DAY, -21, @today) AS DATETIME2))),
    (@qCole, 'aisha.johnson@test.com', 'Aisha Johnson', 3, DATEADD(MINUTE, 907, CAST(DATEADD(DAY, -21, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 949, CAST(DATEADD(DAY, -21, @today) AS DATETIME2))),
    (@qCole, 'patient@test.com', 'Test Patient', 3, DATEADD(MINUTE, 936, CAST(DATEADD(DAY, -21, @today) AS DATETIME2)), 'Served', DATEADD(MINUTE, 980, CAST(DATEADD(DAY, -21, @today) AS DATETIME2))),
    (@qCole, 'maria.garcia@test.com', 'Maria Garcia', 3, DATEADD(MINUTE, 941, CAST(DATEADD(DAY, -21, @today) AS DATETIME2)), 'Canceled', DATEADD(MINUTE, 981, CAST(DATEADD(DAY, -21, @today) AS DATETIME2)))
) AS v(QueueId, Email, DisplayName, Priority, JoinTime, Status, EndedAt)
JOIN UserCredentials c ON c.Email = v.Email;

/* ---------- waiting right now ---------- */
INSERT INTO QueueEntry (QueueId, UserId, DisplayName, Contact, Priority, Position, JoinTime, Status)
SELECT v.QueueId, c.UserId, v.DisplayName, c.Email, 3, v.Position, DATEADD(MINUTE, -v.MinutesAgo, SYSUTCDATETIME()), 'Waiting'
FROM (VALUES
    (@qNate,   'maria.garcia@test.com',  'Maria Garcia',  1, 35),
    (@qNate,   'james.nguyen@test.com',  'James Nguyen',  2, 20),
    (@qNate,   'aisha.johnson@test.com', 'Aisha Johnson', 3, 8),
    (@qValdez, 'daniel.reyes@test.com',  'Daniel Reyes',  1, 12)
) AS v(QueueId, Email, DisplayName, Position, MinutesAgo)
JOIN UserCredentials c ON c.Email = v.Email;

/* ---------- matching history rows (History page) ---------- */
INSERT INTO VisitHistory (UserId, EntryId, ServiceName, JoinedAt, EndedAt, Outcome)
SELECT e.UserId, e.EntryId, s.Name, e.JoinTime, e.EndedAt,
       CASE e.Status WHEN 'Served' THEN 'Served' WHEN 'Canceled' THEN 'Left' ELSE 'Waiting' END
FROM QueueEntry e
JOIN Queue q   ON q.QueueId = e.QueueId
JOIN Service s ON s.ServiceId = q.ServiceId
WHERE NOT EXISTS (SELECT 1 FROM VisitHistory h WHERE h.EntryId = e.EntryId);

/* ---------- a few notifications ---------- */
INSERT INTO Notification (UserId, Type, Title, Message, Status, CreatedAt)
SELECT c.UserId, v.Type, v.Title, v.Message, v.Status, DATEADD(MINUTE, -v.MinutesAgo, SYSUTCDATETIME())
FROM (VALUES
    ('maria.garcia@test.com',  'queue',  'Joined queue',     'You joined the queue for Dr. Nate.',            'Viewed', 35),
    ('james.nguyen@test.com',  'queue',  'Joined queue',     'You joined the queue for Dr. Nate.',            'Sent',   20),
    ('aisha.johnson@test.com', 'queue',  'Joined queue',     'You joined the queue for Dr. Nate.',            'Sent',   8),
    ('daniel.reyes@test.com',  'queue',  'Joined queue',     'You joined the queue for Dr. Valdez.',          'Sent',   12),
    ('patient@test.com',       'status', 'Visit complete',   'Your visit with Dr. Cole is complete.',         'Sent',   1440)
) AS v(Email, Type, Title, Message, Status, MinutesAgo)
JOIN UserCredentials c ON c.Email = v.Email;

/* ---------- check ---------- */
SELECT s.Name, e.Status, COUNT(*) AS Visits
FROM QueueEntry e JOIN Queue q ON q.QueueId = e.QueueId JOIN Service s ON s.ServiceId = q.ServiceId
GROUP BY s.Name, e.Status ORDER BY s.Name, e.Status;
