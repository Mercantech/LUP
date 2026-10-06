import { forlob } from "./forlob.js";

const byShort = Object.fromEntries(forlob.map((f) => [f.short.toLowerCase(), f]));

const homeView = document.getElementById("home-view");
const detailView = document.getElementById("detail-view");
const grid = document.getElementById("forlob-grid");
const detailCode = document.getElementById("detail-code");
const detailTitle = document.getElementById("detail-title");
const detailLead = document.getElementById("detail-lead");
const detailBody = document.getElementById("detail-body");
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

const routeKey = () => {
  const raw = (location.hash || "#/").replace(/^#\/?/, "").toLowerCase();
  return raw.split(/[/?#]/)[0] || "home";
};

const setActiveNav = (key) => {
  navLinks.forEach((link) => {
    link.classList.toggle("is-active", link.dataset.nav === key);
  });
};

const showHome = () => {
  homeView.hidden = false;
  detailView.hidden = true;
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

  homeView.hidden = true;
  detailView.hidden = false;
  detailCode.textContent = item.short;
  detailTitle.textContent = item.titel;
  detailLead.textContent = item.kort;
  detailBody.innerHTML = renderMarkdown(item.markdown);
  document.title = `${item.short} · LUP`;
  setActiveNav(key);
  window.scrollTo({ top: 0, behavior: "smooth" });
};

const render = () => {
  const key = routeKey();
  if (key === "home" || key === "") {
    showHome();
    return;
  }
  showForlob(key);
};

const buildGrid = () => {
  const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  grid.innerHTML = forlob
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
};

buildGrid();
window.addEventListener("hashchange", render);

if (!location.hash || location.hash === "#") {
  location.replace("#/");
}

render();
