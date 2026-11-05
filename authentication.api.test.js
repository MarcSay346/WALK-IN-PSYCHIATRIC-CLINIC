// A4: tests for apiLogin / apiRegister (the login and register pages now call the backend).
const auth = require("./authentication");

function fakeStorage() {
  const data = {};
  return {
    getItem: k => (k in data ? data[k] : null),
    setItem: (k, v) => { data[k] = String(v); },
    removeItem: k => { delete data[k]; }
  };
}

// fake fetch that records the request and answers with the given status/body
function fakeFetch(status, body) {
  const fn = jest.fn(async () => ({ ok: status >= 200 && status < 300, status, json: async () => body }));
  return fn;
}

const apiUser = { userId: 7, patientId: 7, name: "Ana Lopez", email: "ana@test.com", role: "User" };

let store;
beforeEach(() => { store = fakeStorage(); });

describe("apiLogin", () => {
  test("posts to /api/login and saves the user (without password)", async () => {
    const fetchFn = fakeFetch(200, apiUser);
    const result = await auth.apiLogin(" ana@test.com ", "secret1", fetchFn, store);

    expect(result.ok).toBe(true);
    expect(fetchFn).toHaveBeenCalledWith("/api/login", expect.objectContaining({ method: "POST" }));
    expect(JSON.parse(fetchFn.mock.calls[0][1].body)).toEqual({ email: "ana@test.com", password: "secret1" });

    const saved = JSON.parse(store.getItem(auth.USER_KEY));
    expect(saved).toEqual({ userId: 7, patientId: 7, name: "Ana Lopez", email: "ana@test.com", role: "user" });
    expect(saved.password).toBeUndefined();
  });

  test("admin role comes back lower-case so the page can redirect", async () => {
    const result = await auth.apiLogin("a@test.com", "admin123", fakeFetch(200, { ...apiUser, role: "Admin" }), store);
    expect(result.user.role).toBe("admin");
  });

  test("401 shows a wrong email/password message", async () => {
    const result = await auth.apiLogin("ana@test.com", "wrong12", fakeFetch(401, { error: "Incorrect email or password." }), store);
    expect(result.ok).toBe(false);
    expect(result.errors.form).toBe("Incorrect email or password.");
    expect(store.getItem(auth.USER_KEY)).toBeNull();
  });

  test("invalid input never calls the server", async () => {
    const fetchFn = fakeFetch(200, apiUser);
    const result = await auth.apiLogin("bad", "", fetchFn, store);
    expect(result.ok).toBe(false);
    expect(fetchFn).not.toHaveBeenCalled();
  });

  test("server down gives a friendly message", async () => {
    const fetchFn = jest.fn(async () => { throw new Error("offline"); });
    const result = await auth.apiLogin("ana@test.com", "secret1", fetchFn, store);
    expect(result.errors.form).toMatch(/Can't reach the server/);
  });
});

describe("apiRegister", () => {
  const form = { email: "ana@test.com", password: "secret1", confirmPassword: "secret1" };

  test("posts to /api/register", async () => {
    const fetchFn = fakeFetch(200, apiUser);
    const result = await auth.apiRegister(form, fetchFn, store);
    expect(result.ok).toBe(true);
    expect(fetchFn.mock.calls[0][0]).toBe("/api/register");
    expect(result.user.userId).toBe(7);
  });

  test("409 shows the email is taken", async () => {
    const result = await auth.apiRegister(form, fakeFetch(409, { error: "An account with that email already exists." }), store);
    expect(result.errors.email).toBe("An account with that email already exists.");
  });

  test("400 passes the backend's field errors through", async () => {
    const result = await auth.apiRegister(form, fakeFetch(400, { errors: { password: "Too short." } }), store);
    expect(result.errors).toEqual({ password: "Too short." });
  });

  test("other errors fall back to a generic message", async () => {
    const result = await auth.apiRegister(form, fakeFetch(500, null), store);
    expect(result.errors.form).toBe("Request failed (500).");
  });

  test("mismatched passwords never call the server", async () => {
    const fetchFn = fakeFetch(200, apiUser);
    const result = await auth.apiRegister({ ...form, confirmPassword: "other1" }, fetchFn, store);
    expect(result.errors.confirmPassword).toBeDefined();
    expect(fetchFn).not.toHaveBeenCalled();
  });

  test("server down gives a friendly message", async () => {
    const result = await auth.apiRegister(form, jest.fn(async () => { throw new Error("offline"); }), store);
    expect(result.errors.form).toMatch(/Can't reach the server/);
  });
});
