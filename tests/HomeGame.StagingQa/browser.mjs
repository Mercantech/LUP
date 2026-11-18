// Runs only against the private synthetic Compose project. Every /api response is real.
import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import { chromium } from 'playwright';

if (process.env.LUP_HOME_STAGING_QA !== 'synthetic-only-20261118' ||
    process.env.HOME_QA_ORIGIN !== 'http://web:3000')
    throw new Error('Use the isolated Hjem staging Compose project.');
const origin = process.env.HOME_QA_ORIGIN;
const issuerOrigin = 'https://home-game-qa-issuer:9443';
const report = { syntheticOnly: true, apiResponsesMocked: false, startedAt: new Date().toISOString(), checks: [], requests: [] };
const browserErrors = [];
await mkdir('/artifacts', { recursive: true });
let browser;
let phase = 'startup';
const check = (name, details = {}) => {
    report.checks.push({ name, ...details });
    console.log(JSON.stringify({ check: name, ...details }));
};
async function waitUntil(action, timeout = 180000) {
    const end = Date.now() + timeout;
    let last;
    while (Date.now() < end) {
        try { const value = await action(); if (value) return value; }
        catch (error) { last = error; }
        await new Promise(resolve => setTimeout(resolve, 200));
    }
    throw last ?? new Error('Staging startup or condition timed out.');
}
const bundle = await waitUntil(async () => {
    const response = await fetch(issuerOrigin + '/_qa/identities');
    if (!response.ok) return null;
    const value = await response.json();
    assert.equal(value.fixture, 'hjem-staging-synthetic-only');
    return value;
});
const identities = Object.fromEntries(bundle.identities.map(identity => [identity.key, identity]));

async function api(identity, path, method = 'GET', body, expected) {
    assert.ok(path.startsWith('/api/'));
    const headers = { authorization: 'Bearer ' + identity.token, 'x-lup-apartment-subject': identity.sub };
    if (body !== undefined) headers['content-type'] = 'application/json';
    const response = await fetch(origin + path, { method, headers,
        body: body === undefined ? undefined : JSON.stringify(body) });
    const result = { status: response.status, body: null };
    const text = await response.text();
    if (text) { try { result.body = JSON.parse(text); } catch { result.body = text.slice(0, 300); } }
    report.requests.push({ identity: identity.key, path, method, status: response.status });
    if (expected !== undefined) assert.equal(response.status, expected, method + ' ' + path + ' ' + JSON.stringify(result.body));
    else assert.ok(response.ok, method + ' ' + path + ' ' + response.status + ' ' + JSON.stringify(result.body));
    return result.body;
}
async function contextFor(identity) {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 }, serviceWorkers: 'block' });
    context.on('page', page => page.on('pageerror', error => browserErrors.push(error.message)));
    // Block external resources without replacing any API or application response.
    await context.route('**/*', route => {
        const requestUrl = new URL(route.request().url());
        if (requestUrl.origin === origin || ['data:', 'blob:'].includes(requestUrl.protocol))
            return route.continue();
        return route.abort('blockedbyclient');
    });
    await context.addInitScript(({ user, expectedOrigin }) => {
        if (location.origin !== expectedOrigin) return;
        sessionStorage.setItem('mt_access_token', user.token);
        sessionStorage.setItem('mt_refresh_token', '');
        sessionStorage.setItem('mt_expires_at', String(user.expiresAt));
        localStorage.setItem('lup-theme', 'light');
    }, { user: identity, expectedOrigin: origin });
    return context;
}
const build = (identity, home, action, operationId = crypto.randomUUID(), expected) =>
    api(identity, '/api/me/home-game/build', 'POST', { operationId, revision: home.layout.revision, action }, expected);
