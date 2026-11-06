document.addEventListener("DOMContentLoaded", function () {
  setupDashboard();
  setupServices();
  setupQueue();
});

// A4: queue Open / Closed status is saved in the Queue table
// (GET/POST /api/queue/state?serviceName=...&open=true|false).
function setupDashboard() {
  const table = document.getElementById("dashboardServiceTable");
  if (!table) return;
  const message = document.getElementById("dashboardMessage");

  function showStatus(row, isOpen) {
    const status = row.querySelector(".status");
    const button = row.querySelector(".toggle-queue");
    row.dataset.open = String(isOpen);
    status.textContent = isOpen ? "Open" : "Closed";
    status.className = isOpen ? "status open" : "status closed";
    if (button) button.textContent = isOpen ? "Close Queue" : "Open Queue";
  }

  // load the saved status for each service
  table.querySelectorAll("tbody tr").forEach(async function (row) {
    const name = row.querySelector(".service-name").textContent;
    try {
      const res = await fetch(`/api/queue/state?serviceName=${encodeURIComponent(name)}`);
      if (res.ok) showStatus(row, (await res.json()).status === "Open");
    } catch (e) { /* keep what the page shows */ }
  });

  table.addEventListener("click", async function (event) {
    if (!event.target.classList.contains("toggle-queue")) return;
    const row = event.target.closest("tr");
    const name = row.querySelector(".service-name").textContent;
    const nowOpen = adminLogic.toggleQueue(row.dataset.open === "true");

    try {
      const res = await fetch(`/api/queue/state?serviceName=${encodeURIComponent(name)}&open=${nowOpen}`, { method: "POST" });
      const body = await res.json().catch(() => null);
      if (!res.ok) {
        message.textContent = (body && body.message) || "Could not change the queue status.";
        return;
      }
      showStatus(row, body.status === "Open");
      message.textContent = name + " queue was " + (nowOpen ? "opened." : "closed.");
    } catch (e) {
      message.textContent = "Can't reach the server. Is dotnet run still going?";
    }
  });
}

// A4: services are loaded from and saved to the Service table
// (GET /api/services, POST /api/services, PUT /api/services/{id}).
function setupServices() {
  const form = document.getElementById("serviceForm");
  if (!form) return;

  const table = document.querySelector("#serviceTable tbody");
  const fields = {
    name: document.getElementById("serviceName"),
    description: document.getElementById("serviceDescription"),
    duration: document.getElementById("serviceDuration"),
    priority: document.getElementById("servicePriority")
  };
  const editing = document.getElementById("editingRow");   // holds the service id being edited
  const error = document.getElementById("serviceError");
  const message = document.getElementById("serviceMessage");

  function clearForm() {
    form.reset();
    editing.value = "";
    error.textContent = "";
    const titleEl = document.getElementById("formTitle");
    if (titleEl) titleEl.textContent = "Create Service";
  }

  function addRow(service) {
    const row = table.insertRow();
    row.dataset.id = service.id;
    row.innerHTML = '<td class="name"></td><td class="description"></td><td class="duration"></td><td class="priority"></td><td><button class="admin-btn small edit-service">Edit</button></td>';
    row.querySelector(".name").textContent = service.name;
    row.querySelector(".description").textContent = service.description;
    row.querySelector(".duration").textContent = service.durationMinutes;
    row.querySelector(".priority").textContent = service.priority;
  }

  async function loadServices() {
    if (!table) return;
    try {
      const res = await fetch("/api/services");
      if (!res.ok) throw new Error();
      const services = await res.json();
      table.innerHTML = "";
      services.forEach(addRow);
    } catch (e) {
      message.textContent = "Could not load services from the server.";
    }
  }

  form.addEventListener("submit", async function (event) {
    event.preventDefault();
    const formValues = {};
    Object.keys(fields).forEach(key => formValues[key] = fields[key].value);
    const result = adminLogic.validateService(formValues);

    if (!result.valid) {
      error.textContent = result.error;
      return;
    }
    const values = result.values;
    const isNew = editing.value === "";

    try {
      const res = await fetch(isNew ? "/api/services" : `/api/services/${editing.value}`, {
        method: isNew ? "POST" : "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          name: values.name,
          description: values.description,
          durationMinutes: Number(values.duration),
          priority: values.priority
        })
      });
      const body = await res.json().catch(() => null);
      if (!res.ok) {
        error.textContent = (body && body.errors && Object.values(body.errors).join(" "))
          || (body && body.error) || "Could not save the service.";
        return;
      }
      clearForm();
      message.textContent = values.name + (isNew ? " was created." : " was updated.");
      await loadServices();
    } catch (e) {
      error.textContent = "Can't reach the server. Is dotnet run still going?";
    }
  });

  if (table) {
    table.addEventListener("click", function (event) {
      if (!event.target.classList.contains("edit-service")) return;
      const row = event.target.closest("tr");
      Object.keys(fields).forEach(key => fields[key].value = row.querySelector("." + key).textContent);
      editing.value = row.dataset.id;
      const titleEl = document.getElementById("formTitle");
      if (titleEl) titleEl.textContent = "Edit Service";
    });
  }

  const clearBtn = document.getElementById("clearServiceForm");
  if (clearBtn) clearBtn.addEventListener("click", clearForm);

  loadServices();
}

