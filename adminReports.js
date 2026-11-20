// Reporting module (Final Project) - admin-reports.html
// Loads the three admin reports from /api/reports/* and lets the admin export them as CSV
// or print/save the page as a PDF. The pure helper functions at the top are exported for Jest.

const REPORT_TYPES = ["stats", "services", "users"];

// ---------- helpers (tested in adminReports.test.js) ----------

// the logged-in admin from localStorage "currentUser" (null if not logged in / not an admin)
function currentAdmin(storage) {
  try {
    const user = JSON.parse(storage.getItem("currentUser"));
    if (!user || user.userId == null) return null;
    return String(user.role || "").toLowerCase() === "admin" ? user : null;
  } catch (e) {
    return null;
  }
}

// "?from=...&to=...&service=...&format=csv" - only the filters that are set
function reportQuery(filters, format) {
  const params = new URLSearchParams();
  const f = filters || {};
  if (f.from) params.set("from", f.from);
  if (f.to) params.set("to", f.to);
  if (f.service) params.set("service", f.service);
  if (format && format !== "json") params.set("format", format);
  const text = params.toString();
  return text ? "?" + text : "";
}

// checks the filters before calling the API (the backend checks them again)
function validateFilters(filters) {
  const f = filters || {};
  if (f.from && f.to && f.from > f.to) return "The To date must be on or after the From date.";
  return "";
}

function formatMinutes(minutes) {
  if (minutes === null || minutes === undefined) return "—";
  const total = Math.round(minutes);
  if (total < 60) return total + " min";
  const h = Math.floor(total / 60);
  const m = total % 60;
  return m === 0 ? `${h} hr` : `${h} hr ${m} min`;
}

function formatHour(hour) {
  if (hour === null || hour === undefined) return "—";
  const suffix = hour < 12 ? "AM" : "PM";
  const h = hour % 12 === 0 ? 12 : hour % 12;
  return `${h}:00 ${suffix}`;
}

// "2026-11-02T09:00:00" -> "Nov 2, 9:00 AM" (times already come back in clinic time)
function formatDateTime(value) {
  if (!value) return "—";
  const [datePart, timePart = "00:00"] = String(value).split("T");
  const [y, mo, d] = datePart.split("-").map(Number);
  const [hh, mm] = timePart.split(":").map(Number);
  const months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
  if (!y || !mo || !d) return String(value);
  const suffix = hh < 12 ? "AM" : "PM";
  const h12 = hh % 12 === 0 ? 12 : hh % 12;
  return `${months[mo - 1]} ${d}, ${y} ${h12}:${String(mm).padStart(2, "0")} ${suffix}`;
}

// one line that says what the report covers, e.g. "Dr. Nate · Nov 1, 2026 – Nov 30, 2026"
function describeFilters(filters) {
  const f = filters || {};
  const short = v => formatDateTime(v + "T00:00").replace(/ 12:00 AM$/, "");
  let range = "All dates";
  if (f.from && f.to) range = `${short(f.from)} – ${short(f.to)}`;
  else if (f.from) range = `From ${short(f.from)}`;
  else if (f.to) range = `Up to ${short(f.to)}`;
  return `${f.service || "All services"} · ${range}`;
}

function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>"']/g, c =>
    ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}

// turns the API's error body into one message
function errorMessage(status, body) {
  if (body && body.errors) return Object.values(body.errors).join(" ");
  if (body && body.error) return body.error;
  if (status === 401 || status === 403) return "Only administrators can view reports. Please log in as an admin.";
  return "Could not load the report (" + status + ").";
}

// ---------- page (browser only) ----------

