/* =========================================================================
   THE ONLY FILE YOU CHANGE WHEN YOU DEPLOY.
   -------------------------------------------------------------------------
   Local development : the API runs on your own machine.
   Production        : the API runs on Render, so put the Render URL here.
   ========================================================================= */

const CONFIG = {
  // Local backend (dotnet run)
  API_BASE_URL: "http://localhost:5080/api"

  // After deploying to Render, comment the line above and use this instead:
  // API_BASE_URL: "https://YOUR-APP-NAME.onrender.com/api"
};
