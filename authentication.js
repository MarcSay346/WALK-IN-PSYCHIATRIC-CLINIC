// auth.js — login / registration logic using dummy data + localStorage
// Works in the browser (<script src="auth.js">) and in Jest (require("./auth")).

const USERS_KEY = "clinicUsers";   // list of registered users
const USER_KEY = "currentUser";    // who is logged in right now (navbar.js reads this)
const MIN_PASSWORD_LENGTH = 6;

// dummy accounts that always exist
const DUMMY_USERS = [
  { name: "Test Patient", email: "patient@test.com", password: "password1", role: "patient" },
  { name: "Clinic Admin", email: "admin@test.com",   password: "admin123",  role: "admin" }
];

// ---------- storage helper ----------
// Uses the browser's localStorage by default; tests pass in a fake one.
function getStorage(storage) {
  if (storage) return storage;
  if (typeof localStorage !== "undefined") return localStorage;
  throw new Error("No storage available");
}

// ---------- validation rules ----------
function isValidEmail(email) {
  return typeof email === "string" && /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim());
}

function isValidPassword(password) {
  return typeof password === "string" && password.trim().length >= MIN_PASSWORD_LENGTH;
}

function validateLogin(email, password) {
  const errors = {};
  if (!email || !email.trim())        errors.email = "Email is required.";
  else if (!isValidEmail(email))      errors.email = "Please enter a valid email address.";
  if (!password)                      errors.password = "Password is required.";
  else if (!isValidPassword(password)) errors.password = "Password must be at least 6 characters long.";
  return { valid: Object.keys(errors).length === 0, errors };
}

function validateRegistration({ email, password, confirmPassword } = {}) {
  const errors = {};
  if (!email || !email.trim())          errors.email = "Email is required.";
  else if (!isValidEmail(email))        errors.email = "Please enter a valid email address.";

  if (!password)                        errors.password = "Password is required.";
  else if (!isValidPassword(password))  errors.password = "Password must be at least 6 characters.";

  if (!confirmPassword || password !== confirmPassword) errors.confirmPassword = "Passwords do not match.";

  return { valid: Object.keys(errors).length === 0, errors };
}

// ---------- user data ----------
function getUsers(storage) {
  const store = getStorage(storage);
  try {
    const saved = store.getItem(USERS_KEY);
    if (saved) return JSON.parse(saved);
  } catch (e) {}
  return DUMMY_USERS.map(u => ({ ...u }));
}

function saveUsers(users, storage) {
  getStorage(storage).setItem(USERS_KEY, JSON.stringify(users));
}

function findUserByEmail(users, email) {
  const target = email.trim().toLowerCase();
  return users.find(u => u.email.toLowerCase() === target) || null;
}

// ---------- core business logic ----------
function registerUser(data, storage) {
  const check = validateRegistration(data);
  if (!check.valid) return { ok: false, errors: check.errors };

  const users = getUsers(storage);
  if (findUserByEmail(users, data.email)) {
    return { ok: false, errors: { email: "An account with this email already exists." } };
  }

  const email = data.email.trim().toLowerCase();
  const user = { name: email.split("@")[0], email, password: data.password, role: "patient" };
  users.push(user);
  saveUsers(users, storage);
  return { ok: true, user: publicUser(user) };
}

function loginUser(email, password, storage) {
  const check = validateLogin(email, password);
  if (!check.valid) return { ok: false, errors: check.errors };

  const user = findUserByEmail(getUsers(storage), email);
  if (!user || user.password !== password) {
    return { ok: false, errors: { form: "Incorrect email or password." } };
  }

  const current = publicUser(user);
  getStorage(storage).setItem(USER_KEY, JSON.stringify(current));
  return { ok: true, user: current };
}

function logoutUser(storage) {
  getStorage(storage).removeItem(USER_KEY);
}

function getCurrentUser(storage) {
  try {
    const saved = getStorage(storage).getItem(USER_KEY);
    return saved ? JSON.parse(saved) : null;
  } catch (e) {
    return null;
  }
}

// never keep the password in "currentUser"
function publicUser(user) {
  return { name: user.name, email: user.email, role: user.role };
}

// ---------- export for Jest (ignored in the browser) ----------
if (typeof module !== "undefined" && module.exports) {
  module.exports = {
    USERS_KEY, USER_KEY, DUMMY_USERS, MIN_PASSWORD_LENGTH,
    isValidEmail, isValidPassword, validateLogin, validateRegistration,
    getUsers, saveUsers, findUserByEmail,
    registerUser, loginUser, logoutUser, getCurrentUser
  };
}