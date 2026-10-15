//notification data
const NOTIF_KEY = "clinicNotifications";

const defaultNotifications = [
  { id: 1, type: "queue",  title: "You're next in line", text: "A provider will be ready for you in about 5 minutes.", time: "2 min ago", read: false },
  { id: 2, type: "status", title: "Status changed: Ready to be seen", text: "Please head to the front desk when your name is called.", time: "3 min ago", read: false },
  { id: 3, type: "queue",  title: "You moved up to #3", text: "Estimated wait is now 15 minutes.", time: "12 min ago", read: false },
  { id: 4, type: "status", title: "Status changed: Checked in", text: "You're in the queue. Estimated wait is 25 minutes.", time: "20 min ago", read: true },
  { id: 5, type: "status", title: "Status changed: Visit completed", text: "Your last visit was marked complete.", time: "Last week", read: true }
];

//load notifications
function loadNotifications() {
  try {
    const saved = localStorage.getItem(NOTIF_KEY);
    if (saved) return JSON.parse(saved);
  } catch (e) {}
  return defaultNotifications.map(n => ({ ...n }));
}

//save notifications
function saveNotifications(list) {
  try { localStorage.setItem(NOTIF_KEY, JSON.stringify(list)); } catch (e) {}
}

//count unread
function unreadCount(list) {
  return list.filter(n => !n.read).length;
}

//nav badge
function updateNavBadge() {
  const badge = document.getElementById("nav-notif-badge");
  if (!badge) return;
  const count = unreadCount(loadNotifications());
  badge.textContent = count;
  badge.hidden = count === 0;
}

//login state
// auth.js saves the signed-in user under this key.
// (Named differently from auth.js's USER_KEY so both files can load on one page.)
const NAV_USER_KEY = "currentUser";

function isLoggedIn() {
  try { return localStorage.getItem(NAV_USER_KEY) !== null; } catch (e) { return false; }
}

function logout() {
  try { localStorage.removeItem(NAV_USER_KEY); } catch (e) {}
  window.location.href = "login.html";
}

//nav links
// show: "in"  = only when logged in
// show: "out" = only when logged out
// no show     = always
const navLinks = [
  { href: "index.html",         text: "Home" },
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
      <a class="brand" href="index.html">
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