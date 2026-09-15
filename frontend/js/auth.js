/* =========================================================================
   auth.js - stores the logged-in user in the browser and guards the pages.
   ========================================================================= */

/** Saves the token and user details returned by the API. */
function saveSession(authResponse) {
  localStorage.setItem(TOKEN_KEY, authResponse.token);
  localStorage.setItem(USER_KEY, JSON.stringify({
    userId:   authResponse.userId,
    fullName: authResponse.fullName,
    email:    authResponse.email,
    role:     authResponse.role
  }));
}

function getUser() {
  const raw = localStorage.getItem(USER_KEY);
  return raw ? JSON.parse(raw) : null;
}

function isLoggedIn() {
  return !!localStorage.getItem(TOKEN_KEY) && !!getUser();
}

function clearSession() {
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
}

function logout() {
  clearSession();
  window.location.href = "login.html";
}

/** Sends the user to the dashboard that matches their role. */
function dashboardFor(role) {
  return role === "Admin" ? "admin-dashboard.html" : "dashboard.html";
}

/**
 * Put this at the top of every protected page.
 * requiredRole: "Admin" to make the page admin-only, or leave empty for any user.
 */
function requireLogin(requiredRole) {
  if (!isLoggedIn()) {
    window.location.href = "login.html";
    return null;
  }

  const user = getUser();

  if (requiredRole && user.role !== requiredRole) {
    // A student who types the admin URL by hand is sent back to their own page.
    window.location.href = dashboardFor(user.role);
    return null;
  }

  // The admin has a different set of pages, so keep them out of the user pages.
  if (!requiredRole && user.role === "Admin") {
    window.location.href = "admin-dashboard.html";
    return null;
  }

  return user;
}