function setupQueue() {
  const serviceSelect = document.getElementById("queueService");
  if (!serviceSelect) return;

  const body = document.getElementById("queueBody");
  const message = document.getElementById("queueMessage");
  const serveButton = document.getElementById("serveNextUser");
  const queueCountEl = document.getElementById("queueCount");
  const emptyQueueEl = document.getElementById("emptyQueue");

  async function renderQueue() {
    const selectedServiceText = serviceSelect.options[serviceSelect.selectedIndex].text;
    
    try {
      const response = await fetch(`/api/queue?serviceName=${encodeURIComponent(selectedServiceText)}`);
      if (!response.ok) return;

      const queueItems = await response.json();

      if (body) {
        body.innerHTML = queueItems.map((item, index) => {
          const formattedTime = new Date(item.arrivalTime).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
          return `
            <tr>
              <td>#${index + 1}</td>
              <td>
                <div class="name">${item.name}</div>
                <small style="color:var(--muted);">${item.contact || ''}</small>
              </td>
              <td>${formattedTime}</td>
              <td class="button-row">
                <button class="admin-btn small danger remove-user" data-id="${item.id}">Remove</button>
              </td>
            </tr>`;
        }).join("");
      }

      if (queueCountEl) queueCountEl.textContent = queueItems.length;
      if (emptyQueueEl) emptyQueueEl.hidden = queueItems.length > 0;
      if (serveButton) serveButton.disabled = queueItems.length === 0;

    } catch (err) {
      console.error("Failed to load queue from backend", err);
    }
  }

  if (body) {
    body.addEventListener("click", async function (event) {
      if (!event.target.classList.contains("remove-user")) return;
      const patientId = event.target.dataset.id;

      try {
        const response = await fetch(`/api/queue/leave/${patientId}`, { method: 'DELETE' });
        if (response.ok) {
          if (message) message.textContent = "User was removed from the queue.";
          renderQueue();
        }
      } catch (err) {
        console.error("Error removing user", err);
      }
    });
  }

  if (serveButton) {
    serveButton.addEventListener("click", async function () {
      const selectedServiceText = serviceSelect.options[serviceSelect.selectedIndex].text;

      try {
        const response = await fetch(`/api/queue/serve-next?serviceName=${encodeURIComponent(selectedServiceText)}`, {
          method: 'POST'
        });
        if (response.ok) {
          const served = await response.json();
          if (message) message.textContent = served.name + " is now being served.";
          renderQueue();
        } else {
          if (message) message.textContent = "No users are waiting for this service.";
        }
      } catch (err) {
        console.error("Error serving next user", err);
      }
    });
  }

  serviceSelect.addEventListener("change", function () {
    if (message) message.textContent = "";
    renderQueue();
  });

  renderQueue();
}
