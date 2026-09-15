/* =========================================================================
   api.js - one small wrapper around fetch().
   Every page uses these helpers instead of calling fetch() directly, so the
   token header and the error handling are written only once.
   ========================================================================= */

const TOKEN_KEY = "rb_token";
const USER_KEY  = "rb_user";

/**
 * Sends a request to the API.
 * Automatically attaches the JWT token if the user is logged in.
 * Throws an Error with a friendly message when the API answers with an error.
 */
async function apiRequest(path, { method = "GET", body = null, auth = true } = {}) {
  const headers = { "Content-Type": "application/json" };

  const token = localStorage.getItem(TOKEN_KEY);
  if (auth && token) {
    // This is how the API knows who we are.
    headers["Authorization"] = "Bearer " + token;
  }

  let response;
  try {
    response = await fetch(CONFIG.API_BASE_URL + path, {
      method,
      headers,
      body: body ? JSON.stringify(body) : null
    });
  } catch (networkError) {
    throw new Error("Cannot reach the server. Make sure the backend API is running.");
  }

  // The token expired or is missing -> send the user back to the login page.
  if (response.status === 401 && auth) {
    clearSession();
    window.location.href = "login.html";
    throw new Error("Your session has expired. Please log in again.");
  }

  if (response.status === 403) {
    throw new Error("You are not authorized to perform this action.");
  }

  if (response.status === 204) return null;

  const text = await response.text();
  const data = text ? JSON.parse(text) : null;

  if (!response.ok) {
    throw new Error((data && data.message) || "Something went wrong. Please try again.");
  }

  return data;
}

/**
 * Sends a FILE to the API.
 * A file cannot travel as JSON, so we use FormData and let the browser choose
 * the Content-Type header itself (it has to add a special boundary marker).
 */
async function apiUpload(path, file) {
  const token = localStorage.getItem(TOKEN_KEY);
  const body = new FormData();
  body.append("file", file);

  let response;
  try {
    response = await fetch(CONFIG.API_BASE_URL + path, {
      method: "POST",
      headers: token ? { Authorization: "Bearer " + token } : {},
      body
    });
  } catch {
    throw new Error("Cannot reach the server. Make sure the backend API is running.");
  }

  if (response.status === 401) { clearSession(); window.location.href = "login.html"; throw new Error("Your session has expired."); }
  if (response.status === 403) throw new Error("You are not authorized to upload images.");
  if (response.status === 413) throw new Error("The image is too large. Please choose a file smaller than 2 MB.");

  const text = await response.text();
  const data = text ? JSON.parse(text) : null;

  if (!response.ok) throw new Error((data && data.message) || "The image could not be uploaded.");
  return data;
}

/** The address of the API without the trailing "/api" - used to build image links. */
function apiOrigin() {
  return CONFIG.API_BASE_URL.replace(/\/api\/?$/, "");
}

const api = {
  get:  (path)       => apiRequest(path),
  post: (path, body) => apiRequest(path, { method: "POST", body }),
  put:  (path, body) => apiRequest(path, { method: "PUT",  body }),
  del:  (path)       => apiRequest(path, { method: "DELETE" }),

  // Login and register are the only calls made without a token.
  login:    (body) => apiRequest("/auth/login",    { method: "POST", body, auth: false }),
  register: (body) => apiRequest("/auth/register", { method: "POST", body, auth: false }),

  uploadImage: (file) => apiUpload("/images", file)
};
