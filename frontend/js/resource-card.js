/* =========================================================================
   resource-card.js

   Draws the resource cards WITH their picture, and the small booking panel
   that opens directly under a card - like a hotel booking website.

   The student never has to leave the page: click a card, pick the date and
   time, press "Check Availability", then "Book Now".

   The same file is used by the dashboard and by the Resources page.
   ========================================================================= */

/** Where the built-in pictures live, and a friendly name for each one. */
const BUILT_IN_IMAGES = [
  { url: "images/resources/classroom.svg",      label: "Classroom" },
  { url: "images/resources/classroom-2.svg",    label: "Classroom (green board)" },
  { url: "images/resources/computer-lab.svg",   label: "Computer Lab" },
  { url: "images/resources/computer-lab-2.svg", label: "Networking Lab" },
  { url: "images/resources/seminar-hall.svg",   label: "Seminar Hall" },
  { url: "images/resources/projector.svg",      label: "Projector" },
  { url: "images/resources/meeting-room.svg",   label: "Meeting Room" },
  { url: "images/resources/sports.svg",         label: "Sports Facility" },
  { url: "images/resources/default.svg",        label: "General" }
];

const FALLBACK_IMAGE = "images/resources/default.svg";

/**
 * Works out the real address of a resource's picture.
 *
 * Three kinds of address can be stored in the database:
 *   "images/resources/classroom.svg"  a ready-made picture next to the website
 *   "/api/images/7"                   a picture the admin uploaded (stored in
 *                                     the database, served by our own API)
 *   "https://..."                     any picture somewhere on the internet
 *
 * An empty value falls back to a general picture, so a card is never blank.
 */
