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
    document.getElementById("formTitle").textContent = "Create Service";
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
      row = table.insertRow();
      row.innerHTML = '<td class="name"></td><td class="description"></td><td class="duration"></td><td class="priority"></td><td><button class="admin-btn small edit-service">Edit</button></td>';
      message.textContent = values.name + " was created.";
    } else {
      row = table.rows[Number(editing.value)];
      message.textContent = values.name + " was updated.";
    }
    fillRow(row, values);
    clearForm();
  });

  table.addEventListener("click", function (event) {
    if (!event.target.classList.contains("edit-service")) return;
    const row = event.target.closest("tr");
    Object.keys(fields).forEach(key => fields[key].value = row.querySelector("." + key).textContent);
    editing.value = row.sectionRowIndex;
    document.getElementById("formTitle").textContent = "Edit Service";
  });

  document.getElementById("clearServiceForm").addEventListener("click", clearForm);
}

function setupQueue() {
  const service = document.getElementById("queueService");
  if (!service) return;

  const queues = {
    nate: [{ name: "Taylor M.", time: "9:10 AM" }, { name: "Jordan R.", time: "9:18 AM" }, { name: "Casey L.", time: "9:25 AM" }],
    valdez: [{ name: "Morgan S.", time: "9:12 AM" }, { name: "Alex P.", time: "9:23 AM" }],
    cole: []
  };
  const body = document.getElementById("queueBody");
  const message = document.getElementById("queueMessage");
  const serveButton = document.getElementById("serveNextUser");

  function renderQueue() {
    const queue = queues[service.value];
    body.innerHTML = queue.map(function (user, index) {
      return `<tr><td>${index + 1}</td><td>${user.name}</td><td>${user.time}</td><td class="button-row">
        <button class="admin-btn small move-up" data-index="${index}" ${index === 0 ? "disabled" : ""}>Up</button>
        <button class="admin-btn small move-down" data-index="${index}" ${index === queue.length - 1 ? "disabled" : ""}>Down</button>
        <button class="admin-btn small danger remove-user" data-index="${index}">Remove</button></td></tr>`;
    }).join("");
    document.getElementById("queueCount").textContent = queue.length;
    document.getElementById("emptyQueue").hidden = queue.length > 0;
    serveButton.disabled = queue.length === 0;
  }

  body.addEventListener("click", function (event) {
    const index = Number(event.target.dataset.index);
    const queue = queues[service.value];
    if (event.target.classList.contains("move-up")) {
      [queue[index - 1], queue[index]] = [queue[index], queue[index - 1]];
      message.textContent = "User moved up in the queue.";
    } else if (event.target.classList.contains("move-down")) {
      [queue[index], queue[index + 1]] = [queue[index + 1], queue[index]];
      message.textContent = "User moved down in the queue.";
    } else if (event.target.classList.contains("remove-user")) {
      message.textContent = queue[index].name + " was removed from the queue.";
      queue.splice(index, 1);
    } else return;
    renderQueue();
  });

  serveButton.addEventListener("click", function () {
    const user = queues[service.value].shift();
    message.textContent = user.name + " is now being served.";
    renderQueue();
  });

  service.addEventListener("change", function () { message.textContent = ""; renderQueue(); });
  renderQueue();
}
