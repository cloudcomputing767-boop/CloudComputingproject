/* =========================================================================
   admin-resources.js - add / edit / deactivate resources (admin only).
   ========================================================================= */

const resourceAdmin = requireLogin("Admin");
let resourceList = [];

if (resourceAdmin) {
  renderSidebar(resourceAdmin);
  loadResourceList();

  document.getElementById("resourceForm").addEventListener("submit", saveResource);
  document.getElementById("cancelBtn").addEventListener("click", resetForm);

  buildImagePicker();
  setUpUpload();
  document.getElementById("imageUrl").addEventListener("input", updateImagePreview);
}

async function loadResourceList() {
  try {
    // includeInactive=true -> the admin also sees deactivated resources.
    resourceList = await api.get("/resources?includeInactive=true");
    renderList();
  } catch (error) {
    showMessage("msg", "error", error.message);
    document.getElementById("list").innerHTML = "";
  }
}

/* ------------------------------ uploading a picture ------------------------------ */

/**
 * Lets the admin pick a picture from their own computer, either by clicking
 * the box or by dragging a file onto it.
 *
 * The file is sent to POST /api/images, which stores it in the cloud database
 * and replies with its address, for example "/api/images/7". That address is
 * then written into the Picture field and saved on the resource.
 */
function setUpUpload() {
  const zone = document.getElementById("uploadZone");
  const input = document.getElementById("imageFile");

  zone.addEventListener("click", () => input.click());
  zone.addEventListener("keydown", event => {
    if (event.key === "Enter" || event.key === " ") { event.preventDefault(); input.click(); }
  });

  input.addEventListener("change", () => {
    if (input.files.length > 0) uploadImageFile(input.files[0]);
  });

  // Drag and drop.
  ["dragenter", "dragover"].forEach(name =>
    zone.addEventListener(name, event => {
      event.preventDefault();
      zone.classList.add("dragging");
    }));

  ["dragleave", "drop"].forEach(name =>
    zone.addEventListener(name, event => {
      event.preventDefault();
      zone.classList.remove("dragging");
    }));

  zone.addEventListener("drop", event => {
    const file = event.dataTransfer.files[0];
    if (file) uploadImageFile(file);
  });
}

/** Sends one file to the API and puts the returned address in the form. */
async function uploadImageFile(file) {
  const status = document.getElementById("uploadStatus");

  // A quick check in the browser; the server checks properly as well.
  if (!file.type.startsWith("image/")) {
    status.innerHTML = `<div class="alert alert-error">Please choose an image file.</div>`;
    return;
  }
  if (file.size > 2 * 1024 * 1024) {
    status.innerHTML = `<div class="alert alert-error">"${escapeHtml(file.name)}" is larger than 2 MB. Please choose a smaller image.</div>`;
    return;
  }

  status.innerHTML = `
    <div class="alert alert-info">
      Uploading ${escapeHtml(file.name)} (${formatFileSize(file.size)})&hellip;
      <div class="upload-progress"><span></span></div>
    </div>`;

  try {
    const result = await api.uploadImage(file);

    // The address of the picture now lives in the database.
    document.getElementById("imageUrl").value = result.url;
    updateImagePreview();

    status.innerHTML =
      `<div class="alert alert-success">&#10003; ${escapeHtml(result.fileName)} uploaded. ` +
      `Press <strong>${document.getElementById("saveBtn").textContent}</strong> to use it.</div>`;
  } catch (error) {
    status.innerHTML = `<div class="alert alert-error">${escapeHtml(error.message)}</div>`;
  } finally {
    document.getElementById("imageFile").value = "";   // allow re-picking the same file
  }
}

