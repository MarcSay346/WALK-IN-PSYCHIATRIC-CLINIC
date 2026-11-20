# Final Project – What Was Added and How to Ship It

QueueSmart / Radiant Mind Walk-In Psychiatric Clinic. Due **Fri Dec 4, 11:59 PM**.

## 1. What's new

| Requirement | What we built |
|---|---|
| **Reporting module** (admin only) | `admin-reports.html`: filters by date range and service. It has three reports: **queue usage statistics**, **services and queue activity**, and **patients and their queue history** (click a patient to see every visit). Each report has **Export CSV**, and the page has **Print / Save as PDF**. |
| **Smart feature** | **Learned wait times + shorter-wait suggestion.** The wait estimate uses each doctor's real pace from the database instead of a fixed visit length. If another open doctor would see you sooner, the app suggests switching (and Join Queue has a one-click **Switch**). |
| **Tests** | 72 new C# tests (305 total) + 2 new Jest files (104 JS tests total). |
| **Demo data** | `database/seed-demo.sql`: 6 more patients, ~3 weeks of visits, and people waiting right now. |

### New API

| Endpoint | Who | Returns |
|---|---|---|
| `GET /api/reports/users` | Admin | Patients + queue history |
| `GET /api/reports/services` | Admin | Service details + queue activity |
| `GET /api/reports/stats` | Admin | Served, canceled, avg/longest wait, busiest service and hour |
| `GET /api/wait-time/smart?service=Dr. Nate&position=3` | Anyone | Learned estimate + suggestion |

Report query string (all optional): `from=yyyy-MM-dd&to=yyyy-MM-dd&service=Dr. Nate&format=csv`.
Reports need the header `X-User-Id: <logged-in user id>`. The response is **401** if you're not logged in, **403** if you're not an admin, **400** for bad filters and **404** for an unknown service.

## 2. Run it

1. In SQL Server, run `database/schema.sql`, then **`database/seed-demo.sql`**.
2. `cd ClinicApi` → `dotnet run`. The console must say **`[Database] Using SQL Server (ClinicDb)`**.
   Reports and the smart estimate need the database. Without it they return 503, and the Join Queue page falls back to the old estimate.
3. Logins:
   - **Admin:** `admin@test.com` / `admin123`
   - **Patients:** `patient@test.com`, `maria.garcia@test.com`, `james.nguyen@test.com`, … all with password **`password1`**
4. Tests: `dotnet test ClinicApi.Tests` and `npm test`. Coverage: `./coverage.ps1` and `npm run coverage`.

## 3. Who commits what (TAs grade commit history)

Copy the files in, then each person commits **their own** files from **their own** GitHub account.
**Order: Marc first** (Chelsea's tests use the `ClinicTime` and `TestDatabase` helpers that Marc's commit adds). Kayson and Sara can commit anytime.

| Person | Files | Commit message |
|---|---|---|
| **Marc**: report backend | `ClinicApi/Services/ClinicTime.cs`, `ClinicApi/Data/ReportRepository.cs`, `ClinicApi/Services/ReportService.cs`, `ClinicApi/Endpoints/ReportEndpoints.cs`, `ClinicApi/Program.cs`, `ClinicApi.Tests/TestDatabase.cs`, `ClinicApi.Tests/ReportServiceTests.cs`, `ClinicApi.Tests/ReportEndpointTests.cs` | `Final: reporting module API (users, services, stats) with CSV export + tests` |
| **Chelsea**: smart feature | `ClinicApi/Data/SmartWaitRepository.cs`, `ClinicApi/Services/SmartWaitService.cs`, `ClinicApi/Endpoints/WaitTimeEndpoints.cs`, `ClinicApi.Tests/SmartWaitServiceTests.cs`, `smartWait.js`, `smartWait.test.js`, `joinQueue.html` | `Final: smart wait estimate from history + shorter-wait suggestion + tests` |
| **Kayson**: reports page | `admin-reports.html`, `adminReports.js`, `adminReports.test.js`, `admin.css`, `admin-dashboard.html`, `admin-services.html`, `admin-queue.html` | `Final: admin reports page with filters, CSV export, print to PDF + tests` |
| **Sara**: data, cleanup, docs | `database/seed-demo.sql`, `.gitignore`, `.mailmap`, `docs/`, plus the cleanup commands below | `Final: demo seed data, repo cleanup, docs and sample reports` |

