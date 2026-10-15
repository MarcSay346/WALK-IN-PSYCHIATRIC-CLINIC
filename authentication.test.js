const auth = require("./authentication");

// simple fake localStorage so tests don't need a browser
function fakeStorage() {
  const data = {};
  return {
    getItem: k => (k in data ? data[k] : null),
    setItem: (k, v) => { data[k] = String(v); },
    removeItem: k => { delete data[k]; }
  };
}

let store;
beforeEach(() => { store = fakeStorage(); });

// ---------- validation rules ----------
describe("isValidEmail", () => {
  test("accepts a normal email", () => expect(auth.isValidEmail("a@b.com")).toBe(true));
  test("rejects missing @", () => expect(auth.isValidEmail("ab.com")).toBe(false));
  test("rejects missing domain dot", () => expect(auth.isValidEmail("a@b")).toBe(false));
  test("rejects non-string", () => expect(auth.isValidEmail(null)).toBe(false));
});

describe("isValidPassword", () => {
  test("accepts 6+ characters", () => expect(auth.isValidPassword("abc123")).toBe(true));
  test("rejects fewer than 6", () => expect(auth.isValidPassword("abc12")).toBe(false));
  test("rejects only spaces", () => expect(auth.isValidPassword("        ")).toBe(false));
  test("rejects non-string", () => expect(auth.isValidPassword(undefined)).toBe(false));
});

describe("validateLogin", () => {
  test("valid input passes", () => {
    expect(auth.validateLogin("a@b.com", "secret1").valid).toBe(true);
  });
  test("empty fields fail", () => {
    const r = auth.validateLogin("", "");
    expect(r.valid).toBe(false);
    expect(r.errors.email).toBe("Email is required.");
    expect(r.errors.password).toBe("Password is required.");
  });
  test("bad email format fails", () => {
    expect(auth.validateLogin("notanemail", "secret1").errors.email).toBe("Please enter a valid email address.");
  });
  test("short password fails", () => {
    expect(auth.validateLogin("a@b.com", "123").errors.password).toMatch(/at least 6/);
  });
});

describe("validateRegistration", () => {
  const good = { email: "m@test.com", password: "secret1", confirmPassword: "secret1" };

  test("valid input passes", () => expect(auth.validateRegistration(good).valid).toBe(true));
  test("missing everything fails", () => {
    const r = auth.validateRegistration();
    expect(r.errors.email).toBeDefined();
    expect(r.errors.password).toBeDefined();
    expect(r.errors.confirmPassword).toBeDefined();
  });
  test("bad email fails", () => {
    expect(auth.validateRegistration({ ...good, email: "bad" }).errors.email).toMatch(/valid email/);
  });
  test("short password fails", () => {
    expect(auth.validateRegistration({ ...good, password: "abc", confirmPassword: "abc" }).errors.password).toMatch(/at least 6/);
  });
  test("mismatched passwords fail", () => {
    expect(auth.validateRegistration({ ...good, confirmPassword: "other12" }).errors.confirmPassword).toBe("Passwords do not match.");
  });
  test("empty confirm password fails", () => {
    expect(auth.validateRegistration({ ...good, confirmPassword: "" }).errors.confirmPassword).toBeDefined();
  });
});

// ---------- core business logic ----------
describe("getUsers", () => {
  test("returns dummy users when storage is empty", () => {
    expect(auth.getUsers(store)).toHaveLength(auth.DUMMY_USERS.length);
  });
  test("falls back to dummy users if saved data is broken", () => {
    store.setItem(auth.USERS_KEY, "{not json");
    expect(auth.getUsers(store)).toHaveLength(auth.DUMMY_USERS.length);
  });
});

describe("registerUser", () => {
  const data = { email: "New@Test.com", password: "hello12", confirmPassword: "hello12" };

  test("creates a new user and saves it", () => {
    const r = auth.registerUser(data, store);
    expect(r.ok).toBe(true);
    expect(r.user.email).toBe("new@test.com");
    expect(r.user.name).toBe("new");
    expect(r.user.password).toBeUndefined();
    expect(auth.getUsers(store)).toHaveLength(auth.DUMMY_USERS.length + 1);
  });
  test("rejects a duplicate email", () => {
    auth.registerUser(data, store);
    const r = auth.registerUser(data, store);
    expect(r.ok).toBe(false);
    expect(r.errors.email).toMatch(/already exists/);
  });
  test("rejects a dummy user's email", () => {
    expect(auth.registerUser({ ...data, email: "patient@test.com" }, store).ok).toBe(false);
  });
  test("rejects invalid data", () => {
    expect(auth.registerUser({ ...data, email: "bad" }, store).ok).toBe(false);
  });
});

describe("loginUser / logoutUser / getCurrentUser", () => {
  test("logs in a dummy user", () => {
    const r = auth.loginUser("patient@test.com", "password1", store);
    expect(r.ok).toBe(true);
    expect(r.user.name).toBe("Test Patient");
    expect(auth.getCurrentUser(store).email).toBe("patient@test.com");
  });
  test("email is case-insensitive", () => {
    expect(auth.loginUser("PATIENT@test.com", "password1", store).ok).toBe(true);
  });
  test("wrong password fails", () => {
    const r = auth.loginUser("patient@test.com", "wrongpass", store);
    expect(r.ok).toBe(false);
    expect(r.errors.form).toBe("Incorrect email or password.");
    expect(auth.getCurrentUser(store)).toBeNull();
  });
  test("unknown email fails", () => {
    expect(auth.loginUser("nobody@test.com", "password1", store).ok).toBe(false);
  });
  test("empty fields fail validation", () => {
    expect(auth.loginUser("", "", store).errors.email).toBeDefined();
  });
  test("newly registered user can log in", () => {
    auth.registerUser({ email: "sam@test.com", password: "pass123", confirmPassword: "pass123" }, store);
    expect(auth.loginUser("sam@test.com", "pass123", store).ok).toBe(true);
  });
  test("logout clears the current user", () => {
    auth.loginUser("patient@test.com", "password1", store);
    auth.logoutUser(store);
    expect(auth.getCurrentUser(store)).toBeNull();
  });
  test("getCurrentUser handles broken data", () => {
    store.setItem(auth.USER_KEY, "{oops");
    expect(auth.getCurrentUser(store)).toBeNull();
  });
});

describe("storage fallback", () => {
  test("throws if no storage exists", () => {
    expect(() => auth.logoutUser()).toThrow("No storage available");
  });
});