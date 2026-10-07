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
const tidslinjeRail = document.getElementById("tidslinje-rail");
const navLinks = document.querySelectorAll("[data-nav]");

let railActiveId = "";
let railRaf = 0;

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
  document.body.classList.remove("has-tl-rail");
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

const renderStop = (stop, index) => {
  const typeLabel = stop.type === "praktik" ? "Praktik" : "Skole";
  const lup = stop.type === "skole" ? byShort[stop.id] || byShort[stop.label?.toLowerCase()] : null;

  const title = lup?.titel || stop.title;
  const summary = lup?.kort || stop.summary;

  let panel;
  if (stop.type === "praktik") {
    panel = `<div class="tl-panel">${stop.body}</div>`;
  } else {
    const image = stop.image
      ? `<figure class="praktik-fig"><img src="assets/praktik/${escapeHtml(
          stop.image
        )}" alt="${escapeHtml(title)}" loading="lazy" /></figure>`
      : "";
    const body = lup
      ? `<div class="prose tl-lup">${renderMarkdown(lup.markdown)}</div>`
      : `<p class="tl-summary">${escapeHtml(stop.summary)}</p>`;
    panel = `<div class="tl-panel">${image}${body}</div>`;
  }

  return `
    <li class="tl-item tl-${stop.type}" id="stop-${escapeHtml(stop.id)}" style="--i:${index}">
      <header class="tl-node">
        <span class="tl-dot" aria-hidden="true"></span>
        <span class="tl-meta">
          <span class="tl-kind">${typeLabel}</span>
          <span class="tl-label">${escapeHtml(stop.label)}</span>
        </span>
        <span class="tl-copy">
          <h2 class="tl-title">${escapeHtml(title)}</h2>
          <p class="tl-summary">${escapeHtml(summary)}</p>
        </span>
      </header>
      ${panel}
    </li>`;
};

const renderRail = () =>
  tidslinje
    .map((stop) => {
      const kind = stop.type === "praktik" ? "Praktik" : "Skole";
      return `
        <a
          class="tl-rail-link tl-rail-${stop.type}"
          href="#/tidslinje/${escapeHtml(stop.id)}"
          data-stop="${escapeHtml(stop.id)}"
          title="${escapeHtml(kind)} · ${escapeHtml(stop.label)}"
        >
          <span class="tl-rail-dot" aria-hidden="true"></span>
          <span class="tl-rail-text">
            <span class="tl-rail-kind">${kind}</span>
            <span class="tl-rail-label">${escapeHtml(stop.label)}</span>
          </span>
        </a>`;
    })
    .join("");

const syncRail = () => {
  if (tidslinjeView.hidden || !tidslinjeRail) return;

  const marker = window.scrollY + Math.min(180, window.innerHeight * 0.22);
  let active = tidslinje[0]?.id || "";

  for (const stop of tidslinje) {
    const el = document.getElementById(`stop-${stop.id}`);
    if (!el) continue;
    const top = el.getBoundingClientRect().top + window.scrollY;
    if (top <= marker) active = stop.id;
  }

  if (active === railActiveId) return;
  railActiveId = active;

  tidslinjeRail.querySelectorAll("[data-stop]").forEach((link) => {
    const on = link.dataset.stop === active;
    link.classList.toggle("is-active", on);
    if (on) link.setAttribute("aria-current", "true");
    else link.removeAttribute("aria-current");
  });

  const activeLink = tidslinjeRail.querySelector(`[data-stop="${active}"]`);
  activeLink?.scrollIntoView({ block: "nearest", inline: "nearest" });
};

const onRailScroll = () => {
  if (railRaf) return;
  railRaf = requestAnimationFrame(() => {
    railRaf = 0;
    syncRail();
  });
};

const showTidslinje = (stopId = "") => {
  hideAll();
  tidslinjeView.hidden = false;
  tidslinjeTitle.textContent = tidslinjeMeta.title;
  tidslinjeLead.textContent = tidslinjeMeta.lead;
  tidslinjeTrack.innerHTML = tidslinje.map((s, i) => renderStop(s, i)).join("");
  tidslinjeRail.innerHTML = renderRail();
  railActiveId = "";
  document.title = "Tidslinje · LUP";
  setActiveNav("tidslinje");
  document.body.classList.add("has-tl-rail");

  const target = stopId ? document.getElementById(`stop-${stopId}`) : null;
  if (target) {
    target.scrollIntoView({ behavior: "smooth", block: "start" });
  } else {
    window.scrollTo({ top: 0, behavior: "smooth" });
  }

  requestAnimationFrame(syncRail);
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
window.addEventListener("scroll", onRailScroll, { passive: true });
window.addEventListener("resize", onRailScroll, { passive: true });

if (!location.hash || location.hash === "#") {
  location.replace("#/");
}

render();
