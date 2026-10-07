import { forlob } from "./forlob.js";
import { praktik, praktikHtml } from "./praktik.js";

const byShort = Object.fromEntries(forlob.map((f) => [f.short.toLowerCase(), f]));

const homeView = document.getElementById("home-view");
const detailView = document.getElementById("detail-view");
const praktikView = document.getElementById("praktik-view");
const grid = document.getElementById("forlob-grid");
const detailCode = document.getElementById("detail-code");
const detailTitle = document.getElementById("detail-title");
const detailLead = document.getElementById("detail-lead");
const detailBody = document.getElementById("detail-body");
const praktikTitle = document.getElementById("praktik-title");
const praktikLead = document.getElementById("praktik-lead");
const praktikBody = document.getElementById("praktik-body");
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

const hideAll = () => {
  homeView.hidden = true;
  detailView.hidden = true;
  praktikView.hidden = true;
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

const render = () => {
  const key = routeKey();
  if (key === "home" || key === "") {
    showHome();
    return;
  }
  if (key === "praktik") {
    showPraktik();
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

  const praktikCard = `
    <a class="card card-accent" href="#/praktik" role="listitem" style="${
      reduceMotion ? "" : `animation: rise 0.55s ${0.15 + forlob.length * 0.06}s ease both;`
    }">
      <p class="card-code">Praktik</p>
      <h2 class="card-title">Praktikmålsoversigt</h2>
      <p class="card-desc">Mål og forventninger mellem skoleopholdene — med oversigtsbilleder for GF2–H6.</p>
      <span class="card-go">Åbn oversigt →</span>
    </a>`;

  grid.innerHTML = cards + praktikCard;
};

buildGrid();
window.addEventListener("hashchange", render);

if (!location.hash || location.hash === "#") {
  location.replace("#/");
}

render();
