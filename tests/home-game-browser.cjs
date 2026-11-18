// UI regression check. The API uses synthetic data, and runtime integration has its own tests.
const assert = require("node:assert/strict");
const path = require("node:path");
const os = require("node:os");
const fs = require("node:fs/promises");
const { homeGameFixture, piece, point } = require("../release-news/home-game-fixture.cjs");

function dependency(name) {
    try { return require(name); }
    catch { return require(path.join(os.homedir(), ".cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules", name)); }
}

const { chromium } = dependency("playwright");
const base = process.env.LUP_QA_URL ?? "http://127.0.0.1:5319";
const subject = "5d917801-9b74-4514-8d7b-13fca420e226";
const teacherSubject = "79a2b803-e11a-4ae8-b606-9b7f816e7c63";
const runId = "469f3838-456d-4201-ad66-7bd7a031af90";
const versionId = "667101e8-910b-4ba3-b759-2606c6b7d4f6";
const startedAtUtc = "2026-11-16T09:00:00Z";
const state = homeGameFixture({ lampInStorage: true });
const cells = state.layout.grid.rooms.living_room;
const program = "import home\nhome.set(\"desk_lamp\", True)\n";
const hostileLog = "<img src=x onerror=alert(1)>";
let undo, run;

function snapshot(tick) {
    return {
        tick, timeMs: tick * 100, avatarCell: tick ? point(6, 4) : point(0, 4), activity: tick ? "reading" : "idle",
        daylightLux: 40, ambientLux: 40, avatarLux: tick ? 378 : 40, comfort: tick ? 100 : null, energyMicroJ: tick * 600000,
        autoEnabled: true, poweredAliases: tick ? ["desk_lamp"] : [], sensors: [],
        lastCommands: tick ? [{ alias: "desk_lamp", action: "setPower", value: true }] : [],
        deviceCommits: tick ? [{ alias: "desk_lamp", action: "setPower", value: true, tick: 0, stateChanged: true }] : [],
        lightMap: cells.map(cell => ({ cell, lux: cell.x === 6 && cell.y === 4 && tick ? 378 : 40 })),
        message: tick ? "Avataren kan læse." : null, error: null
    };
}

async function handler(route) {
    const request = route.request();
    const url = new URL(request.url());
    const fromApi = url.pathname.startsWith("/api/");
    if (!fromApi) {
        if (url.origin === base || url.protocol === "data:" || url.protocol === "blob:") return route.continue();
        return route.fulfill({ status: 200, body: "" });
    }
    const headers = {
        "access-control-allow-origin": base,
        "access-control-allow-methods": "GET,POST,PUT,OPTIONS",
        "access-control-allow-headers": "*"
    };
    const send = (json, status = 200) => route.fulfill({ status, headers, json });
    if (request.method() === "OPTIONS") return route.fulfill({ status: 204, headers });
    if (!url.pathname.includes("/me/home-game")) {
        if (url.pathname.endsWith("/me/avatars")) return send({
            selectedAvatarId: state.selectedAvatarId, ownedAvatarIds: ["lupp", "bip01", "starter"], gold: state.gold, level: 1,
            ownedItemIds: [], canImportLegacy: false, revision: 1, unlockProgress: []
        });
        return send([]);
    }
    assert.equal(request.headers()["x-lup-apartment-subject"], state.isTeacherSandbox ? teacherSubject : subject);
    const endpoint = url.pathname.split("/me/home-game")[1];
    const method = request.method();
    const body = request.postDataJSON();
    if (endpoint === "" && method === "GET") return send(state);
    if (endpoint === "/build") {
        assert.equal(body.revision, state.layout.revision);
        const { action } = body;
        const previous = structuredClone({ items: state.layout.items, gold: state.gold });
        if (action.kind === "place") {
            state.layout.items.find(item => item.id === action.itemId).placement = action.placement;
        } else if (action.kind === "buy") {
            assert.equal(action.definitionId, "sofa_basic");
            const goldPaid = state.isTeacherSandbox ? 0 : 90;
            state.layout.items.push(piece("sofa", "sofa_basic", "sofa_basic", "Sofa", action.placement, { starter: false, goldPaid }));
            state.gold -= goldPaid;
        } else if (action.kind === "undo") {
            state.layout.items = undo.items;
            state.gold = undo.gold;
        } else throw new Error(`Unexpected build action ${action.kind}`);
        undo = action.kind === "undo" ? null : previous;
        state.canUndo = undo !== null;
        state.layout.revision++;
        if (run?.run.status === "paused") run.run.status = "cancelled";
        return send(state);
    }
    if (endpoint === "/program") {
        assert.equal(body.language, "python");
        assert.equal(body.source, program);
        assert.equal(body.revision, state.layout.revision);
        state.drafts = [{ language: "python", source: program, versionId, hash: "fixture" }];
        state.layout.revision++;
        return send(state);
    }
    if (endpoint === "/runs") {
        assert.equal(body.revision, state.layout.revision);
        assert.equal(Boolean(body.assessment), false);
        assert.equal(body.task ?? 0, 0);
        run = {
            run: {
                id: runId, revision: state.layout.revision, programVersionId: versionId, language: "python", task: 0,
                assessment: false, status: "running", startedAtUtc, programHash: "fixture", sdkVersion: "virtual-home-v1",
                simulatorVersion: "hjem-v1", contentVersion: "hjem-pilot-v1"
            },
            sequence: 1, scenarioIndex: 0, scenarioIds: ["free"], speed: body.speed, snapshot: snapshot(0),
            outcomes: [], logs: [hostileLog], goldAwarded: 0
        };
        state.lastRun = run.run;
        return send(run, 202);
    }
    if (endpoint === `/runs/${runId}/step`) {
        if (run.run.status !== "running" || body.afterSequence < run.sequence) return send(run);
        run.sequence++;
        const nextTick = Math.min(300, run.snapshot.tick + body.maxTicks);
        run.snapshot = snapshot(nextTick);
        if (nextTick >= 300) {
            run.run.status = "completed";
        }
        return send(run);
    }
    if (endpoint === `/runs/${runId}/control`) {
        run.run.status = body.action === "pause" ? "paused" : "running";
        run.sequence++;
        return send(run);
    }
    if (endpoint === `/runs/${runId}/replay`) return send({
        run,
        layout: state.layout,
        scenarios: [{
            id: "free", title: "Fri afprøvning", ticks: 300, free: true, seed: 0, version: 1,
            events: [{ tick: 0, daylightLux: 40, ambientLux: 40, intention: "read", autoEnabled: true }],
            windows: []
        }],
        events: [{ scenarioIndex: 0, tick: 0, commands: [{ alias: "desk_lamp", action: "setPower", value: true }],
            waitMs: null, controllerCompleted: true }],
        initialIntent: "read"
    });
    if (endpoint === `/runs/${runId}`) return send(run);
    throw new Error(`Unexpected endpoint ${endpoint}`);
}

