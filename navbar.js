const navLinks = [
  { href: "index.html",         text: "Home" },
  { href: "Notifications.html", text: "Notifications" }
];

const currentPage = location.pathname.split("/").pop() || "index.html";

document.getElementById("nav-placeholder").innerHTML = `
  <nav class="navbar">
    <a class="brand" href="index.html">Walk-In Psychiatric Clinic</a>
    <ul>
      ${navLinks.map(link => `
        <li><a href="${link.href}" class="${link.href === currentPage ? "active" : ""}">${link.text}</a></li>
      `).join("")}
    </ul>
  </nav>`;
