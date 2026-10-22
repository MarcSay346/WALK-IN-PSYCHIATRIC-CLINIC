document.addEventListener("DOMContentLoaded", function () {
  setupDashboard();
  setupServices();
  setupQueue();
});

function setupDashboard() {
  const table = document.getElementById("dashboardServiceTable");
  if (!table) return;

  table.addEventListener("click", function (event) {
    if (!event.target.classList.contains("toggle-queue")) return;
    const row = event.target.closest("tr");
    const isOpen = row.dataset.open === "true";
    const status = row.querySelector(".status");
    const name = row.querySelector(".service-name").textContent;

    row.dataset.open = String(!isOpen);
    status.textContent = isOpen ? "Closed" : "Open";
    status.className = isOpen ? "status closed" : "status open";
    event.target.textContent = isOpen ? "Open Queue" : "Close Queue";
    document.getElementById("dashboardMessage").textContent = name + " queue was " + (isOpen ? "closed." : "opened.");
  });
}

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
  const editing = document.getElementById("editingRow");
  const error = document.getElementById("serviceError");
  const message = document.getElementById("serviceMessage");

  function clearForm() {
    form.reset();
    editing.value = "";
    error.textContent = "";
    const titleEl = document.getElementById("formTitle");
    if (titleEl) titleEl.textContent = "Create Service";
  }

  function fillRow(row, values) {
    row.querySelector(".name").textContent = values.name;
    row.querySelector(".description").textContent = values.description;
    row.querySelector(".duration").textContent = values.duration;
    row.querySelector(".priority").textContent = values.priority;
  }

  form.addEventListener("submit", function (event) {
    event.preventDefault();
    const values = {};
    Object.keys(fields).forEach(key => values[key] = fields[key].value.trim());

    if (!values.name || !values.description || !values.duration || !values.priority) {
      error.textContent = "Please complete all required fields.";
      return;
    }
    if (values.name.length > 100) {
      error.textContent = "Service Name must be 100 characters or less.";
      return;
    }
    if (!Number.isInteger(Number(values.duration)) || Number(values.duration) < 1) {
      error.textContent = "Duration must be a whole number greater than 0.";
      return;
    }

    let row;
    if (editing.value === "") {
      if (table) {
        row = table.insertRow();
        row.innerHTML = '<td class="name"></td><td class="description"></td><td class="duration"></td><td class="priority"></td><td><button class="admin-btn small edit-service">Edit</button></td>';
      }
      message.textContent = values.name + " was created.";
    } else {
      if (table) {
        row = table.rows[Number(editing.value)];
      }
      message.textContent = values.name + " was updated.";
    }
    if (row) fillRow(row, values);
    clearForm();
  });

  if (table) {
    table.addEventListener("click", function (event) {
      if (!event.target.classList.contains("edit-service")) return;
      const row = event.target.closest("tr");
      Object.keys(fields).forEach(key => fields[key].value = row.querySelector("." + key).textContent);
      editing.value = row.sectionRowIndex;
      const titleEl = document.getElementById("formTitle");
      if (titleEl) titleEl.textContent = "Edit Service";
    });
  }

  const clearBtn = document.getElementById("clearServiceForm");
  if (clearBtn) clearBtn.addEventListener("click", clearForm);
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
