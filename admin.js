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
    const nowOpen = adminLogic.toggleQueue(isOpen);
    const status = row.querySelector(".status");
    const name = row.querySelector(".service-name").textContent;

    row.dataset.open = String(nowOpen);
    status.textContent = nowOpen ? "Open" : "Closed";
    status.className = nowOpen ? "status open" : "status closed";
    event.target.textContent = nowOpen ? "Close Queue" : "Open Queue";
    document.getElementById("dashboardMessage").textContent = name + " queue was " + (nowOpen ? "opened." : "closed.");
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
    const formValues = {};
    Object.keys(fields).forEach(key => formValues[key] = fields[key].value);
    const result = adminLogic.validateService(formValues);

    if (!result.valid) {
      error.textContent = result.error;
      return;
    }
    const values = result.values;

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
      adminLogic.moveUser(queue, index, -1);
      message.textContent = "User moved up in the queue.";
    } else if (event.target.classList.contains("move-down")) {
      adminLogic.moveUser(queue, index, 1);
      message.textContent = "User moved down in the queue.";
    } else if (event.target.classList.contains("remove-user")) {
      const removed = adminLogic.removeUser(queue, index);
      message.textContent = removed.name + " was removed from the queue.";
    } else return;
    renderQueue();
  });

  serveButton.addEventListener("click", function () {
    const user = adminLogic.serveNextUser(queues[service.value]);
    if (!user) return;
    message.textContent = user.name + " is now being served.";
    renderQueue();
  });

  service.addEventListener("change", function () { message.textContent = ""; renderQueue(); });
  renderQueue();
}
