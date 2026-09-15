/* =========================================================================
   utilization.js - a simple usage report for the admin.
   No advanced analytics: just how many times each resource was booked.
   ========================================================================= */

const reportAdmin = requireLogin("Admin");

if (reportAdmin) {
  renderSidebar(reportAdmin);
  playPageEntrance();
  loadReport();
}

async function loadReport() {
  try {
    const rows = await api.get("/reports/utilization");
    renderSummary(rows);
    animateStats("summary");
    renderReport(rows);
    animateBars();
  } catch (error) {
    showMessage("msg", "error", error.message);
    document.getElementById("report").innerHTML = "";
  }
}

function renderSummary(rows) {
  const totalBookings = rows.reduce((sum, r) => sum + r.totalBookings, 0);
  const totalHours    = rows.reduce((sum, r) => sum + r.bookedHours, 0);
  const used          = rows.filter(r => r.totalBookings > 0).length;
  const busiest       = rows.find(r => r.totalBookings > 0);

  document.getElementById("summary").innerHTML = `
    <div class="stat total">
      <div class="value">${totalBookings}</div><div class="label">Total Bookings</div>
    </div>
    <div class="stat approved">
      <div class="value">${Math.round(totalHours * 10) / 10}</div><div class="label">Approved Hours Booked</div>
    </div>
    <div class="stat">
      <div class="value">${used} / ${rows.length}</div><div class="label">Resources Used</div>
    </div>
    <div class="stat">
      <div class="value" style="font-size:1.1rem;padding-top:.5rem">${busiest ? escapeHtml(busiest.resourceName) : "&ndash;"}</div>
      <div class="label">Most Booked Resource</div>
    </div>`;
}

/** Grows the usage bars from zero to their real width. */
function animateBars() {
  document.querySelectorAll(".bar > span").forEach((bar, index) => {
    const width = bar.style.width;
    if (prefersReducedMotion()) return;

    bar.style.width = "0%";
    bar.style.transition = "width .7s cubic-bezier(.22,.8,.32,1)";
    setTimeout(() => { bar.style.width = width; }, 80 + index * 45);
  });
}

function renderReport(rows) {
  const container = document.getElementById("report");

  if (rows.length === 0) {
    container.innerHTML = `<p class="empty">There are no resources yet.</p>`;
    return;
  }

  // The longest bar is the resource with the most bookings.
  const max = Math.max(1, ...rows.map(r => r.totalBookings));

  container.innerHTML = `
    <div class="table-wrap">
      <table>
        <thead>
          <tr>
            <th>Resource</th><th>Type</th><th>Bookings</th>
            <th>Approved</th><th>Pending</th><th>Hours</th><th style="width:200px">Share</th>
          </tr>
        </thead>
        <tbody>
          ${rows.map(r => `
            <tr>
              <td><strong>${escapeHtml(r.resourceName)}</strong></td>
              <td>${escapeHtml(r.type)}</td>
              <td>${r.totalBookings}</td>
              <td>${r.approvedBookings}</td>
              <td>${r.pendingBookings}</td>
              <td>${r.bookedHours}</td>
              <td>
                <div class="bar"><span style="width:${Math.round(r.totalBookings / max * 100)}%"></span></div>
                <span style="font-size:.78rem;color:var(--text-muted)">${r.sharePercent}% of all bookings</span>
              </td>
            </tr>`).join("")}
        </tbody>
      </table>
    </div>`;
}
