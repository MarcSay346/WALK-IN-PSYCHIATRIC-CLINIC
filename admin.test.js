const admin = require("./adminLogic");

function sampleService(changes) {
  return Object.assign({
    name: "Dr. Nate",
    description: "General counseling visit",
    duration: "60",
    priority: "High"
  }, changes);
}

describe("admin service validation", () => {
  test("accepts a complete service", () => {
    expect(admin.validateService(sampleService()).valid).toBe(true);
  });

  test("removes extra spaces from the fields", () => {
    const result = admin.validateService(sampleService({ name: "  Dr. Nate  " }));
    expect(result.values.name).toBe("Dr. Nate");
  });

  test("requires every service field", () => {
    const result = admin.validateService(sampleService({ description: "" }));
    expect(result.valid).toBe(false);
    expect(result.error).toBe("Please complete all required fields.");
  });

  test("allows a service name with 100 characters", () => {
    expect(admin.validateService(sampleService({ name: "a".repeat(100) })).valid).toBe(true);
  });

  test("rejects a service name over 100 characters", () => {
    const result = admin.validateService(sampleService({ name: "a".repeat(101) }));
    expect(result.valid).toBe(false);
    expect(result.error).toMatch(/100 characters/);
  });

  test("rejects a duration of zero", () => {
    expect(admin.validateService(sampleService({ duration: "0" })).valid).toBe(false);
  });

  test("rejects a decimal duration", () => {
    expect(admin.validateService(sampleService({ duration: "15.5" })).valid).toBe(false);
  });

  test("rejects an unknown priority", () => {
    const result = admin.validateService(sampleService({ priority: "Urgent" }));
    expect(result.valid).toBe(false);
    expect(result.error).toMatch(/Low, Medium, or High/);
  });
});

describe("admin dashboard queue status", () => {
  test("closes an open queue", () => {
    expect(admin.toggleQueue(true)).toBe(false);
  });

  test("opens a closed queue", () => {
    expect(admin.toggleQueue(false)).toBe(true);
  });
});

describe("admin queue management", () => {
  let queue;

  beforeEach(() => {
    queue = [
      { name: "Taylor M." },
      { name: "Jordan R." },
      { name: "Casey L." }
    ];
  });

  test("moves a user up one position", () => {
    expect(admin.moveUser(queue, 1, -1)).toBe(true);
    expect(queue.map(user => user.name)).toEqual(["Jordan R.", "Taylor M.", "Casey L."]);
  });

  test("moves a user down one position", () => {
    expect(admin.moveUser(queue, 0, 1)).toBe(true);
    expect(queue.map(user => user.name)).toEqual(["Jordan R.", "Taylor M.", "Casey L."]);
  });

  test("does not move the first user farther up", () => {
    expect(admin.moveUser(queue, 0, -1)).toBe(false);
    expect(queue[0].name).toBe("Taylor M.");
  });

  test("does not move the last user farther down", () => {
    expect(admin.moveUser(queue, 2, 1)).toBe(false);
    expect(queue[2].name).toBe("Casey L.");
  });

  test("rejects an invalid move direction", () => {
    expect(admin.moveUser(queue, 1, 3)).toBe(false);
  });

  test("removes the selected user", () => {
    const removed = admin.removeUser(queue, 1);
    expect(removed.name).toBe("Jordan R.");
    expect(queue).toHaveLength(2);
  });

  test("does not remove a user at an invalid position", () => {
    expect(admin.removeUser(queue, 10)).toBeNull();
    expect(queue).toHaveLength(3);
  });

  test("serves the first user in line", () => {
    const served = admin.serveNextUser(queue);
    expect(served.name).toBe("Taylor M.");
    expect(queue[0].name).toBe("Jordan R.");
  });

  test("handles serving an empty queue", () => {
    expect(admin.serveNextUser([])).toBeNull();
  });
});
