const reports = require("./adminReports");

function fakeStorage(user) {
  return { getItem: () => (user === undefined ? null : typeof user === "string" ? user : JSON.stringify(user)) };
}

describe("reports page: who can see it", () => {
  test("an admin is allowed", () => {
    const admin = reports.currentAdmin(fakeStorage({ userId: 2, role: "admin", name: "Clinic Admin" }));
    expect(admin.userId).toBe(2);
  });

  test("role check ignores upper/lower case", () => {
    expect(reports.currentAdmin(fakeStorage({ userId: 2, role: "Admin" }))).not.toBeNull();
  });

  test("a patient is not allowed", () => {
    expect(reports.currentAdmin(fakeStorage({ userId: 1, role: "user" }))).toBeNull();
  });

  test("nobody logged in, missing id, or broken storage -> not allowed", () => {
    expect(reports.currentAdmin(fakeStorage())).toBeNull();
    expect(reports.currentAdmin(fakeStorage({ role: "admin" }))).toBeNull();
    expect(reports.currentAdmin(fakeStorage("{not json"))).toBeNull();
  });
});

describe("reports page: query string", () => {
  test("no filters and json -> empty", () => {
    expect(reports.reportQuery({}, "json")).toBe("");
    expect(reports.reportQuery(null)).toBe("");
  });

  test("only includes the filters that are set", () => {
    expect(reports.reportQuery({ from: "2026-11-01", to: "", service: "Dr. Nate" }, "json"))
      .toBe("?from=2026-11-01&service=Dr.+Nate");
  });

  test("adds format=csv for exports", () => {
    expect(reports.reportQuery({ to: "2026-11-30" }, "csv")).toBe("?to=2026-11-30&format=csv");
  });
});

describe("reports page: filter check", () => {
  test("from after to is rejected", () => {
    expect(reports.validateFilters({ from: "2026-11-05", to: "2026-11-01" })).toMatch(/on or after/);
  });

  test("valid or partial ranges are fine", () => {
    expect(reports.validateFilters({ from: "2026-11-01", to: "2026-11-01" })).toBe("");
    expect(reports.validateFilters({ from: "2026-11-01" })).toBe("");
    expect(reports.validateFilters()).toBe("");
  });
});

describe("reports page: formatting", () => {
  test.each([
    [null, "—"],
    [undefined, "—"],
    [0, "0 min"],
    [37.5, "38 min"],
    [60, "1 hr"],
    [96, "1 hr 36 min"]
  ])("formatMinutes(%p) -> %p", (minutes, expected) => {
    expect(reports.formatMinutes(minutes)).toBe(expected);
  });

  test.each([
    [null, "—"],
    [0, "12:00 AM"],
    [9, "9:00 AM"],
    [12, "12:00 PM"],
    [14, "2:00 PM"]
  ])("formatHour(%p) -> %p", (hour, expected) => {
    expect(reports.formatHour(hour)).toBe(expected);
  });

  test("formatDateTime shows a readable clinic time", () => {
    expect(reports.formatDateTime("2026-11-02T09:05:00")).toBe("Nov 2, 2026 9:05 AM");
    expect(reports.formatDateTime("2026-11-02T14:30:00-06:00")).toBe("Nov 2, 2026 2:30 PM");
    expect(reports.formatDateTime(null)).toBe("—");
    expect(reports.formatDateTime("garbage")).toBe("garbage");
  });

  test("describeFilters says what the report covers", () => {
    expect(reports.describeFilters({})).toBe("All services · All dates");
    expect(reports.describeFilters({ service: "Dr. Cole", from: "2026-11-01", to: "2026-11-30" }))
      .toBe("Dr. Cole · Nov 1, 2026 – Nov 30, 2026");
    expect(reports.describeFilters({ from: "2026-11-01" })).toBe("All services · From Nov 1, 2026");
    expect(reports.describeFilters({ to: "2026-11-30" })).toBe("All services · Up to Nov 30, 2026");
  });

  test("escapeHtml stops names from breaking the page", () => {
    expect(reports.escapeHtml('<b>"Ana" & Co\'s</b>')).toBe("&lt;b&gt;&quot;Ana&quot; &amp; Co&#39;s&lt;/b&gt;");
    expect(reports.escapeHtml(null)).toBe("");
  });
});

describe("reports page: error messages", () => {
  test("shows field errors from the API", () => {
    expect(reports.errorMessage(400, { errors: { from: "From date must be a date like 2026-11-30." } }))
      .toBe("From date must be a date like 2026-11-30.");
  });

  test("shows the API's error text", () => {
    expect(reports.errorMessage(404, { error: "There is no service named 'X'." })).toBe("There is no service named 'X'.");
  });

  test("401/403 without a body -> admin message, others -> status code", () => {
    expect(reports.errorMessage(403, null)).toMatch(/Only administrators/);
    expect(reports.errorMessage(500, null)).toBe("Could not load the report (500).");
  });
});
