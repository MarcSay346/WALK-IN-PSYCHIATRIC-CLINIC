// history.js — History module front end.
// Fills the History table on joinQueue.html (<tbody id="history-tbody">) from the backend:
//   GET /api/history/{userId}
// Load it at the bottom of joinQueue.html, after navbar.js:
//   <script src="history.js"></script>
(function () {
  const OUTCOMES = {
    Waiting: { label: "In queue", style: "color: var(--blue-dark); font-weight: 600;" },
    Served:  { label: "Completed", style: "color: #2E6B3A; font-weight: 600;" },
    Left:    { label: "Left queue", style: "color: var(--muted);" }
  };

  function escapeHtml(text) {
    return String(text).replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
  }

  function messageRow(text) {
    return `<tr><td colspan="3" style="text-align: center; color: var(--muted);">${text}</td></tr>`;
  }

  function formatDate(iso) {
    return new Date(iso).toLocaleDateString(undefined, { month: "short", day: "numeric", year: "numeric" });
  }

  async function loadHistory() {
    const tbody = document.getElementById("history-tbody");
    if (!tbody) return;

    const userId = typeof getCurrentUserId === "function" ? getCurrentUserId() : null;   // from navbar.js
    if (!userId) {
      tbody.innerHTML = messageRow("Log in to see your visit history.");
      return;
    }

    tbody.innerHTML = messageRow("Loading…");
    try {
      const res = await fetch(`/api/history/${encodeURIComponent(userId)}`);
      if (!res.ok) throw new Error(`Request failed (${res.status})`);
      const entries = await res.json();

      if (entries.length === 0) {
        tbody.innerHTML = messageRow("No visits yet. Join a queue to start your history.");
        return;
      }

      tbody.innerHTML = entries.map(entry => {
        const outcome = OUTCOMES[entry.outcome] || { label: entry.outcome, style: "" };
        return `
          <tr>
            <td>${formatDate(entry.joinedAt)}</td>
            <td>${escapeHtml(entry.serviceName)}</td>
            <td><span style="${outcome.style}">${escapeHtml(outcome.label)}</span></td>
          </tr>`;
      }).join("");
    } catch (e) {
      tbody.innerHTML = messageRow("Could not load history. Make sure the backend is running (dotnet run).");
    }
  }

  // other scripts (e.g. the join-queue form) can call loadHistory() to refresh the table
  window.loadHistory = loadHistory;
  window.addEventListener("hashchange", () => {
    if (location.hash === "#history") loadHistory();
  });
  loadHistory();
})();
