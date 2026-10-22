//login state
// Works with either login style: localStorage "currentUser" or sessionStorage "patient"
// (the C# /api/login response). Named "nav..." so they don't clash with other scripts.
const NAV_USER_KEY = "currentUser";

function navReadStored(storage, key) {
  try {
    const raw = storage.getItem(key);
    return raw ? JSON.parse(raw) : null;
  } catch (e) {
    return null;
  }
}

function navGetUser() {
  return navReadStored(localStorage, NAV_USER_KEY) || navReadStored(sessionStorage, "patient");
}

// the id the backend uses for this user (patientId, id, userId or email)
function getCurrentUserId() {
  const user = navGetUser();
  if (!user) return null;
  const id = user.patientId ?? user.id ?? user.userId ?? user.email;
  return id == null ? null : String(id);
}

function isLoggedIn() {
  return navGetUser() !== null;
}

function logout() {
  try {
    localStorage.removeItem(NAV_USER_KEY);
    sessionStorage.removeItem("patient");
  } catch (e) {}
  window.location.href = "login.html";
}

//nav badge (unread count comes from the backend)
async function updateNavBadge() {
  const badge = document.getElementById("nav-notif-badge");
  const userId = getCurrentUserId();
  if (!badge || !userId) return;
  try {
    const res = await fetch(`/api/notifications/${encodeURIComponent(userId)}/unread-count`);
    if (!res.ok) return;
    const { count } = await res.json();
    badge.textContent = count;
    badge.hidden = count === 0;
  } catch (e) {
    badge.hidden = true;   // backend not running
  }
}

//nav links
// show: "in"  = only when logged in
// show: "out" = only when logged out
// no show     = always
// Dashboard, Join Queue, Status and History are all sections of joinQueue.html;
// the part after # picks which section opens.
const navLinks = [
  { href: "index.html",               text: "Home",       show: "out" },
  { href: "joinQueue.html#dashboard", text: "Dashboard",  show: "in" },
  { href: "joinQueue.html#join",      text: "Join Queue", show: "in" },
  { href: "joinQueue.html#status",    text: "Status",     show: "in" },
  { href: "joinQueue.html#history",   text: "History",    show: "in" },
  { href: "Notifications.html", text: "Notifications", badge: true, show: "in" },
  { href: "login.html",         text: "Login",    button: "login",    show: "out" },
  { href: "register.html",      text: "Register", button: "register", show: "out" },
  { href: "#",                  text: "Logout",   button: "logout",   show: "in", id: "logout-btn" }
];

const currentPage = location.pathname.split("/").pop() || "index.html";
const loggedIn = isLoggedIn();

//pages that need a login (lowercase); logged-out visitors get sent to login
const protectedPages = ["notifications.html", "joinqueue.html"];
if (!loggedIn && protectedPages.includes(currentPage.toLowerCase())) {
  window.location.replace("login.html");
}

//nav bar
document.getElementById("nav-placeholder").innerHTML = `
  <nav class="navbar">
    <div class="nav-inner">
      <a class="brand" href="${loggedIn ? "joinQueue.html#dashboard" : "index.html"}">
        <img src="brain-logo.png" alt="" />
        <span class="brand-name">Radiant Mind</span>
        <span class="brand-sub">Walk-In Psychiatric Clinic</span>
      </a>
      <ul>
        ${navLinks
          .filter(link => !link.show || (link.show === "in") === loggedIn)
          .map(link => {
            const classes = [];
            if (link.button) classes.push("nav-btn", link.button);
            if (link.href === currentPage) classes.push("active");
            const badge = link.badge ? '<span id="nav-notif-badge" class="nav-badge" hidden></span>' : "";
            const id = link.id ? ` id="${link.id}"` : "";
            return `<li><a href="${link.href}"${id} class="${classes.join(" ")}">${link.text}${badge}</a></li>`;
          }).join("")}
      </ul>
    </div>
  </nav>`;

//logout click
const logoutBtn = document.getElementById("logout-btn");
if (logoutBtn) {
  logoutBtn.addEventListener("click", e => {
    e.preventDefault();
    logout();
  });
}

updateNavBadge();
