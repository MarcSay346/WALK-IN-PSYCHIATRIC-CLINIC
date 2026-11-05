Wait-Time module (A3 module 4) - starter files

1. git pull (on the test_everyone branch) so you have the latest code.
2. Copy these files into the same places in the project:
     ClinicApi/Services/WaitTimeService.cs
     ClinicApi/Endpoints/WaitTimeEndpoints.cs
     ClinicApi.Tests/WaitTimeServiceTests.cs
3. In ClinicApi/Program.cs add two lines:
     builder.Services.AddWaitTime();   // right below builder.Services.AddNotificationAndHistory();
     app.MapWaitTimeEndpoints();       // right below app.MapNotificationAndHistoryEndpoints();
4. Fill in the TODOs (validation + formula in WaitTimeService, tests in WaitTimeServiceTests).
   Note: the first test only passes once you add the validation.
5. Run the tests:  cd ClinicApi.Tests  then  dotnet test
6. Try it: dotnet run in ClinicApi, then open
     http://localhost:5083/api/wait-time?position=3&durationMinutes=15
7. Optional: make the Queue Status section in joinQueue.html fetch /api/wait-time
   instead of showing the hard-coded "15 minutes".
8. Commit and push from your own account.