const homeFor = identity => api(identity, '/api/me/home-game');
const avatarFor = identity => api(identity, '/api/me/avatars');
const installed = (home, definition) => home.layout.items.find(item => item.definitionId === definition);
async function finishFreeRun(identity, initial) {
    let run = initial;
    const end = Date.now() + 180000;
    while (['queued', 'compiling', 'running'].includes(run.run.status)) {
        assert.ok(Date.now() < end, 'Free run timed out for ' + run.run.language);
        if (run.run.status === 'running') {
            run = await api(identity, '/api/me/home-game/runs/' + run.run.id + '/step', 'POST',
                { afterSequence: run.sequence, maxTicks: 20 });
        } else {
            await new Promise(resolve => setTimeout(resolve, 150));
            run = await api(identity, '/api/me/home-game/runs/' + run.run.id);
        }
    }
    assert.equal(run.run.status, 'completed', JSON.stringify({ language: run.run.language, error: run.errorMessage }));
    assert.equal(run.run.task, 0);
    assert.equal(run.run.assessment, false);
    assert.equal(run.goldAwarded, 0);
    assert.ok(run.snapshot.poweredAliases.includes('desk_lamp'));
    const reading = run.snapshot.lightMap.find(v => v.cell.x === 6 && v.cell.y === 4);
    assert.ok(reading && reading.lux > run.snapshot.ambientLux);
    return run;
}

