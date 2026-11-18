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
const place = async (identity, home, definition, x, y, rotation = 0, wall = null) =>
    build(identity, home, { kind: 'place', itemId: installed(home, definition).id,
        placement: { anchor: { x, y }, rotation, wall } });

function source(language, task) {
    if (language === 'python') {
        if (task <= 2) return 'import home\nhome.set("desk_lamp", True)\n';
        const condition = ['home.read("daylight") < 300',
            ...(task >= 4 ? ['home.read("presence")'] : []),
            ...(task >= 5 ? ['home.read("auto_enabled")'] : [])].join(' and ');
        return 'import home\nwhile True:\n    home.set("desk_lamp", ' + condition + ')\n    home.wait(0.2)\n';
    }
    if (language === 'csharp') {
        if (task <= 2) return 'using HomeSdk;\nHome.Set("desk_lamp", true);\n';
        const condition = ['Home.Read("daylight") < 300',
            ...(task >= 4 ? ['Home.ReadBool("presence")'] : []),
            ...(task >= 5 ? ['Home.ReadBool("auto_enabled")'] : [])].join(' && ');
        return 'using HomeSdk;\nwhile (true) { Home.Set("desk_lamp", ' + condition + '); Home.Wait(0.2); }\n';
    }
    if (task <= 2)
        return '#include <VirtualHome.h>\nvoid setup() { home.set("desk_lamp", true); }\nvoid loop() { delay(200); }\n';
    const condition = ['home.read("daylight") < 300',
        ...(task >= 4 ? ['home.readBool("presence")'] : []),
        ...(task >= 5 ? ['home.readBool("auto_enabled")'] : [])].join(' && ');
    return '#include <VirtualHome.h>\nvoid setup() { home.begin(); }\nvoid loop() { home.set("desk_lamp", ' +
        condition + '); delay(200); }\n';
}
async function assess(identity, language, task) {
    let home = await homeFor(identity);
    home = await api(identity, '/api/me/home-game/program', 'PUT',
        { revision: home.layout.revision, language, source: source(language, task) });
    let run = await api(identity, '/api/me/home-game/runs', 'POST',
        { revision: home.layout.revision, language, task, assessment: true, speed: 10, intent: 'read' });
    const id = run.run.id;
    const end = Date.now() + 180000;
    let iterations = 0;
    while (['queued', 'compiling', 'running', 'paused'].includes(run.run.status)) {
        assert.ok(Date.now() < end, 'Real worker run timed out for ' + language + '/P' + task);
        if (run.run.status === 'paused') throw new Error('Unexpected paused worker run.');
        if (run.run.status === 'running')
            run = await api(identity, '/api/me/home-game/runs/' + id + '/step', 'POST',
                { afterSequence: run.sequence, maxTicks: 20 });
        else {
            await new Promise(resolve => setTimeout(resolve, 150));
            run = await api(identity, '/api/me/home-game/runs/' + id);
        }
        assert.ok(++iterations < 10000);
    }
    assert.equal(run.run.status, 'passed', JSON.stringify({ language, task, status: run.run.status,
        error: run.errorMessage, outcomes: run.outcomes }));
    assert.equal(run.run.isTeacherSandbox, true);
    assert.equal(run.goldAwarded, 0);
    assert.ok(run.outcomes.length > 0 && run.outcomes.every(outcome => outcome.passed));
    check('Real isolated teacher scenario passed', { language, task, scenarios: run.outcomes.length, runId: id });
    return run;
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
    const positions = [[0,0],[2,0],[4,0],[6,0],[8,0],[10,0],[0,2],[2,2],[4,2],[8,2]];
    for (const [x, y] of positions)
        pupilHome = await build(identities.pupil, pupilHome, { kind: 'buy', definitionId: 'rug_basic',
            placement: { anchor: { x, y }, rotation: 0 } });
    assert.equal(pupilHome.gold, 20);
    const purchase = { kind: 'buy', definitionId: 'rug_basic', placement: { anchor: { x: 10, y: 2 }, rotation: 0 } };
    await build(identities.pupil, pupilHome, purchase, crypto.randomUUID(), 422);
    assert.equal((await homeFor(identities.pupil)).gold, 20);
    check('Default 320 gold legally spent to 20; insufficient purchase rejected');

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

    let teacherHome = await homeFor(identities.teacher);
    assert.equal(teacherHome.gold, 320);
    assert.equal(teacherHome.highestAvailableTask, 6);
    assert.equal(teacherHome.isTeacherSandbox, true);
    const languages = ['python', 'csharp', 'arduino'];
    for (const language of languages) {
        phase = 'teacher-pilot-' + language;
        teacherHome = await homeFor(identities.teacher);
        // Store the lamp before restoring the desk so build ordering never strands the avatar.
        teacherHome = await build(identities.teacher, teacherHome, { kind: 'store', itemId: installed(teacherHome, 'lamp_basic').id });
        teacherHome = await place(identities.teacher, teacherHome, 'desk_basic', 6, 3);
        teacherHome = await place(identities.teacher, teacherHome, 'lamp_basic', 5, 4);
        for (let task = 1; task <= 6; task++) {
            teacherHome = await homeFor(identities.teacher);
            if (task === 2) {
                teacherHome = await place(identities.teacher, teacherHome, 'desk_basic', 2, 6);
                teacherHome = await place(identities.teacher, teacherHome, 'lamp_basic', 3, 7);
            }
            if (task === 3) teacherHome = await place(identities.teacher, teacherHome, 'daylight_sensor', 7, 0, 180,
                { x: 7, y: 0, horizontal: true });
            if (task === 4) teacherHome = await place(identities.teacher, teacherHome, 'presence_sensor', 0, 7, 90,
                { x: 0, y: 7, horizontal: false });
            if (task === 5) teacherHome = await place(identities.teacher, teacherHome, 'switch_basic', 8, 0, 180,
                { x: 8, y: 0, horizontal: true });
            await assess(identities.teacher, language, task);
        }
    }
    phase = 'teacher-browser-result-and-isolation';
    teacherHome = await homeFor(identities.teacher);
    assert.equal(teacherHome.gold, 320);
    assert.equal(teacherHome.progress.length, 0);
    await teacherPage.goto(origin + '/spil/hjem', { waitUntil: 'domcontentloaded' });
    await teacherPage.getByText('Prøvebolig for undervisere', { exact: true }).waitFor();
    assert.equal(await teacherPage.locator('.task-label option:enabled').count(), 6);
    await teacherPage.getByRole('button', { name: 'Afprøv', exact: true }).click();
    await teacherPage.getByText('Prøvevurdering bestået', { exact: true }).waitFor();
    await teacherPage.screenshot({ path: '/artifacts/teacher-real-worker.png', fullPage: true });
    await api(identities.outsider, '/api/holds/' + holdId + '/home-game', 'GET', undefined, 404);
    await api(identities.peer, '/api/me/home-game/runs/' + teacherHome.lastRun.id, 'GET', undefined, 404);
    assert.deepEqual(browserErrors, [], 'Browser runtime errors');
    check('Teacher P01–P06 passed in all three real runtimes; no teacher gold or learner progress awarded');
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