function setupReportsPage() {
  const admin = currentAdmin(localStorage);
  if (!admin) {
    window.location.replace("login.html");
    return;
  }

  const form = document.getElementById("reportFilters");
  const message = document.getElementById("reportMessage");
  const serviceSelect = document.getElementById("reportService");
  const headers = { "X-User-Id": String(admin.userId) };

  function filters() {
    return {
      from: document.getElementById("reportFrom").value,
      to: document.getElementById("reportTo").value,
      service: serviceSelect.value
    };
  }

  async function getJson(type, f) {
    const res = await fetch(`/api/reports/${type}${reportQuery(f, "json")}`, { headers });
    const body = await res.json().catch(() => null);
    if (!res.ok) throw new Error(errorMessage(res.status, body));
    return body;
  }

  async function loadServiceOptions() {
    try {
      const res = await fetch("/api/services");
      if (!res.ok) return;
      (await res.json()).forEach(s => {
        const option = document.createElement("option");
        option.value = s.name;
        option.textContent = s.name;
        serviceSelect.appendChild(option);
      });
    } catch (e) { /* the "All services" option still works */ }
  }

  function renderStats(s) {
    const cards = [
      ["Total joined", s.totalJoined],
      ["Served", s.served],
      ["Canceled", s.canceled],
      ["Still waiting", s.stillWaiting],
      ["Average wait", formatMinutes(s.avgWaitMinutes)],
      ["Longest wait", formatMinutes(s.longestWaitMinutes)],
      ["Busiest service", s.busiestService || "—"],
      ["Busiest hour", formatHour(s.busiestHour)],
      ["Served rate", s.servedRatePercent + "%"]
    ];
    document.getElementById("statCards").innerHTML = cards.map(([label, value]) =>
      `<div class="stat-card"><span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong></div>`).join("");
  }

  function renderServices(rows) {
    const body = document.querySelector("#servicesReport tbody");
    body.innerHTML = rows.length === 0
      ? '<tr><td colspan="9" class="empty-row">No services match these filters.</td></tr>'
      : rows.map(s => `
        <tr>
          <td class="name">${escapeHtml(s.name)}</td>
          <td>${escapeHtml(s.durationMinutes)} min</td>
          <td>${escapeHtml(s.priority)}</td>
          <td><span class="status ${s.queueStatus === "Open" ? "open" : "closed"}">${escapeHtml(s.queueStatus)}</span></td>
          <td>${s.currentlyWaiting}</td>
          <td>${s.totalJoined}</td>
          <td>${s.served}</td>
          <td>${s.canceled}</td>
          <td>${formatMinutes(s.avgWaitMinutes)}</td>
        </tr>`).join("");
  }

  function renderUsers(rows) {
    const body = document.querySelector("#usersReport tbody");
    if (rows.length === 0) {
      body.innerHTML = '<tr><td colspan="7" class="empty-row">No patients joined a queue in this range.</td></tr>';
      return;
    }
    body.innerHTML = rows.map((u, i) => `
      <tr class="user-row" data-index="${i}" tabindex="0" aria-expanded="false">
        <td class="name">${escapeHtml(u.name)}</td>
        <td>${escapeHtml(u.email)}</td>
        <td>${u.totalVisits}</td>
        <td>${u.served}</td>
        <td>${u.canceled}</td>
        <td>${formatMinutes(u.avgWaitMinutes)}</td>
        <td>${u.visits.length ? '<span class="toggle-hint">Show visits ▾</span>' : "—"}</td>
      </tr>
      ${u.visits.length === 0 ? "" : `<tr class="visit-row" data-for="${i}" hidden>
        <td colspan="7">
          <table class="visit-table">
            <thead><tr><th>Service</th><th>Joined</th><th>Ended</th><th>Outcome</th><th>Wait</th></tr></thead>
            <tbody>${u.visits.map(v => `
              <tr>
                <td>${escapeHtml(v.service)}</td>
                <td>${formatDateTime(v.joinedAt)}</td>
                <td>${formatDateTime(v.endedAt)}</td>
                <td>${escapeHtml(v.outcome)}</td>
                <td>${formatMinutes(v.waitMinutes)}</td>
              </tr>`).join("")}
            </tbody>
          </table>
        </td>
      </tr>`}`).join("");
  }

  async function generate() {
    const f = filters();
    const problem = validateFilters(f);
    if (problem) {
      message.textContent = problem;
      message.className = "error";
      return;
    }
    message.className = "message";
    message.textContent = "Loading…";
    try {
      const [stats, services, users] = await Promise.all(REPORT_TYPES.map(t => getJson(t, f)));
      renderStats(stats);
      renderServices(services);
      renderUsers(users);
      document.getElementById("reportScope").textContent = describeFilters(f);
      document.getElementById("reportGenerated").textContent = "Generated " + new Date().toLocaleString();
      message.textContent = "";
    } catch (e) {
      message.className = "error";
      message.textContent = e.message === "Failed to fetch" ? "Can't reach the server. Is dotnet run still going?" : e.message;
    }
  }

  // CSV: fetch with the admin header, then save the file the server sends back
  async function exportCsv(type) {
    const f = filters();
    try {
      const res = await fetch(`/api/reports/${type}${reportQuery(f, "csv")}`, { headers });
      if (!res.ok) {
        const body = await res.json().catch(() => null);
        throw new Error(errorMessage(res.status, body));
      }
      const blob = await res.blob();
      const match = /filename="?([^";]+)"?/.exec(res.headers.get("Content-Disposition") || "");
      const link = document.createElement("a");
      link.href = URL.createObjectURL(blob);
      link.download = match ? match[1] : `${type}-report.csv`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(link.href);
      message.className = "message";
      message.textContent = "Downloaded " + link.download;
    } catch (e) {
      message.className = "error";
      message.textContent = e.message;
    }
  }

  form.addEventListener("submit", e => { e.preventDefault(); generate(); });
  document.getElementById("clearFilters").addEventListener("click", () => { form.reset(); generate(); });
  document.querySelectorAll("[data-export]").forEach(btn =>
    btn.addEventListener("click", () => exportCsv(btn.dataset.export)));
  document.getElementById("printReport").addEventListener("click", () => window.print());

  // click (or Enter) on a patient to show their visits
  document.querySelector("#usersReport tbody").addEventListener("click", toggleVisits);
  document.querySelector("#usersReport tbody").addEventListener("keydown", e => { if (e.key === "Enter") toggleVisits(e); });
  function toggleVisits(e) {
    const row = e.target.closest(".user-row");
    if (!row) return;
    const details = row.parentElement.querySelector(`.visit-row[data-for="${row.dataset.index}"]`);
    if (!details) return;
    details.hidden = !details.hidden;
    row.setAttribute("aria-expanded", String(!details.hidden));
    const hint = row.querySelector(".toggle-hint");
    if (hint) hint.textContent = details.hidden ? "Show visits ▾" : "Hide visits ▴";
  }

  loadServiceOptions();
  generate();
}

if (typeof document !== "undefined" && document.getElementById("reportFilters")) {
  document.addEventListener("DOMContentLoaded", setupReportsPage);
}

// ---------- export for Jest (ignored in the browser) ----------
if (typeof module !== "undefined" && module.exports) {
  module.exports = {
    currentAdmin, reportQuery, validateFilters, formatMinutes, formatHour,
    formatDateTime, describeFilters, escapeHtml, errorMessage
  };
}
