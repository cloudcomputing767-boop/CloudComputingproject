/* =========================================================================
   THE ONLY FILE YOU CHANGE WHEN YOU DEPLOY.
   -------------------------------------------------------------------------
   The website has to reach the API at two different addresses:

     - while you develop, the API runs on your own machine
     - once deployed, the API runs on Render

   Instead of editing this file every time you switch, the page simply looks
   at its own address: if it is being served from localhost, it talks to the
   local API; otherwise it talks to the one in the cloud.
   ========================================================================= */

/** The API deployed on Render. Change this if you redeploy under a new name. */
const PRODUCTION_API_URL = "https://cloudcomputingproject-98vu.onrender.com/api";

/** The API running on your own machine (dotnet run). */
const LOCAL_API_URL = "http://localhost:5080/api";

/** True when the page itself is opened from your own machine. */
const isLocalMachine =
  location.hostname === "localhost" || location.hostname === "127.0.0.1";

const CONFIG = {
  API_BASE_URL: isLocalMachine ? LOCAL_API_URL : PRODUCTION_API_URL
};
