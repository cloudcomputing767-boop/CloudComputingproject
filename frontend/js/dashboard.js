/* =========================================================================
   dashboard.js - the student / faculty home page.
   ========================================================================= */

const user = requireLogin();   // any logged-in non-admin user

if (user) {
  renderSidebar(user);
  document.getElementById("welcome").textContent = `Welcome, ${user.fullName.split(" ")[0]}`;
  document.getElementById("subtitle").textContent =
    user.role === "Faculty"
      ? "Book classrooms, labs and equipment for your classes."
      : "Book classrooms, labs and equipment for your studies.";

  playPageEntrance();
  loadDashboard();
}

async function loadDashboard() {
  try {
    // Two API calls, run at the same time.
    const [stats, resources] = await Promise.all([
      api.get("/bookings/stats"),
      api.get("/resources")
    ]);

    renderStats(stats);
    animateStats("stats");          // the numbers count up from zero
    renderResources(resources);
    revealOnScroll(".resource-card");   // cards fade in as you scroll
  } catch (error) {
    showMessage("msg", "error", error.message);
    document.getElementById("resources").innerHTML = "";
  }
}

function renderStats(stats) {
  const cards = [
    { key: "total",    label: "Total Bookings", value: stats.total },
    { key: "pending",  label: "Pending",        value: stats.pending },
    { key: "approved", label: "Approved",       value: stats.approved },
    { key: "rejected", label: "Rejected",       value: stats.rejected }
  ];

  document.getElementById("stats").innerHTML = cards.map(card => `
    <div class="stat ${card.key}">
      <div class="value">${card.value}</div>
      <div class="label">${card.label}</div>
    </div>`).join("");
}

function renderResources(resources) {
  // Cards with pictures + the inline booking panel (see js/resource-card.js).
  renderResourceCards("resources", resources);
}

/** Called after a booking is made from a card, so the counters stay correct. */
async function refreshStats() {
  try {
    renderStats(await api.get("/bookings/stats"));
  } catch {
    /* the booking already succeeded - a stale counter is not worth an error */
  }
}