/** 18234 -> "18 KB" */
function formatFileSize(bytes) {
  return bytes < 1024 ? `${bytes} B`
       : bytes < 1024 * 1024 ? `${Math.round(bytes / 1024)} KB`
       : `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

/* ------------------------------ picture picker ------------------------------ */

/** Draws the row of ready-made pictures the admin can choose from. */
function buildImagePicker() {
  document.getElementById("imagePicker").innerHTML = BUILT_IN_IMAGES.map(img => `
    <button type="button" data-url="${img.url}" onclick="pickImage('${img.url}')" title="${img.label}">
      <img src="${img.url}" alt="${img.label}">
      <span class="cap">${img.label}</span>
    </button>`).join("");
}

/** Clicking one of the pictures just fills in the text field. */
function pickImage(url) {
  document.getElementById("imageUrl").value = url;
  updateImagePreview();
}

/** Keeps the preview and the highlighted thumbnail in step with the text field. */
function updateImagePreview() {
  const url = document.getElementById("imageUrl").value.trim();
  const preview = document.getElementById("imagePreview");

  if (url) {
    // resourceImage() turns "/api/images/7" into a full address.
    document.getElementById("imagePreviewImg").src = resourceImage({ imageUrl: url });
    preview.hidden = false;
  } else {
    preview.hidden = true;
  }

  document.querySelectorAll("#imagePicker button").forEach(button => {
    button.classList.toggle("selected", button.dataset.url === url);
  });
}

function renderList() {
  const container = document.getElementById("list");

  if (resourceList.length === 0) {
    container.innerHTML = `<p class="empty">No resources yet. Add the first one above.</p>`;
    return;
  }

  container.innerHTML = `
    <div class="table-wrap">
      <table>
        <thead>
          <tr><th>Picture</th><th>Name</th><th>Type</th><th>Location</th><th>Capacity</th><th>Status</th><th></th></tr>
        </thead>
        <tbody>
          ${resourceList.map(r => `
            <tr>
              <td>
                <img src="${escapeHtml(resourceImage(r))}" alt="${escapeHtml(r.name)}"
                     onerror="this.onerror=null;this.src='${FALLBACK_IMAGE}'"
                     style="width:58px;height:38px;object-fit:cover;border-radius:5px;display:block">
              </td>
              <td><strong>${escapeHtml(r.name)}</strong></td>
              <td>${escapeHtml(r.type)}</td>
              <td>${escapeHtml(r.location)}</td>
              <td>${r.capacity}</td>
              <td><span class="badge badge-${r.isActive ? "active" : "inactive"}">${r.isActive ? "Active" : "Inactive"}</span></td>
              <td>
                <div class="btn-row">
                  <button class="btn btn-secondary btn-sm" onclick="editResource(${r.id})">Edit</button>
                  <button class="btn btn-danger btn-sm" onclick="deleteResource(${r.id})">Delete</button>
                </div>
              </td>
            </tr>`).join("")}
        </tbody>
      </table>
    </div>`;
}

/** Loads a resource into the form so it can be edited. */
function editResource(id) {
  const resource = resourceList.find(r => r.id === id);
  if (!resource) return;

  document.getElementById("resourceId").value  = resource.id;
  document.getElementById("name").value        = resource.name;
  document.getElementById("type").value        = resource.type;
  document.getElementById("location").value    = resource.location;
  document.getElementById("capacity").value    = resource.capacity;
  document.getElementById("description").value = resource.description;
  document.getElementById("imageUrl").value    = resource.imageUrl || "";
  document.getElementById("isActive").value    = String(resource.isActive);
  updateImagePreview();

  document.getElementById("formTitle").textContent = `Edit: ${resource.name}`;
  document.getElementById("saveBtn").textContent   = "Save Changes";
  document.getElementById("cancelBtn").style.display = "inline-block";

  window.scrollTo({ top: 0, behavior: "smooth" });
}

function resetForm() {
  document.getElementById("resourceForm").reset();
  document.getElementById("resourceId").value = "";
  document.getElementById("capacity").value = 30;
  document.getElementById("imageUrl").value = "";
  document.getElementById("uploadStatus").innerHTML = "";
  updateImagePreview();
  document.getElementById("formTitle").textContent = "Add a new resource";
  document.getElementById("saveBtn").textContent   = "Add Resource";
  document.getElementById("cancelBtn").style.display = "none";
  clearMessage("msg");
}

/** One function handles both "create" (POST) and "update" (PUT). */
async function saveResource(event) {
  event.preventDefault();

  const id = document.getElementById("resourceId").value;
  const body = {
    name:        document.getElementById("name").value.trim(),
    type:        document.getElementById("type").value.trim(),
    location:    document.getElementById("location").value.trim(),
    capacity:    Number(document.getElementById("capacity").value),
    description: document.getElementById("description").value.trim(),
    imageUrl:    document.getElementById("imageUrl").value.trim(),
    isActive:    document.getElementById("isActive").value === "true"
  };

  const button = document.getElementById("saveBtn");
  button.disabled = true;

  try {
    if (id) {
      await api.put(`/resources/${id}`, body);
      showMessage("msg", "success", `"${body.name}" was updated.`);
    } else {
      await api.post("/resources", body);
      showMessage("msg", "success", `"${body.name}" was added.`);
    }

    resetForm();
    loadResourceList();
  } catch (error) {
    showMessage("msg", "error", error.message);
  } finally {
    button.disabled = false;
  }
}

/**
 * Deleting is safe: the API deletes the resource only if it has never been
 * booked, otherwise it just deactivates it and keeps the history.
 */
async function deleteResource(id) {
  const resource = resourceList.find(r => r.id === id);
  if (!confirm(`Remove "${resource.name}"?\n\nIf it already has bookings it will be deactivated instead of deleted.`)) return;

  try {
    const result = await api.del(`/resources/${id}`);
    showMessage("msg", result.deactivated ? "warning" : "success", result.message);
    loadResourceList();
  } catch (error) {
    showMessage("msg", "error", error.message);
  }
}
