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

//nav links
const navLinks = [
  { href: "index.html",         text: "Home" },
  { href: "Notifications.html", text: "Notifications", badge: true }
];

const currentPage = location.pathname.split("/").pop() || "index.html";

//nav bar
document.getElementById("nav-placeholder").innerHTML = `
  <nav class="navbar">
    <a class="brand" href="index.html"><img src="brain-logo-transparent.png" alt="" /> Walk-In Psychiatric Clinic</a>
    <ul>
      ${navLinks.map(link => `
        <li><a href="${link.href}" class="${link.href === currentPage ? "active" : ""}">
          ${link.text}${link.badge ? '<span id="nav-notif-badge" class="nav-badge" hidden></span>' : ""}
        </a></li>
      `).join("")}
    </ul>
  </nav>`;

updateNavBadge();