async function checkAvatar(page, id) {
    const resident = page.locator('.resident[data-avatar-id="' + id + '"]');
    await resident.waitFor({ state: 'visible' });
    if (id === 'starter') {
        await resident.locator('.v2-sprite').waitFor({ state: 'visible' });
        return;
    }
    const sheet = resident.locator('.avatar-sheet[data-avatar="' + id + '"]');
    const portrait = resident.locator('img');
    await waitUntil(async () => {
        if (await sheet.count()) {
            const loaded = await sheet.evaluate(async elem => {
                const imageUrl = getComputedStyle(elem).backgroundImage.match(/url\(["']?(.*?)["']?\)/)?.[1];
                if (!imageUrl) return false;
                const img = new Image(); img.src = imageUrl; await img.decode();
                return img.naturalWidth > 0 && elem.getBoundingClientRect().width > 0;
            });
            return loaded;
        }
        if (await portrait.count())
            return portrait.first().evaluate(img => img.complete && img.naturalWidth > 0);
        return false;
    }, 15000);
}

try {
    await waitUntil(async () => {
        const response = await fetch(origin + '/api/health');
        return response.ok;
    });
    const unauthorized = await fetch(origin + '/api/me/home-game');
    assert.equal(unauthorized.status, 401);
    const release = await (await fetch(origin + '/release.json')).json();
    report.release = release;
    const apiRelease = await (await fetch(origin + '/api/platform-updates/deployment')).json();
    assert.equal(apiRelease.revision, release.revision, 'Web and API release fingerprints must match.');
    report.apiRevision = apiRelease.revision;
    if (process.env.HOME_QA_EXPECTED_REVISION)
        assert.equal(release.revision, process.env.HOME_QA_EXPECTED_REVISION);
    check('Exact web and API images started with signed staging identity');
    assert.deepEqual(await api(identities.teacher, '/api/forloeb'), [], 'Fresh staging DB must contain no courses.');

    phase = 'synthetic-hold-and-quest';
    const created = await api(identities.teacher, '/api/forloeb', 'POST',
        { name: 'QA Hjem isoleret testhold', description: 'Synthetic staging-only acceptance data.' });
    const holdId = created.hold.id;
    for (const identity of [identities.pupil, identities.peer])
        await api(identities.teacher, '/api/holds/' + holdId + '/memberships', 'POST',
            { userSub: identity.sub, displayName: identity.name, source: 'manual' });
    const quest = await api(identities.teacher, '/api/quests', 'POST', {
        holdId, title: 'QA Guld fra rigtig aflevering', description: 'Aflevér en kort forklaring til guldtesten.',
        submissionMode: 'individual', published: true, deadlineUtc: null
    });
    phase = 'legal-gold-spending';
    let pupilHome = await homeFor(identities.pupil);
    assert.equal(pupilHome.gold, 320);
    assert.equal((await avatarFor(identities.pupil)).gold, 320);
    assert.equal(pupilHome.isTeacherSandbox, false);
    assert.equal(pupilHome.selectedAvatarId, 'starter');
    assert.ok(installed(pupilHome, 'lamp_basic').placement);
    await api(identities.pupil, '/api/me/avatars/bip01/unlock', 'POST', {});
    pupilHome = await homeFor(identities.pupil);
    assert.equal(pupilHome.selectedAvatarId, 'bip01');
    assert.equal(pupilHome.gold, 170);
    const positions = [[0,0],[2,0],[4,0],[6,0],[8,0]];
    for (const [x, y] of positions)
        pupilHome = await build(identities.pupil, pupilHome, { kind: 'buy', definitionId: 'rug_basic',
            placement: { anchor: { x, y }, rotation: 0 } });
    assert.equal(pupilHome.gold, 20);
    const purchase = { kind: 'buy', definitionId: 'rug_basic', placement: { anchor: { x: 10, y: 2 }, rotation: 0 } };
    await build(identities.pupil, pupilHome, purchase, crypto.randomUUID(), 422);
    assert.equal((await homeFor(identities.pupil)).gold, 20);
    check('A student equipped BIP-01 and bought five rugs; a purchase exceeding their 20 gold was rejected');

    phase = 'student-browser-submission';
    browser = await chromium.launch({ headless: true });
    const pupilContext = await contextFor(identities.pupil);
    const teacherContext = await contextFor(identities.teacher);
    const pupilPage = await pupilContext.newPage();
    const teacherPage = await teacherContext.newPage();
    pupilPage.setDefaultTimeout(60000);
    teacherPage.setDefaultTimeout(60000);
    await pupilPage.goto(origin + '/quests/' + quest.id + '?hold=' + holdId, { waitUntil: 'domcontentloaded' });
    await pupilPage.getByRole('button', { name: 'Start aflevering', exact: true }).click();
    await pupilPage.getByLabel('Tekst til afleveringen', { exact: true }).fill('QA: Jeg har testet at sensorens måling og lampens kode hænger sammen.');
    await pupilPage.getByRole('button', { name: 'Aflevér', exact: true }).click();
    await pupilPage.getByText(/Version 1 er gemt/).waitFor();
    const submissions = await api(identities.pupil, '/api/submissions?questId=' + quest.id);
    assert.equal(submissions.length, 1);
    let submission = submissions[0];
    const submissionId = submission.id;
    phase = 'teacher-browser-approval';
    await teacherPage.goto(origin + '/quests/' + quest.id + '?hold=' + holdId + '&submission=' + submissionId,
        { waitUntil: 'domcontentloaded' });
    await teacherPage.getByRole('combobox', { name: /^Vurdering/ }).selectOption('approved');
    await teacherPage.getByRole('button', { name: 'Gem feedback', exact: true }).click();
    await waitUntil(async () => (await homeFor(identities.pupil)).gold === 40);
    submission = await api(identities.teacher, '/api/submissions/' + submissionId);
    assert.equal(submission.state, 'approved');
    const revision = submission.revisions.at(-1);
    await api(identities.teacher, '/api/submissions/' + submissionId + '/feedback', 'POST',
        { revisionId: revision.id, version: submission.version, text: 'QA genforsøg', decision: 'approved',
            fileAssetId: null, attachmentIds: [] });
    assert.equal((await homeFor(identities.pupil)).gold, 40);
    check('Actual student UI submission and teacher UI approval credited 20 gold once', { questId: quest.id, submissionId });
    phase = 'browser-quest-gold-purchase';
    await pupilPage.goto(origin + '/spil/hjem?vis=butik', { waitUntil: 'domcontentloaded' });
    await pupilPage.locator('.gold-balance').getByText('40', { exact: true }).waitFor();
    await pupilPage.getByRole('listitem').filter({ hasText: 'Tæppe' })
        .getByRole('button', { name: '30 guld', exact: true }).click();
    await pupilPage.locator('.home-board [data-x="10"][data-y="2"]').click();
    await pupilPage.getByText('Køb og placering gemt.', { exact: true }).waitFor();
    assert.equal((await homeFor(identities.pupil)).gold, 10);
    assert.equal((await avatarFor(identities.pupil)).gold, 10);
    await pupilPage.reload({ waitUntil: 'domcontentloaded' });
    await pupilPage.locator('.gold-balance').getByText('10', { exact: true }).waitFor();
    await pupilPage.screenshot({ path: '/artifacts/quest-gold-purchase.png', fullPage: true });
    check('Quest gold spent through real Hjem UI; persistent Home and Avatar balances both 10');

    phase = 'student-free-workshop';
    await checkAvatar(pupilPage, 'bip01');
    await api(identities.pupil, '/api/me/avatars/selection', 'PUT', { avatarId: 'starter' });
    await pupilPage.reload({ waitUntil: 'domcontentloaded' });
    await checkAvatar(pupilPage, 'starter');
    await api(identities.pupil, '/api/me/avatars/selection', 'PUT', { avatarId: 'bip01' });
    await pupilPage.reload({ waitUntil: 'domcontentloaded' });
    await checkAvatar(pupilPage, 'bip01');

    await pupilPage.getByRole('button', { name: 'Programmér', exact: true }).click();
    await pupilPage.getByText('Kom i gang', { exact: true }).first().click();
    await pupilPage.getByRole('button', { name: 'Brug lampeeksempel', exact: true }).click();
    const guideProgram = await pupilPage.getByLabel('Controllerens program', { exact: true }).inputValue();
    assert.ok(guideProgram.includes('desk_lamp') && guideProgram.includes('True'));
    const creation = pupilPage.waitForResponse(r => new URL(r.url()).pathname === '/api/me/home-game/runs' && r.request().method() === 'POST');
    await pupilPage.getByRole('button', { name: 'Kør kode', exact: true }).click();
    const started = await creation;
    assert.equal(started.status(), 202);
    const pupilRun = await started.json();
    assert.equal(pupilRun.run.isTeacherSandbox, false);
    assert.ok((await homeFor(identities.pupil)).drafts.some(d => d.language === 'python' && d.source === guideProgram));
    await waitUntil(async () => {
        const current = await api(identities.pupil, '/api/me/home-game/runs/' + pupilRun.run.id);
        if (['error','cancelled','expired'].includes(current.run.status)) throw new Error(current.run.status);
        return current.snapshot?.poweredAliases.includes('desk_lamp');
    });
    await pupilPage.getByRole('button', { name: 'Afprøv', exact: true }).click();
    const status = await finishFreeRun(identities.pupil, pupilRun);
    const expectedLux = status.snapshot.lightMap.find(v => v.cell.x === 6 && v.cell.y === 4).lux;
    const panel = pupilPage.getByTestId('light-panel');
    await panel.waitFor({ state: 'visible' });
    await waitUntil(async () => {
        const reading = await pupilPage.getByTestId('reading-lux').innerText();
        const probe = await pupilPage.locator('.reading-probe').getAttribute('data-lux');
        return Number(probe) === expectedLux && reading.includes(String(expectedLux));
    }, 20000);
    assert.doesNotMatch(await pupilPage.locator('.home-workshop').innerText(), /Vurdér opgave|Opgave bestået|Næste opgave|P0[1-6]\b/i);
    assert.equal((await homeFor(identities.pupil)).gold, 10);
    await pupilPage.screenshot({ path: '/artifacts/workshop-student.png', fullPage: true });
    check('Student UI ran real Python; their BIP-01, room probe and light panel agree with the worker',
        { lux: expectedLux, runId: status.run.id, balance: 10 });

    phase = 'free-languages';
    for (const language of ['csharp','arduino']) {
        await pupilPage.getByRole('button', { name: 'Programmér', exact: true }).click();
        await pupilPage.locator('.code-controls select').filter({ has: pupilPage.locator('option[value="csharp"]') }).selectOption(language);
        const next = pupilPage.waitForResponse(r => new URL(r.url()).pathname === '/api/me/home-game/runs' && r.request().method() === 'POST');
        await pupilPage.getByRole('button', { name:'Kør kode', exact:true }).click();
        const response = await next;
        assert.equal(response.status(), 202);
        const attempt = await response.json();
        assert.equal(attempt.run.language, language);
        const result = await finishFreeRun(identities.pupil, attempt);
        await pupilPage.getByText('Forsøg gennemført', { exact:true }).waitFor();
        check('Real free workspace run completed', { language, runId: result.run.id });
    }
    const refreshed = await homeFor(identities.pupil);
    await api(identities.pupil, '/api/me/home-game/runs', 'POST', { revision: refreshed.layout.revision,
        language:'python', task:1, assessment:true }, 400);
    assert.equal(refreshed.gold, 10);
    assert.deepEqual(refreshed.progress, []);
    phase = 'teacher-workshop';
    const question = 'Hvordan kan jeg måle forskellen, når jeg flytter lampen?';
    await pupilPage.getByText('Spørg din lærer', { exact: true }).click();
    await pupilPage.getByLabel('Dit spørgsmål').fill(question);
    await pupilPage.getByRole('button', { name:'Send spørgsmål', exact:true }).click();
    await pupilPage.getByText('Dit spørgsmål er sendt til holdets lærer.', {exact:true}).waitFor();
    await teacherPage.goto(origin + '/teacher/home-game', { waitUntil:'domcontentloaded' });
    await teacherPage.locator('.teacher-home select').first().selectOption(holdId);
    await teacherPage.getByText(question, { exact:true }).waitFor();
    await teacherPage.getByRole('link', {name:'Åbn min prøvebolig'}).click();
    for (const id of ['starter','bip01','raymond','santa','bilbo','lupp']) {
        if (id !== 'starter') await api(identities.teacher, '/api/me/avatars/' + id + '/unlock', 'POST',{});
        await teacherPage.reload({ waitUntil:'domcontentloaded' });
        await checkAvatar(teacherPage,id);
    }
    const teacher = await homeFor(identities.teacher);
    assert.equal(teacher.isTeacherSandbox, true);
    assert.equal(teacher.gold, 320);
    assert.deepEqual(teacher.progress, []);
    const teachStart=teacherPage.waitForResponse(r=>new URL(r.url()).pathname==='/api/me/home-game/runs' && r.request().method()==='POST');
    await teacherPage.getByRole('button',{name:'Kør kode',exact:true}).click();
    const trial = await teachStart;
    assert.equal(trial.status(),202);
    const teacherRun=await finishFreeRun(identities.teacher, await trial.json());
    assert.equal(teacherRun.run.isTeacherSandbox,true);
    await teacherPage.getByText('Forsøg gennemført',{exact:true}).waitFor();
    assert.equal((await homeFor(identities.teacher)).gold,320);
    await teacherPage.screenshot({ path: '/artifacts/workshop-teacher.png', fullPage:true });
    await api(identities.outsider, '/api/holds/' + holdId + '/home-game', 'GET', undefined, 404);
    await api(identities.peer, '/api/me/home-game/runs/' + status.run.id, 'GET', undefined, 404);
    await pupilPage.setViewportSize({ width:390,height:844 });
    await pupilPage.reload({ waitUntil:'domcontentloaded' });
    await pupilPage.getByRole('heading',{name:'Hjem.exe',exact:true}).waitFor();
    assert.ok(await pupilPage.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1));
    await pupilPage.screenshot({ path: '/artifacts/workshop-mobile.png', fullPage:true });
    assert.deepEqual(browserErrors, [], 'Browser runtime errors');
    check('All six selected characters displayed, mobile layout fit viewport, and class/run authorization held');
    report.passed = true;
} catch (error) {
    report.passed = false;
    report.error = String(error.stack ?? error);
    report.failedPhase = phase;
    console.log(JSON.stringify({ failed: 'staging-acceptance', phase, errorType: error.name ?? 'Error' }));
    console.error(report.error);
    process.exitCode = 1;
} finally {
    report.finishedAt = new Date().toISOString();
    report.browserErrors = browserErrors;
    if (browser) await browser.close();
    await writeFile('/artifacts/report.json', JSON.stringify(report, null, 2));
    console.log(JSON.stringify({ passed: report.passed, checks: report.checks.length,
        requests: report.requests.length, report: '/artifacts/report.json' }));
}