Sara's cleanup (files that were committed by mistake; this deletes them from git, not from your laptop):

```bash
git rm -r --cached node_modules coverage
git rm "desktop.ini" "brain-logo (1) (1).png"
git commit -m "Remove node_modules, generated coverage and stray files"
```

After cleanup, anyone who pulls runs `npm install` once to get Jest back.

## 4. Text for the submission doc

### Smart feature: learned wait times + shorter-wait suggestion

**What makes it smart.** The old estimate multiplied the people ahead of you by each doctor's *scheduled* visit length, which was a fixed guess. Now the system learns from what actually happens. Every time an admin clicks *Serve Next*, the serve time is saved in `QueueEntry.EndedAt`. The time between two serves of the same doctor on the same day is how long one patient really took. The system averages those gaps over the last 30 days. It ignores gaps under 1 minute (double clicks) and over 3× the scheduled length (breaks or a closed queue). If there are fewer than 5 gaps, it falls back to the scheduled length. The estimate updates itself as the clinic keeps working. In our demo data, Dr. Nate is scheduled for 60 minutes but actually averages 48, so the old estimate overstated his waits by 25%. The system also compares every other **open** queue and suggests one when switching saves at least 10 minutes.

**How it improves the user experience.**
- Patients get a realistic wait time and see what it's based on ("Based on 48 recent visits…").
- They can choose a doctor with a shorter wait before joining, or switch with one click after joining.
- Shorter, more predictable waits matter a lot in a walk-in psychiatric clinic.

**How it fits into the existing system.**
- **A4 (database):** it reads the existing `Service`, `Queue` and `QueueEntry` tables, with no new tables.
- **A3 (API):** it's a new endpoint in the wait-time module, `GET /api/wait-time/smart`.
- **A2 (front end):** it's shown on the Join Queue page before and after joining.
- **Notifications:** the "joined" notification now carries the smarter estimate.
- **Switch:** it uses the existing leave/join queue APIs.

### Report generation example

Attach `docs/sample-reports/*.csv`, or better, export fresh ones from your own database during the demo.

- **How it's generated:** an admin opens **Reports**, picks a date range and service (optional), and clicks *Generate Report*. The page calls `/api/reports/*`. The server runs the queries against SQL Server (QueueEntry joined with Queue, Service, UserCredentials and UserProfile), computes the statistics in C#, and returns JSON for the page. *Export CSV* returns a `.csv` file, and *Print / Save as PDF* prints a clean version of the page.
- **What data it includes:**
  1. **Patients:** name, email, visit counts (served/canceled), average wait, and each visit (doctor, joined, ended, outcome, wait, priority).
  2. **Services:** duration, priority, queue open or closed, waiting now, joined, served, canceled, average wait.
  3. **Statistics:** total joined, served, canceled, still waiting, served rate, average and longest wait, busiest doctor, busiest hour.
- **Who can access it:** only administrators. The page sends you to login if you're not an admin, and the API checks the user's role in the database on every request. It returns 401 if you're not logged in and 403 for patients.

## 5. Demo script (~10 min, everyone talks)

Before starting:
1. Run `schema.sql` + `seed-demo.sql`.
2. Open two browser windows: one logged in as patient, one as admin.
3. Have SQL Server Management Studio open.

| Who | What to show |
|---|---|
| **Sara** (1 min) | Team, problem, architecture (HTML/JS → ASP.NET API → SQL Server) |
| **Chelsea** (3 min) | Log in as patient → Join Queue → pick Dr. Nate → *"about 2 hr 24 min… averaging 48 min per patient (scheduled for 60)"* → *"Dr. Cole has a shorter wait"* → join → **Switch** → check History and Notifications |
| **Kayson** (3 min) | Admin dashboard → edit a service → close/open a queue → Serve Next → refresh / show the row in SSMS (persistence) |
| **Marc** (3 min) | Reports → filter by date + doctor → stats, services, patient history → **Export CSV** → open in Excel → show that a patient account gets blocked |
| **Sara** (1 min) | Coverage report, wrap-up, questions |
