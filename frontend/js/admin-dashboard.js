/* =========================================================================
   admin-dashboard.js - admin home page.
   requireLogin("Admin") blocks students and faculty from this page.
   ========================================================================= */

const adminUser = requireLogin("Admin");

if (adminUser) {
  renderSidebar(adminUser);
  document.getElementById("welcome").textContent = `Welcome, ${adminUser.fullName.split(" ")[0]}`;
  playPageEntrance();
  loadAdminDashboard();
}

async function loadAdminDashboard() {
  try {
    const [stats, pending] = await Promise.all([
      api.get("/bookings/stats"),
      api.get("/bookings?status=Pending")
    ]);

    document.getElementById("stats").innerHTML = [
      { key: "total",    label: "Total Resources",  value: stats.totalResources },
      { key: "",         label: "Total Bookings",   value: stats.total },
      { key: "pending",  label: "Pending Requests", value: stats.pending },
      { key: "approved", label: "Approved Bookings", value: stats.approved }
    ].map(card => `
      <div class="stat ${card.key}">
        <div class="value">${card.value}</div>
        <div class="label">${card.label}</div>
      </div>`).join("");

    animateStats("stats");
    renderPending(pending.slice(0, 5));
    revealOnScroll(".request", { stagger: 70 });
  } catch (error) {
    showMessage("msg", "error", error.message);
    document.getElementById("pending").innerHTML = "";
  }
}

function renderPending(bookings) {
  const container = document.getElementById("pending");

  if (bookings.length === 0) {
    container.innerHTML = `<p class="empty">There are no pending requests. Everything is handled.</p>`;
    return;
  }

  container.innerHTML = bookings.map(b => `
    <div class="request">
      <div class="info">
        <h3>${escapeHtml(b.resourceName)}</h3>
        <div class="line">${escapeHtml(b.userRole)}: ${escapeHtml(b.userName)}</div>
        <div class="line">${formatDate(b.bookingDate)} &nbsp;|&nbsp; ${formatTime(b.startTime)} &ndash; ${formatTime(b.endTime)}</div>
        <div class="line">Purpose: ${escapeHtml(b.purpose)}</div>
      </div>
      <div class="actions">
        <button class="btn btn-success btn-sm" onclick="decide(${b.id}, 'approve')">Approve</button>
        <button class="btn btn-danger  btn-sm" onclick="decide(${b.id}, 'reject')">Reject</button>
      </div>
    </div>`).join("")
    + `<div style="margin-top:14px"><a class="btn btn-secondary btn-sm" href="admin-bookings.html">See all requests</a></div>`;
}

async function decide(id, action) {
  try {
    await api.put(`/bookings/${id}/${action}`);
    showMessage("msg", "success", `Booking ${action === "approve" ? "approved" : "rejected"}.`);
    loadAdminDashboard();
  } catch (error) {
    showMessage("msg", "error", error.message);
  }
}