function resourceImage(resource) {
  const url = (resource.imageUrl || "").trim();

  if (url === "") return FALLBACK_IMAGE;
  if (/^https?:\/\//i.test(url)) return url;
  if (url.startsWith("/api/")) return apiOrigin() + url;   // uploaded picture
  return url;                                              // file in frontend/images/
}

/* The resources currently drawn on the page, so the panel can find them again. */
let cardResources = [];

/**
 * Draws the cards into a container.
 * @param {string} containerId  id of the grid element
 * @param {Array}  resources    resources from the API
 */
function renderResourceCards(containerId, resources) {
  cardResources = resources;
  const container = document.getElementById(containerId);

  if (resources.length === 0) {
    container.innerHTML = `<p class="empty">No resources match your search.</p>`;
    return;
  }

  container.innerHTML = resources.map(r => `
    <div class="resource-card" id="card-${r.id}">
      <div class="photo" onclick="toggleBooking(${r.id})" title="Click to book ${escapeHtml(r.name)}">
        <img src="${escapeHtml(resourceImage(r))}" alt="${escapeHtml(r.name)}"
             onerror="this.onerror=null;this.src='${FALLBACK_IMAGE}'">
        <span class="capacity-tag">Capacity ${r.capacity}</span>
      </div>

      <div class="body">
        <span class="type">${escapeHtml(r.type)}</span>
        <h3>${escapeHtml(r.name)}</h3>
        <div class="meta">${escapeHtml(r.location)}</div>
        <p class="desc">${escapeHtml(r.description)}</p>

        <button class="btn btn-primary btn-sm" id="toggle-${r.id}" onclick="toggleBooking(${r.id})">
          View &amp; Book
        </button>

        <div class="book-panel" id="panel-${r.id}" hidden></div>
      </div>
    </div>`).join("");
}

/** Opens or closes the booking panel of one card. */
function toggleBooking(id) {
  const panel = document.getElementById(`panel-${id}`);
  const card = document.getElementById(`card-${id}`);
  const button = document.getElementById(`toggle-${id}`);
  if (!panel) return;

  // Close any other open card first, so only one form is visible at a time.
  cardResources.forEach(r => {
    if (r.id !== id) {
      const other = document.getElementById(`panel-${r.id}`);
      if (other && !other.hidden) {
        other.hidden = true;
        document.getElementById(`card-${r.id}`).classList.remove("open");
        document.getElementById(`toggle-${r.id}`).textContent = "View & Book";
      }
    }
  });

  if (panel.hidden) {
    panel.innerHTML = bookingPanelHtml(id);
    panel.hidden = false;
    card.classList.add("open");
    button.textContent = "Close";
    loadPanelBusySlots(id);
    card.scrollIntoView({ behavior: "smooth", block: "nearest" });
  } else {
    panel.hidden = true;
    card.classList.remove("open");
    button.textContent = "View & Book";
  }
}

/** The little booking form shown inside a card. */
function bookingPanelHtml(id) {
  return `
    <div class="panel-title">Book this resource</div>

    <div class="time-grid">
      <div class="form-row">
        <label for="date-${id}">Date</label>
        <input type="date" id="date-${id}" min="${todayIso()}" value="${todayIso()}"
               onchange="onPanelChange(${id})">
      </div>
      <div class="form-row">
        <label for="start-${id}">Start time</label>
        <input type="time" id="start-${id}" step="900" value="10:00" onchange="onPanelChange(${id})">
      </div>
      <div class="form-row">
        <label for="end-${id}">End time</label>
        <input type="time" id="end-${id}" step="900" value="12:00" onchange="onPanelChange(${id})">
      </div>
    </div>

    <div class="form-row">
      <label for="purpose-${id}">Purpose</label>
      <textarea id="purpose-${id}" maxlength="300"
                placeholder="For example: Database practical session for CS-301"></textarea>
    </div>

    <div id="result-${id}"></div>

    <div class="btn-row">
      <button class="btn btn-secondary btn-sm" id="check-${id}" onclick="checkCardAvailability(${id})">
        Check Availability
      </button>
      <button class="btn btn-primary btn-sm" id="book-${id}" onclick="bookFromCard(${id})" disabled>
        Book Now
      </button>
    </div>

    <div class="busy-list" id="busy-${id}"></div>`;
}

/** Changing the date or time cancels the previous availability answer. */
function onPanelChange(id) {
  document.getElementById(`book-${id}`).disabled = true;
  document.getElementById(`result-${id}`).innerHTML = "";
  loadPanelBusySlots(id);
}

/** Reads the three time fields, validating them. Returns null when invalid. */
function readPanel(id) {
  const date  = document.getElementById(`date-${id}`).value;
  const start = document.getElementById(`start-${id}`).value;
  const end   = document.getElementById(`end-${id}`).value;

  if (!date || !start || !end) {
    panelMessage(id, "error", "Please choose a date, a start time and an end time.");
    return null;
  }
  if (start >= end) {
    panelMessage(id, "error", "Start time must be before end time.");
    return null;
  }
  return { date, start, end };
}

function panelMessage(id, type, text, extraHtml = "") {
  document.getElementById(`result-${id}`).innerHTML =
    `<div class="alert alert-${type}">${text}${extraHtml}</div>`;
}

/** Asks the server whether the slot is free. */
async function checkCardAvailability(id) {
  const form = readPanel(id);
  if (!form) return;

  const button = document.getElementById(`check-${id}`);
  button.disabled = true;
  button.textContent = "Checking...";

  try {
    const result = await api.get(
      `/bookings/availability?resourceId=${id}&date=${form.date}` +
      `&startTime=${form.start}&endTime=${form.end}`);

    if (result.available) {
      panelMessage(id, "success", "&#10003; " + escapeHtml(result.message));
      document.getElementById(`book-${id}`).disabled = false;
    } else {
      const conflicts = result.conflictingSlots.length
        ? `<ul>${result.conflictingSlots.map(s =>
            `<li>${formatTime(s.startTime)} &ndash; ${formatTime(s.endTime)} (${s.status})</li>`).join("")}</ul>`
        : "";
      panelMessage(id, "error", "&#10007; " + escapeHtml(result.message), conflicts);
      document.getElementById(`book-${id}`).disabled = true;
    }
  } catch (error) {
    panelMessage(id, "error", escapeHtml(error.message));
  } finally {
    button.disabled = false;
    button.textContent = "Check Availability";
  }
}

/** Sends the booking. The server checks availability again before saving it. */
async function bookFromCard(id) {
  const form = readPanel(id);
  if (!form) return;

  const purpose = document.getElementById(`purpose-${id}`).value.trim();
  if (purpose.length < 3) {
    panelMessage(id, "error", "Please describe the purpose of your booking.");
    return;
  }

  const button = document.getElementById(`book-${id}`);
  button.disabled = true;
  button.textContent = "Booking...";

  try {
    await api.post("/bookings", {
      resourceId:  id,
      bookingDate: form.date,
      startTime:   form.start,
      endTime:     form.end,
      purpose:     purpose
    });

    panelMessage(id, "success",
      "&#10003; Booking request sent. It is now pending approval by the administrator. " +
      `<a href="my-bookings.html">See my bookings</a>`);

    document.getElementById(`purpose-${id}`).value = "";
    loadPanelBusySlots(id);

    // Refresh the counters on the dashboard, if this page has them.
    if (typeof refreshStats === "function") refreshStats();
  } catch (error) {
    panelMessage(id, "error", escapeHtml(error.message));
  } finally {
    button.textContent = "Book Now";
    button.disabled = true;   // must check availability again before re-booking
  }
}

/** Shows the times that are already taken on the chosen date. */
async function loadPanelBusySlots(id) {
  const dateField = document.getElementById(`date-${id}`);
  const busy = document.getElementById(`busy-${id}`);
  if (!dateField || !busy) return;

  try {
    const slots = await api.get(`/bookings/busy?resourceId=${id}&date=${dateField.value}`);

    busy.innerHTML = slots.length === 0
      ? `Free all day on ${formatDate(dateField.value)}.`
      : `Already booked on ${formatDate(dateField.value)}: ` +
        slots.map(s => `<span>${formatTime(s.startTime)}&ndash;${formatTime(s.endTime)}</span>`).join("");
  } catch {
    busy.innerHTML = "";
  }
}
