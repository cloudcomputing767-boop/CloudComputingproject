/* =========================================================================
   layout.js - builds the sidebar and holds small shared helpers.
   Writing the menu once here keeps all pages consistent.
   ========================================================================= */

/** Menu entries per role. */
const MENUS = {
  user: [
    { href: "dashboard.html",   label: "Dashboard" },
    { href: "resources.html",   label: "Browse & Book" },
    { href: "my-bookings.html", label: "My Bookings" }
  ],
  admin: [
    { href: "admin-dashboard.html", label: "Dashboard" },
    { href: "admin-bookings.html",  label: "Booking Requests" },
    { href: "admin-resources.html", label: "Manage Resources" },
    { href: "utilization.html",     label: "Utilization Report" }
  ]
};

/** Renders the sidebar into <div id="sidebar"></div>. */
function renderSidebar(user) {
  const menu = user.role === "Admin" ? MENUS.admin : MENUS.user;
  const current = window.location.pathname.split("/").pop() || "index.html";

  const links = menu.map(item =>
    `<a href="${item.href}" class="${item.href === current ? "active" : ""}">${item.label}</a>`
  ).join("");

  const sidebar = document.getElementById("sidebar");
  sidebar.className = "sidebar";
  sidebar.innerHTML = `
    <div class="brand">
      College Booking
      <span>Resource Management</span>
    </div>
    <button class="menu-toggle" onclick="document.getElementById('sidebar').classList.toggle('open')">Menu</button>
    <nav>${links}</nav>
    <div class="user-box">
      <div class="name">${escapeHtml(user.fullName)}</div>
      <div class="role">${user.role}</div>
      <button onclick="logout()">Log out</button>
    </div>`;
}

/* ------------------------------ small helpers ------------------------------ */

/** Never insert raw API text into HTML - this makes it safe. */
function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>"']/g, c =>
    ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}

/** "2026-09-20" -> "20/09/2026" */
function formatDate(isoDate) {
  const [y, m, d] = isoDate.split("-");
  return `${d}/${m}/${y}`;
}

/** "10:00:00" -> "10:00" */
function formatTime(time) {
  return String(time).slice(0, 5);
}

/** Coloured status badge. */
function statusBadge(status) {
  return `<span class="badge badge-${status.toLowerCase()}">${status}</span>`;
}

/** Shows a message in an element, e.g. showMessage("msg", "error", "Wrong password"). */
function showMessage(elementId, type, text) {
  const el = document.getElementById(elementId);
  if (!el) return;
  el.innerHTML = `<div class="alert alert-${type}">${escapeHtml(text)}</div>`;
}

function clearMessage(elementId) {
  const el = document.getElementById(elementId);
  if (el) el.innerHTML = "";
}

/** Today as "YYYY-MM-DD", used as the minimum date in booking forms. */
function todayIso() {
  const now = new Date();
  const pad = n => String(n).padStart(2, "0");
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
}
