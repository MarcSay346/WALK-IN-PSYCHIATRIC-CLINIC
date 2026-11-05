warning: in the working copy of 'ClinicApi/Program.cs', LF will be replaced by CRLF the next time Git touches it
[1mdiff --git a/ClinicApi/Program.cs b/ClinicApi/Program.cs[m
[1mindex a336b5c..1cb6ba7 100644[m
[1m--- a/ClinicApi/Program.cs[m
[1m+++ b/ClinicApi/Program.cs[m
[36m@@ -28,7 +28,7 @@[m [mvar patients = new List<PatientAccount>();[m
 var patientsLock = new object();[m
 var nextPatientId = 1;[m
 [m
[31m-void AddPatient(string firstName, string lastName, string email, string password, string? phone = null, DateTime? dateOfBirth = null)[m
[32m+[m[32mvoid AddPatient(string firstName, string lastName, string email, string password, string? phone = null, DateTime? dateOfBirth = null, string role = "patient")[m
 {[m
     var normalizedEmail = email.Trim().ToLowerInvariant();[m
     patients.Add(new PatientAccount[m
[36m@@ -39,12 +39,14 @@[m [mvoid AddPatient(string firstName, string lastName, string email, string password[m
         Email = normalizedEmail,[m
         Phone = phone,[m
         DateOfBirth = dateOfBirth,[m
[31m-        PasswordHash = hasher.HashPassword(normalizedEmail, password)[m
[32m+[m[32m        PasswordHash = hasher.HashPassword(normalizedEmail, password),[m
[32m+[m[32m        Role = role[m
     });[m
 }[m
 [m
[31m-// demo account that always exists[m
[32m+[m[32m// demo accounts that always exist (one patient, one administrator)[m
 AddPatient("Test", "Patient", "patient@test.com", "password1");[m
[32m+[m[32mAddPatient("Clinic", "Admin", "admin@test.com", "admin123", role: "admin");[m
 [m
 // Serve the HTML/CSS/JS from the repo folder (one level up)[m
 var site = new PhysicalFileProvider(Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..")));[m
[36m@@ -89,7 +91,8 @@[m [mapp.MapPost("/api/login", (LoginRequest? req) =>[m
         hasher.VerifyHashedPassword(email, user.PasswordHash, req.Password) == PasswordVerificationResult.Failed)[m
         return Results.Unauthorized();[m
 [m
[31m-    return Results.Ok(new { user.PatientId, user.FirstName, user.LastName, user.Email });[m
[32m+[m[32m    // role tells the front end whether to open the patient pages or the admin pages[m
[32m+[m[32m    return Results.Ok(new { user.PatientId, user.FirstName, user.LastName, user.Email, user.Role });[m
 });[m
 [m
 // ---------- NOTIFICATIONS + HISTORY (/api/notifications, /api/history, /api/queue-events) ----------[m
[36m@@ -115,4 +118,5 @@[m [mclass PatientAccount[m
     public string? Phone { get; set; }[m
     public DateTime? DateOfBirth { get; set; }[m
     public string PasswordHash { get; set; } = "";[m
[32m+[m[32m    public string Role { get; set; } = "patient";   // "patient" or "admin"; /api/register always creates patients[m
 }[m
\ No newline at end of file[m
