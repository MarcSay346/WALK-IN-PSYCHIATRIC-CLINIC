// Small functions used by the administrator pages.
// Keeping them separate makes the important admin actions easy to test.
function cleanService(service) {
  service = service || {};
  return {
    name: String(service.name || "").trim(),
    description: String(service.description || "").trim(),
    duration: String(service.duration || "").trim(),
    priority: String(service.priority || "").trim()
  };
}

function validateService(service) {
  const values = cleanService(service);

  if (!values.name || !values.description || !values.duration || !values.priority) {
    return { valid: false, error: "Please complete all required fields.", values: values };
  }
  if (values.name.length > 100) {
    return { valid: false, error: "Service Name must be 100 characters or less.", values: values };
  }
  if (!Number.isInteger(Number(values.duration)) || Number(values.duration) < 1) {
    return { valid: false, error: "Duration must be a whole number greater than 0.", values: values };
  }
  if (!["Low", "Medium", "High"].includes(values.priority)) {
    return { valid: false, error: "Priority must be Low, Medium, or High.", values: values };
  }

  return { valid: true, error: "", values: values };
}

function toggleQueue(isOpen) {
  return !isOpen;
}

function moveUser(queue, index, direction) {
  const newIndex = index + direction;
  if (!Array.isArray(queue) || ![1, -1].includes(direction)) return false;
  if (index < 0 || index >= queue.length || newIndex < 0 || newIndex >= queue.length) return false;

  [queue[index], queue[newIndex]] = [queue[newIndex], queue[index]];
  return true;
}

function removeUser(queue, index) {
  if (!Array.isArray(queue) || index < 0 || index >= queue.length) return null;
  return queue.splice(index, 1)[0];
}

function serveNextUser(queue) {
  if (!Array.isArray(queue) || queue.length === 0) return null;
  return queue.shift();
}

const adminLogic = {
  validateService,
  toggleQueue,
  moveUser,
  removeUser,
  serveNextUser
};

if (typeof window !== "undefined") window.adminLogic = adminLogic;
if (typeof module !== "undefined" && module.exports) module.exports = adminLogic;
