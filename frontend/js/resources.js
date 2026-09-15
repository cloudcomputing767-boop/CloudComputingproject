/* =========================================================================
   resources.js - browse and filter the resources.
   ========================================================================= */

const currentUser = requireLogin();
let allResources = [];

if (currentUser) {
  renderSidebar(currentUser);
  playPageEntrance();
  loadResources();

  document.getElementById("search").addEventListener("input", applyFilters);
  document.getElementById("typeFilter").addEventListener("change", applyFilters);
}

async function loadResources() {
  try {
    allResources = await api.get("/resources");

    // Build the "Type" dropdown from the data we received.
    const types = [...new Set(allResources.map(r => r.type))].sort();
    document.getElementById("typeFilter").innerHTML =
      `<option value="">All types</option>` +
      types.map(t => `<option value="${escapeHtml(t)}">${escapeHtml(t)}</option>`).join("");

    applyFilters();
  } catch (error) {
    showMessage("msg", "error", error.message);
    document.getElementById("resources").innerHTML = "";
  }
}

function applyFilters() {
  const search = document.getElementById("search").value.trim().toLowerCase();
  const type   = document.getElementById("typeFilter").value;

  const filtered = allResources.filter(resource => {
    const matchesType   = !type || resource.type === type;
    const matchesSearch = !search
      || resource.name.toLowerCase().includes(search)
      || resource.location.toLowerCase().includes(search);
    return matchesType && matchesSearch;
  });

  renderResourceCards("resources", filtered);
  revealOnScroll(".resource-card");
}