async function device(browser, sub, role) {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, serviceWorkers: "block" });
    await context.route("**/*", handler);
    await context.addInitScript(({ sub, role }) => {
        const encode = value => btoa(JSON.stringify(value)).replace(/=/g, "").replace(/\+/g, "-").replace(/\//g, "_");
        const now = Math.floor(Date.now() / 1000);
        const token = `${encode({ alg: "HS256", typ: "JWT" })}.${encode({
            sub, name: "Hjem QA", role, iss: "local-test-only", iat: now, exp: now + 3600
        })}.bG9jYWwtdGVzdA`;
        sessionStorage.setItem("mt_access_token", token);
        sessionStorage.setItem("mt_expires_at", String(now + 3600));
        localStorage.setItem("lup-theme", "light");
    }, { sub, role });
    return context;
}

async function main() {
    const output = process.env.LUP_QA_OUTPUT;
    if (output) await fs.mkdir(output, { recursive: true });
    const browser = await chromium.launch({
        executablePath: process.env.LUP_QA_CHROME ?? "C:/Program Files/Google/Chrome/Application/chrome.exe",
        headless: true
    });
    try {
        const context = await device(browser, subject, "Student");
        const page = await context.newPage();
        page.setDefaultTimeout(60000);
        const errors = [];
        page.on("pageerror", error => errors.push(error.message));
        page.on("dialog", async dialog => { errors.push(dialog.message()); await dialog.dismiss(); });
        await page.goto(`${base}/apartment/smart-home`, { waitUntil: "domcontentloaded" });
        await page.getByRole("heading", { name: "Hjem.exe" }).waitFor();
        await page.getByRole("button", { name: "Indret", exact: true }).click();
        const board = page.locator(".home-board");
        await board.waitFor();
        const tile = (x, y) => board.locator(`[data-x="${x}"][data-y="${y}"]`);
        assert.equal(await board.getByRole("button").count(), 120);
        await board.locator('.resident[data-avatar-id="lupp"]').waitFor();
        assert.equal(await page.locator(".task-label").count(), 0);
        if (output) await page.screenshot({ path: path.join(output, "home-initial.png"), fullPage: true });
        await page.getByRole("listitem").filter({hasText:"Gulvlampe"}).getByRole("button",{name:"Placér",exact:true}).click();
        await tile(0, 4).focus();
        for (let i = 0; i < 5; i++) await page.keyboard.press("ArrowRight");
        await page.keyboard.press("Enter");
        await page.getByText("Indretningen er gemt.").waitFor();
        assert.deepEqual(state.layout.items.find(item => item.id === "lamp").placement.anchor, point(5, 4));
        assert.ok((await tile(6, 4).getAttribute("aria-label")).includes("Inden for rækkevidde"));
        await page.getByRole("button", { name: "Butik", exact: true }).click();
        await page.getByRole("listitem").filter({ hasText: "Sofa" }).getByRole("button").click();
        await tile(6, 3).hover();
        await page.getByText(/optager allerede feltet/i).waitFor();
        assert.equal(await page.getByRole("button", { name: "Placér", exact: true }).isDisabled(), true);
        assert.equal(state.gold, 100);
        await tile(1, 1).click();
        await page.getByText("Køb og placering gemt.").waitFor();
        assert.equal(state.gold, 10);
        await page.getByRole("button", { name: "Fortryd" }).click();
        await page.getByText("Seneste byggehandling fortrudt.").waitFor();
        assert.equal(state.gold, 100);

        await page.getByRole("button", { name: "Programmér", exact: true }).click();
        await page.getByLabel("Controllerens program").fill(program);
        await page.getByText("Gemt på LUP", { exact: true }).waitFor();
        await page.reload({ waitUntil: "domcontentloaded" });
        await page.getByRole("button", { name: "Programmér", exact: true }).click();
        assert.equal(await page.getByLabel("Controllerens program").inputValue(), program);
        await page.getByRole("button", { name: "Afprøv", exact: true }).click();
        await page.getByRole("button", { name: "Kør kode", exact: true }).click();
        await page.getByRole("button", { name: "Pause", exact: true }).click();
        await page.getByRole("button", { name: "Genoptag", exact: true }).waitFor();
        const pausedTick = run.snapshot.tick;
        await page.waitForTimeout(300);
        assert.equal(run.snapshot.tick, pausedTick);
        await page.getByRole("button", { name:"Genoptag", exact:true }).click();
        await page.getByText("Forsøg gennemført", { exact: true }).waitFor();
        assert.equal(run.run.status, "completed");
        assert.equal(state.gold, 100);
        await page.getByText("Log (1)", { exact: true }).click();
        assert.equal(await page.locator(".run-log").textContent(), hostileLog);
        assert.equal(await page.locator(".run-log img").count(), 0);
        assert.match(await page.getByTestId("reading-lux").innerText(), /378/);
        assert.ok((await tile(6, 4).getAttribute("aria-label")).includes("378 lux"));
        if (output) await page.screenshot({ path: path.join(output, "home-result.png"), fullPage: true });
        await page.getByRole("button", { name: "Genafspil forsøg" }).click();
        await page.getByRole("slider", { name: /^Tick/ }).fill("300");
        await page.getByText("Din avatar læser", { exact: true }).waitFor();
        assert.ok((await tile(6, 4).getAttribute("aria-label")).includes("378 lux"));
        await page.getByRole("button", { name: "Luk genafspilning" }).click();
        await page.setViewportSize({ width: 390, height: 844 });
        await page.getByRole("button", { name: "Rum", exact: true }).click();
        await board.waitFor({ state: "visible" });
        assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
        await page.getByRole("button", { name: "Værktøjer", exact: true }).click();
        await page.getByTestId("light-panel").waitFor({ state:"visible" });
        if (output) await page.screenshot({ path: path.join(output, "home-mobile.png"), fullPage: true });
        assert.deepEqual(errors, []);
        await context.close();

        Object.assign(state, homeGameFixture({ gold: 0, isTeacherSandbox: true, selectedAvatarId:"bip01" }));
        run = undo = null;
        const teacher = await device(browser, teacherSubject, "Teacher");
        const preview = await teacher.newPage();
        await preview.goto(`${base}/spil/hjem?vis=butik`, { waitUntil: "domcontentloaded" });
        await preview.getByText("Prøvebolig for undervisere", { exact: true }).waitFor();
        await preview.locator('.resident[data-avatar-id="bip01"]').waitFor();
        assert.equal(await preview.locator(".task-label").count(), 0);
        await preview.getByRole("listitem").filter({ hasText: "Sofa" }).getByRole("button", { name: "Tilføj gratis" }).click();
        await preview.locator('.home-board [data-x="1"][data-y="1"]').click();
        await preview.getByText("Prøveudstyret er placeret.", { exact: true }).waitFor();
        assert.equal(state.gold, 0);
        assert.equal(state.layout.items.find(item => item.id === "sofa").goldPaid, 0);
        await teacher.close();
        console.log("Hjem.exe browser: selected avatars, free simulation, keyboard placement, shared gold, mobile controls and teacher equipment passed.");
    } finally {
        await browser.close();
    }
}

main().catch(error => { console.error(error); process.exitCode = 1; });
