/* =========================================================================
   motion.js - the small animations of the interface.

   Two effects only, both optional:
     1. countUp()   - the dashboard numbers count from 0 up to their value.
     2. revealOnScroll() - cards fade and slide in as you scroll to them.

   Important rule: the page must be readable even if these never run.
   Nothing here HIDES content - an element is only hidden for the short moment
   after JavaScript has decided it is going to animate it. If JavaScript is
   switched off, or the visitor prefers less motion, everything is simply
   visible straight away.
   ========================================================================= */

/** True when the visitor asked their system for less animation. */
function prefersReducedMotion() {
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

/* ------------------------------------------------------------------ counters */

/**
 * Counts an element's number up from zero.
 * The element must already contain the final number as its text.
 *
 * @param {HTMLElement} element
 * @param {number} duration  milliseconds
 */
function countUp(element, duration = 900) {
  const text = element.textContent.trim();
  const target = Number(text);

  // Not a plain number (for example a resource name, or "2 / 8") - leave it alone.
  if (!Number.isFinite(target)) return;

  // "6.5" must not be rounded to "7" on the way, so keep the same number
  // of decimal places the real value has.
  const decimals = text.includes(".") ? text.split(".")[1].length : 0;

  if (prefersReducedMotion() || target === 0) {
    element.textContent = text;
    return;
  }

  const start = performance.now();

  function frame(now) {
    const progress = Math.min((now - start) / duration, 1);

    // "ease-out": fast at the beginning, gentle at the end.
    const eased = 1 - Math.pow(1 - progress, 3);

    element.textContent = (target * eased).toFixed(decimals);

    if (progress < 1) requestAnimationFrame(frame);
    else element.textContent = text;   // always land exactly on the real value
  }

  requestAnimationFrame(frame);
}

/** Animates every .stat .value inside a container. */
function animateStats(containerId) {
  const container = document.getElementById(containerId);
  if (!container) return;

  container.querySelectorAll(".stat .value").forEach((value, index) => {
    // A small stagger so the four cards do not all tick at once.
    setTimeout(() => countUp(value), index * 80);
  });
}

/* -------------------------------------------------------------- scroll reveal */

/**
 * Makes the given elements fade and slide in when they scroll into view.
 *
 * The elements are visible to begin with. This function is what decides to
 * hide them for a moment, so a page without JavaScript still shows everything.
 */
function revealOnScroll(selector, options = {}) {
  const elements = Array.from(document.querySelectorAll(selector));
  if (elements.length === 0) return;

  if (prefersReducedMotion() || !("IntersectionObserver" in window)) return;

  const stagger = options.stagger ?? 60;

  const observer = new IntersectionObserver((entries, obs) => {
    entries.forEach(entry => {
      if (!entry.isIntersecting) return;

      const index = Number(entry.target.dataset.revealIndex || 0);
      setTimeout(() => entry.target.classList.add("revealed"), index * stagger);

      obs.unobserve(entry.target);   // animate once, then leave it alone
    });
  }, { rootMargin: "0px 0px -40px 0px", threshold: 0.05 });

  elements.forEach((element, index) => {
    // Items already on screen animate together; later ones stagger from 0 again.
    const onScreen = element.getBoundingClientRect().top < window.innerHeight;
    element.dataset.revealIndex = onScreen ? index : 0;

    element.classList.add("reveal");
    observer.observe(element);
  });

  // Safety net: if anything is still hidden after a second, just show it.
  setTimeout(() => {
    elements.forEach(element => element.classList.add("revealed"));
  }, 1200);
}

/* ------------------------------------------------------------- page entrance */

/** Fades the main area in once, when the page opens. */
function playPageEntrance() {
  if (prefersReducedMotion()) return;
  document.querySelector(".main")?.classList.add("page-enter");
}
