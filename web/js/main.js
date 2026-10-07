import { forlob } from "./forlob.js";
import { praktik, praktikHtml } from "./praktik.js";
import { tidslinje, tidslinjeMeta } from "./tidslinje.js";

const byShort = Object.fromEntries(forlob.map((f) => [f.short.toLowerCase(), f]));

const homeView = document.getElementById("home-view");
const detailView = document.getElementById("detail-view");
const praktikView = document.getElementById("praktik-view");
const tidslinjeView = document.getElementById("tidslinje-view");
const grid = document.getElementById("forlob-grid");
const detailCode = document.getElementById("detail-code");
const detailTitle = document.getElementById("detail-title");
const detailLead = document.getElementById("detail-lead");
const detailBody = document.getElementById("detail-body");
const praktikTitle = document.getElementById("praktik-title");
const praktikLead = document.getElementById("praktik-lead");
const praktikBody = document.getElementById("praktik-body");
const tidslinjeTitle = document.getElementById("tidslinje-title");
const tidslinjeLead = document.getElementById("tidslinje-lead");
const tidslinjeTrack = document.getElementById("tidslinje-track");
const navLinks = document.querySelectorAll("[data-nav]");

const escapeHtml = (value) =>
  String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");

const inlineMarkdown = (text) =>
  escapeHtml(text).replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>");

const renderMarkdown = (md) => {
  const lines = md.replace(/\r\n/g, "\n").split("\n");
  const html = [];
  let inList = false;

  const closeList = () => {
    if (inList) {
      html.push("</ul>");
      inList = false;
    }
  };

  for (const line of lines) {
    const trimmed = line.trim();
    if (!trimmed) {
      closeList();
      continue;
    }
    if (trimmed.startsWith("## ")) {
      closeList();
      html.push(`<h2>${inlineMarkdown(trimmed.slice(3))}</h2>`);
      continue;
    }
    if (trimmed.startsWith("- ")) {
      if (!inList) {
        html.push("<ul>");
        inList = true;
      }
      html.push(`<li>${inlineMarkdown(trimmed.slice(2))}</li>`);
      continue;
    }
    closeList();
    html.push(`<p>${inlineMarkdown(trimmed)}</p>`);
  }
  closeList();
  return html.join("\n");
};

const routeParts = () => {
  const raw = (location.hash || "#/").replace(/^#\/?/, "").toLowerCase();
  const [key = "home", stop = ""] = raw.split(/[/?#]/);
  return { key: key || "home", stop };
};

const hideAll = () => {
  homeView.hidden = true;
  detailView.hidden = true;
  praktikView.hidden = true;
  tidslinjeView.hidden = true;
};

const setActiveNav = (key) => {
  navLinks.forEach((link) => {
    link.classList.toggle("is-active", link.dataset.nav === key);
  });
};

const showHome = () => {
  hideAll();
  homeView.hidden = false;
  document.title = "LUP · Lokale undervisningsplaner";
  setActiveNav("home");
  window.scrollTo({ top: 0, behavior: "smooth" });
};

const showForlob = (key) => {
  const item = byShort[key];
  if (!item) {
    showHome();
    return;
  }

  hideAll();
  detailView.hidden = false;
  detailCode.textContent = item.short;
  detailTitle.textContent = item.titel;
  detailLead.textContent = item.kort;
  detailBody.innerHTML = renderMarkdown(item.markdown);
  document.title = `${item.short} · LUP`;
  setActiveNav(key);
  window.scrollTo({ top: 0, behavior: "smooth" });
};

const showPraktik = () => {
  hideAll();
  praktikView.hidden = false;
  praktikTitle.textContent = praktik.title;
  praktikLead.textContent = praktik.lead;
  praktikBody.innerHTML = praktikHtml;
  document.title = "Praktikmålsoversigt · LUP";
  setActiveNav("praktik");
  window.scrollTo({ top: 0, behavior: "smooth" });
};

const renderStop = (stop, index, activeId) => {
  const isActive = stop.id === activeId;
  const typeLabel = stop.type === "praktik" ? "Praktik" : "Skole";
  const topics =
    stop.topics?.length
      ? `<ul class="tl-topics">${stop.topics
          .map((t) => `<li>${escapeHtml(t)}</li>`)
          .join("")}</ul>`
      : "";

  const actions =
    stop.type === "skole"
      ? `<a class="cta cta-primary tl-cta" href="${escapeHtml(stop.href)}">Åbn ${escapeHtml(
          stop.label
        )} LUP →</a>`
      : `<a class="cta cta-ghost tl-cta" href="#/praktik">Hele praktikoversigten</a>`;

  const panel =
    stop.type === "praktik"
      ? `<div class="tl-panel">${stop.body}${actions}</div>`
      : `<div class="tl-panel">
          ${
            stop.image
              ? `<figure class="praktik-fig"><img src="assets/praktik/${escapeHtml(
                  stop.image
                )}" alt="${escapeHtml(stop.title)}" loading="lazy" /></figure>`
              : ""
          }
          ${topics}
          ${actions}
        </div>`;

  return `
    <li class="tl-item tl-${stop.type}${isActive ? " is-open" : ""}" data-stop="${escapeHtml(
      stop.id
    )}" style="--i:${index}">
      <button type="button" class="tl-node" aria-expanded="${isActive}" data-tl-toggle="${escapeHtml(
        stop.id
      )}">
        <span class="tl-dot" aria-hidden="true"></span>
        <span class="tl-meta">
          <span class="tl-kind">${typeLabel}</span>
          <span class="tl-label">${escapeHtml(stop.label)}</span>
        </span>
        <span class="tl-copy">
          <span class="tl-title">${escapeHtml(stop.title)}</span>
          <span class="tl-summary">${escapeHtml(stop.summary)}</span>
        </span>
        <span class="tl-chevron" aria-hidden="true"></span>
      </button>
      ${panel}
    </li>`;
};

const bindTidslinje = () => {
  tidslinjeTrack.querySelectorAll("[data-tl-toggle]").forEach((btn) => {
    btn.addEventListener("click", () => {
      const id = btn.dataset.tlToggle;
      const next = location.hash === `#/tidslinje/${id}` ? "#/tidslinje" : `#/tidslinje/${id}`;
      location.hash = next;
    });
  });
};

