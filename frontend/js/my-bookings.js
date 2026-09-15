/* =========================================================================
   my-bookings.js - booking history of the logged-in user.
   ========================================================================= */

const myUser = requireLogin();

if (myUser) {
  renderSidebar(myUser);
  playPageEntrance();
  loadMyBookings();
}

async function loadMyBookings() {
  try {
    const [stats, bookings] = await Promise.all([
      api.get("/bookings/stats"),
      api.get("/bookings/my")
    ]);

    document.getElementById("stats").innerHTML = [
      { key: "total",    label: "Total",    value: stats.total },
      { key: "pending",  label: "Pending",  value: stats.pending },
      { key: "approved", label: "Approved", value: stats.approved },
      { key: "rejected", label: "Rejected", value: stats.rejected }
    ].map(card => `
      <div class="stat ${card.key}">
        <div class="value">${card.value}</div>
        <div class="label">${card.label}</div>
      </div>`).join("");

    animateStats("stats");
    renderBookingTable(bookings);
  } catch (error) {
    showMessage("msg", "error", error.message);
    document.getElementById("bookings").innerHTML = "";
  }
}

function renderBookingTable(bookings) {
  const container = document.getElementById("bookings");

  if (bookings.length === 0) {
    container.innerHTML = `
      <div class="empty">
        You have not made any bookings yet.<br><br>
        <a class="btn btn-primary btn-sm" href="resources.html">Browse resources and book</a>
      </div>`;
    return;
  }

  container.innerHTML = `
    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Resource</th><th>Date</th><th>Start</th><th>End</th>
            <th>Purpose</th><th>Status</th><th></th>
          </tr>
        </thead>
        <tbody>
          ${bookings.map(b => `
            <tr>
              <td>
                <strong>${escapeHtml(b.resourceName)}</strong><br>
                <span class="meta" style="color:var(--text-muted);font-size:.82rem">${escapeHtml(b.resourceLocation)}</span>
              </td>
              <td>${formatDate(b.bookingDate)}</td>
              <td>${formatTime(b.startTime)}</td>
              <td>${formatTime(b.endTime)}</td>
              <td>${escapeHtml(b.purpose)}</td>
              <td>${statusBadge(b.status)}</td>
              <td>
                ${b.status === "Pending" || b.status === "Approved"
                  ? `<button class="btn btn-secondary btn-sm" onclick="cancelBooking(${b.id})">Cancel</button>`
                  : ""}
              </td>
            </tr>`).join("")}
        </tbody>
      </table>
    </div>`;
}

/** Cancelling frees the time slot for other users. */
async function cancelBooking(id) {
  if (!confirm("Cancel this booking? The time slot will become free for others.")) return;

  try {
    await api.put(`/bookings/${id}/cancel`);
    showMessage("msg", "success", "Your booking has been cancelled.");
    loadMyBookings();
  } catch (error) {
    showMessage("msg", "error", error.message);
  }
}
