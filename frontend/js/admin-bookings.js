/* =========================================================================
   admin-bookings.js - the approve / reject screen.
   ========================================================================= */

const bookingAdmin = requireLogin("Admin");

if (bookingAdmin) {
  renderSidebar(bookingAdmin);
  playPageEntrance();
  loadBookings();
  document.getElementById("statusFilter").addEventListener("change", loadBookings);
}

async function loadBookings() {
  const status = document.getElementById("statusFilter").value;
  const container = document.getElementById("list");
  container.innerHTML = `<p class="empty">Loading&hellip;</p>`;

  try {
    const bookings = await api.get(status ? `/bookings?status=${status}` : "/bookings");

    document.getElementById("listTitle").textContent =
      `${status || "All"} bookings (${bookings.length})`;

    if (bookings.length === 0) {
      container.innerHTML = `<p class="empty">There are no ${(status || "").toLowerCase()} bookings.</p>`;
      return;
    }

    // Pending requests get Approve / Reject buttons; the rest are shown as history.
    container.innerHTML = bookings.map(b => `
      <div class="request">
        <div class="info">
          <h3>${escapeHtml(b.resourceName)} &nbsp; ${statusBadge(b.status)}</h3>
          <div class="line">${escapeHtml(b.userRole)}: ${escapeHtml(b.userName)}</div>
          <div class="line">${formatDate(b.bookingDate)} &nbsp;|&nbsp; ${formatTime(b.startTime)} &ndash; ${formatTime(b.endTime)}</div>
          <div class="line">Purpose: ${escapeHtml(b.purpose)}</div>
        </div>
        <div class="actions">
          ${b.status === "Pending" ? `
            <button class="btn btn-success btn-sm" onclick="decide(${b.id}, 'approve')">Approve</button>
            <button class="btn btn-danger  btn-sm" onclick="decide(${b.id}, 'reject')">Reject</button>
          ` : b.status === "Approved" ? `
            <button class="btn btn-danger btn-sm" onclick="decide(${b.id}, 'reject')">Revoke</button>
          ` : ""}
        </div>
      </div>`).join("");
    revealOnScroll(".request", { stagger: 50 });
  } catch (error) {
    showMessage("msg", "error", error.message);
    container.innerHTML = "";
  }
}

async function decide(id, action) {
  try {
    const result = await api.put(`/bookings/${id}/${action}`);
    showMessage("msg", "success",
      `Booking for ${result.resourceName} is now ${result.status}.`);
    loadBookings();
  } catch (error) {
    showMessage("msg", "error", error.message);
  }
}