const showTidslinje = (stopId = "") => {
  hideAll();
  tidslinjeView.hidden = false;
  tidslinjeTitle.textContent = tidslinjeMeta.title;
  tidslinjeLead.textContent = tidslinjeMeta.lead;
  const activeId = tidslinje.some((s) => s.id === stopId) ? stopId : tidslinje[0].id;
  tidslinjeTrack.innerHTML = tidslinje.map((s, i) => renderStop(s, i, activeId)).join("");
  bindTidslinje();
  document.title = "Tidslinje · LUP";
  setActiveNav("tidslinje");

  const activeEl = tidslinjeTrack.querySelector(`[data-stop="${activeId}"]`);
  if (activeEl && stopId) {
    activeEl.scrollIntoView({ behavior: "smooth", block: "center" });
  } else {
    window.scrollTo({ top: 0, behavior: "smooth" });
  }
};

const render = () => {
  const { key, stop } = routeParts();
  if (key === "home" || key === "") {
    showHome();
    return;
  }
  if (key === "praktik") {
    showPraktik();
    return;
  }
  if (key === "tidslinje") {
    showTidslinje(stop);
    return;
  }
  showForlob(key);
};

const buildGrid = () => {
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const cards = forlob
    .map(
      (item, index) => `
      <a class="card" href="#/${item.short.toLowerCase()}" role="listitem" style="${
        reduceMotion ? "" : `animation: rise 0.55s ${0.15 + index * 0.06}s ease both;`
      }">
        <p class="card-code">${escapeHtml(item.short)}</p>
        <h2 class="card-title">${escapeHtml(item.titel)}</h2>
        <p class="card-desc">${escapeHtml(item.kort)}</p>
        <span class="card-go">Åbn LUP →</span>
      </a>`
    )
    .join("");

  const extra = `
    <a class="card card-accent" href="#/tidslinje" role="listitem" style="${
      reduceMotion ? "" : `animation: rise 0.55s ${0.15 + forlob.length * 0.06}s ease both;`
    }">
      <p class="card-code">Tidslinje</p>
      <h2 class="card-title">Inden H1 → H6</h2>
      <p class="card-desc">Skoleperioder og praktikmål i den rækkefølge, eleven møder dem.</p>
      <span class="card-go">Åbn tidslinje →</span>
    </a>
    <a class="card" href="#/praktik" role="listitem" style="${
      reduceMotion ? "" : `animation: rise 0.55s ${0.15 + (forlob.length + 1) * 0.06}s ease both;`
    }">
      <p class="card-code">Praktik</p>
      <h2 class="card-title">Praktikmålsoversigt</h2>
      <p class="card-desc">Samlet oversigt med alle billeder og delpraktikmål.</p>
      <span class="card-go">Åbn oversigt →</span>
    </a>`;

  grid.innerHTML = cards + extra;
};

buildGrid();
window.addEventListener("hashchange", render);

if (!location.hash || location.hash === "#") {
  location.replace("#/");
}

render();
