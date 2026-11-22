import assert from 'node:assert/strict';
import { chromium } from '../../release-news/node_modules/playwright/index.mjs';

const base = 'http://localhost:9098';
const apiBase = 'http://127.0.0.1:8088';
const health = await (await fetch(apiBase + '/_qa/health')).json();
assert.equal(health.fixture, 'fictional-notebook-browser-qa');

const identities = await (await fetch(apiBase + '/_qa/identities')).json();
const pupilToken = identities.find(identity => identity.key === 'pupil').token;
const activity = 'QA Kalender · Programmering';
const secondActivity = 'QA Kalender · Netværk';
const outlookTitle = 'QA Outlook · Fagteam';
const outlookId = 'qa+AAMk/oktober=';
const outlookDescription = 'Vi gennemgår semesterplanen. <b>Vises som tekst</b>';
const quest = 'QA Quest om routere';
const activityId = '66666666-aaaa-4666-8666-666666666666';
const holder = '11111111-aaaa-4111-8111-111111111111';
const today = new Intl.DateTimeFormat('sv-SE', { timeZone: 'Europe/Copenhagen', dateStyle: 'short' }).format(new Date());
const browserInstance = await chromium.launch({ headless: true });

async function createContext(viewport) {
  const context = await browserInstance.newContext({ viewport, timezoneId: 'Europe/Copenhagen',
    hasTouch: viewport.width < 600, serviceWorkers: 'block' });
  await context.route('**/*', route => new URL(route.request().url()).origin === base
    ? route.continue() : route.abort('blockedbyclient'));
  return context;
}

async function openCalendar(context, role) {
  const page = await context.newPage();
  await page.goto(base + '/_qa/login?user=' + role);
  const menu = page.getByLabel('Kontomenu');
  await menu.waitFor();
  await menu.focus();
  await page.keyboard.press('Enter');
  await menu.locator('..').getByRole('link', { name: 'Min kalender', exact: true }).click();
  await page.locator('.personal-calendar').waitFor();
  return page;
}

function calendarEntry(page, title, source = 'activity') {
  return page.locator('[data-calendar-source="' + source + '"]').filter({ hasText: title });
}

async function changeStatus(resourceId, state) {
  const response = await fetch(base + '/api/me/actions?version=2', { headers: { Authorization: 'Bearer ' + pupilToken } });
  assert.equal(response.status, 200);
  const { items } = await response.json();
  return items.filter(item => item.kind === 'calendar-change' && item.targetId === resourceId && item.state === state);
}

async function outlookCopies(subject) {
  const response = await fetch(apiBase + '/_qa/outlook-copies');
  assert.equal(response.status, 200);
  return (await response.json()).filter(copy => copy.subject === subject);
}

try {
  const teacherContext = await createContext({ width: 1440, height: 1050 });
  const clock = await teacherContext.newPage();
  await clock.goto(base + '/_qa');
  const [start, end] = await clock.evaluate(day =>
    ['10:15', '11:15'].map(time => new Date(day + 'T' + time + ':00').toISOString()), today);
  await clock.close();
  const outlookEvent = {
    id: outlookId, subject: outlookTitle, start, end, isCancelled: false, isPrivate: false,
    isAllDay: false, location: 'Mødelokale', isOnlineMeeting: true,
    onlineMeetingUrl: 'https://teams.microsoft.com/qa-meeting',
    webLink: 'https://outlook.office.com/qa-event'
  };
  await teacherContext.route('**/api/calendar/mine?**', route => route.fulfill({ json: {
    configured: true, azureUpn: 'underviser@example.test', fromUtc: start, toUtc: end,
    events: [
      outlookEvent,
      { id: 'qa-private', subject: 'Privat', start, end, isPrivate: true, isAllDay: false },
    ]
  } }));
  let outlookDetailReads = 0;
  await teacherContext.route('**/api/calendar/mine/event?**', route => {
    assert.equal(new URL(route.request().url()).searchParams.get('id'), outlookId);
    outlookDetailReads++;
    return route.fulfill({ json: {
      event: outlookEvent, description: outlookDescription, hideAttendees: false,
      organizer: { name: 'QA Mødeleder', address: 'moedeleder@example.test' },
      attendees: [{ name: 'QA Deltager', address: 'deltager@example.test', type: 'required', response: 'accepted' }]
    } });
  });

  const pupilContext = await createContext({ width: 390, height: 844 });
  let pupilOutlookReads = 0;
  pupilContext.on('request', request => {
    if (new URL(request.url()).pathname.startsWith('/api/calendar/mine')) pupilOutlookReads++;
  });
  const pupil = await openCalendar(pupilContext, 'pupil');
  await pupil.getByRole('heading', { name: 'Min dag', exact: true }).waitFor();
  await calendarEntry(pupil, activity).waitFor();
  await calendarEntry(pupil, secondActivity).waitFor();
  await pupil.getByRole('region', { name: 'Kommende questfrister' }).getByRole('link', { name: quest }).waitFor();
  await pupil.getByLabel('Vis hold').selectOption({ label: 'QA Netværkshold' });
  await calendarEntry(pupil, activity).waitFor({ state: 'hidden' });
  await pupil.getByLabel('Vis hold').selectOption({ label: 'Alle hold' });
  await calendarEntry(pupil, activity).waitFor();
  assert.ok(await pupil.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
  assert.equal(await calendarEntry(pupil, outlookTitle, 'outlook').count(), 0);

  const pupilTwin = await pupilContext.newPage();
  const protectedLink = base + '/student/calendar?check=ny-fane#main-content';
  await pupilTwin.goto(protectedLink);
  await pupilTwin.getByRole('heading', { name: 'Min dag', exact: true }).waitFor();
  await calendarEntry(pupilTwin, activity).waitFor();
  assert.equal(pupilTwin.url(), protectedLink);
  assert.equal(await pupilTwin.evaluate(() => window.opener), null);

  const teacher = await openCalendar(teacherContext, 'teacher');
  const outlookCard = calendarEntry(teacher, outlookTitle, 'outlook');
  await outlookCard.waitFor();
  await calendarEntry(teacher, activity).waitFor();
  const privateCard = calendarEntry(teacher, 'Privat aftale', 'outlook');
  await privateCard.waitFor();
  assert.equal(await privateCard.getByRole('button').count(), 0);
  assert.equal(outlookDetailReads, 0);

  const eventTrigger = outlookCard.getByRole('button');
  await eventTrigger.focus();
  await teacher.keyboard.press('Enter');
  const eventDialog = teacher.getByRole('dialog');
  await eventDialog.getByRole('heading', { name: outlookTitle, exact: true }).waitFor();
  await eventDialog.getByText(outlookDescription, { exact: true }).waitFor();
  assert.equal(await eventDialog.locator('b').count(), 0);
  await eventDialog.locator('dd').filter({ hasText: 'QA Mødeleder' }).waitFor();
  await eventDialog.getByText('QA Deltager', { exact: true }).waitFor();
  await eventDialog.getByText('Accepteret', { exact: true }).waitFor();
  assert.equal(await eventDialog.getByRole('link', { name: /Outlook/i }).getAttribute('href'), outlookEvent.webLink);
  assert.equal(await eventDialog.getByRole('link', { name: /Teams/i }).getAttribute('href'), outlookEvent.onlineMeetingUrl);
  assert.equal(outlookDetailReads, 1);
  await teacher.keyboard.press('Escape');
  await eventDialog.waitFor({ state: 'hidden' });
  await teacher.waitForFunction(() => document.activeElement?.getAttribute('aria-label')?.startsWith('Åbn Outlook-aftale:'));

  await teacher.setViewportSize({ width: 390, height: 844 });
  await eventTrigger.click();
  await eventDialog.getByText('QA Deltager', { exact: true }).waitFor();
  const dialogBounds = await eventDialog.boundingBox();
  assert.ok(dialogBounds.width <= 390 && dialogBounds.x >= 0);
  await teacher.keyboard.press('Escape');
  await eventDialog.waitFor({ state: 'hidden' });
  await teacher.setViewportSize({ width: 1440, height: 1050 });
  await teacher.getByRole('button', { name: 'Redigér ' + activity, exact: true }).click();
  const editor = teacher.locator('.pc-editor');
  await editor.getByLabel('Start (dansk tid)').fill(today + 'T10:30');
  await editor.getByRole('button', { name: 'Gem aktivitet', exact: true }).click();
  await editor.getByRole('button', { name: 'Gem alligevel', exact: true }).waitFor();
  await editor.getByLabel('Start (dansk tid)').fill(today + 'T12:30');
  const saved = teacher.waitForResponse(response => response.request().method() === 'PUT'
    && new URL(response.url()).pathname.endsWith('/calendar/' + activityId));
  await editor.getByRole('button', { name: 'Gem aktivitet', exact: true }).click();
  assert.equal((await saved).status(), 200);
  await editor.waitFor({ state: 'hidden' });

  await pupil.reload();
  await calendarEntry(pupil, activity).getByText(/12[.:]30/).waitFor();
  await pupil.getByRole('button', { name: /^Notifikationer/ }).click();
  const notification = pupil.getByRole('region', { name: 'Notifikationer' }).getByRole('link', { name: /QA Kalender · Programmering/ });
  await notification.waitFor();
  assert.equal(await notification.count(), 1);
  assert.equal((await changeStatus(activityId, 'moved')).length, 1);
  await pupil.keyboard.press('Escape');
  await pupil.goto(base + '/courses/' + holder + '?view=calendar');
  await pupil.locator('.hold-calendar').getByText(activity, { exact: true }).waitFor();
  assert.match(await pupil.locator('.hold-calendar').textContent(), /12[.:]30/);
  assert.equal(pupilOutlookReads, 0);

  await teacher.getByRole('button', { name: 'Redigér ' + activity, exact: true }).click();
  await editor.getByRole('button', { name: 'Slet aktivitet', exact: true }).click();
  await teacher.getByRole('button', { name: 'Bekræft sletning', exact: true }).click();
  await calendarEntry(teacher, activity).waitFor({ state: 'hidden' });
  assert.equal((await changeStatus(activityId, 'cancelled')).length, 1);

  await teacher.getByRole('button', { name: 'Planlæg aktivitet', exact: true }).click();
  await editor.getByLabel(/^Hold/).selectOption({ label: 'QA Netværkshold' });
  await editor.getByLabel(/^Type/).selectOption('quest');
  await editor.getByLabel(/^Quest/).selectOption({ label: quest });
  await editor.getByLabel('Fremmødeform').selectOption('online');
  await editor.getByLabel('Start (dansk tid)').fill(today + 'T15:30');
  await editor.getByRole('button', { name: 'Gem aktivitet', exact: true }).click();
  await editor.waitFor({ state: 'hidden' });
  await pupil.goto(base + '/student/calendar');
  await calendarEntry(pupil, quest).getByText('Online fremmøde').waitFor();
  await pupil.getByRole('region', { name: 'Kommende questfrister' }).getByRole('link', { name: quest }).waitFor();

  const plannerCopyTitle = 'QA Outlook fra kalender';
  await teacher.getByRole('button', { name: 'Planlæg aktivitet', exact: true }).click();
  await editor.getByLabel('Titel').fill(plannerCopyTitle);
  await editor.getByLabel('Start (dansk tid)').fill(today + 'T17:15');
  await editor.getByRole('button', { name: /^45 min/ }).click();
  assert.equal(await editor.getByLabel('Varighed (minutter)').inputValue(), '45');
  await editor.getByRole('switch', { name: 'Kasper' }).check();
  await editor.getByRole('switch', { name: 'Mathias' }).check();
  await editor.getByRole('button', { name: 'Gem og kopiér til Outlook', exact: true }).click();
  await calendarEntry(teacher, plannerCopyTitle).waitFor();
  const plannerCopies = teacher.getByRole('region', { name: 'Outlook-kopier' });
  await plannerCopies.getByText('Kunne ikke kopieres', { exact: true }).waitFor();
  await plannerCopies.getByText('Oprettet', { exact: true }).waitFor();
  await plannerCopies.getByRole('button', { name: 'Prøv Outlook igen' }).click();
  await plannerCopies.getByText('Oprettet', { exact: true }).nth(1).waitFor();
  assert.equal(await plannerCopies.getByText('Oprettet', { exact: true }).count(), 2);
  const plannerAttempts = await outlookCopies(plannerCopyTitle);
  const kasperAttempts = plannerAttempts.filter(copy => copy.mailbox === 'kasper@example.test');
  const mathiasAttempts = plannerAttempts.filter(copy => copy.mailbox === 'mathias@example.test');
  assert.deepEqual(kasperAttempts.map(copy => copy.accepted), [false, true]);
  assert.equal(kasperAttempts[0].transactionId, kasperAttempts[1].transactionId);
  assert.equal(mathiasAttempts.length, 1);
  assert.equal(mathiasAttempts[0].accepted, true);
  assert.notEqual(kasperAttempts[0].transactionId, mathiasAttempts[0].transactionId);
  await teacher.reload();
  await calendarEntry(teacher, plannerCopyTitle).waitFor();
  assert.equal(await calendarEntry(teacher, plannerCopyTitle).count(), 1);
  assert.equal((await outlookCopies(plannerCopyTitle)).length, 3);

  const holdCopyTitle = 'QA Outlook fra hold';
  await teacher.setViewportSize({ width: 390, height: 844 });
  await teacher.goto(base + '/courses/' + holder + '?view=calendar');
  const holdCalendar = teacher.locator('.hold-calendar');
  await holdCalendar.getByRole('button', { name: 'Opret begivenhed', exact: true }).click();
  const holdEditor = holdCalendar.locator('.hc-editor');
  await holdEditor.getByLabel('Titel').fill(holdCopyTitle);
  await holdEditor.getByLabel('Start (dansk tid)').fill(today + 'T16:15');
  await holdEditor.getByRole('button', { name: /^30 min/ }).click();
  await holdEditor.getByRole('switch', { name: 'Kasper' }).check();
  assert.ok(await teacher.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
  const holdSave = teacher.waitForResponse(response => response.request().method() === 'POST'
    && /\/holds\/[^/]+\/calendar$/.test(new URL(response.url()).pathname));
  await holdEditor.getByRole('button', { name: 'Gem og kopiér til Outlook', exact: true }).click();
  const holdSaved = await holdSave;
  assert.equal(holdSaved.status(), 200);
  const holdEvent = await holdSaved.json();
  assert.equal(Date.parse(holdEvent.endUtc) - Date.parse(holdEvent.startUtc), 30 * 60 * 1000);
  await holdCalendar.getByRole('region', { name: 'Outlook-kopier' }).getByText('Oprettet', { exact: true }).waitFor();
  const holdAttempts = await outlookCopies(holdCopyTitle);
  assert.deepEqual(holdAttempts.map(copy => [copy.mailbox, copy.accepted]), [['kasper@example.test', true]]);
  assert.deepEqual(await outlookCopies(quest), []);
  await teacher.setViewportSize({ width: 1440, height: 1050 });

  await teacher.goto(base + '/teacher');
  const favorites = teacher.getByRole('list', { name: 'Favoritgenveje' }).getByRole('link');
  await favorites.nth(6).waitFor();
  await calendarEntry(teacher, outlookTitle, 'outlook').waitFor();
  assert.equal(await favorites.count(), 7);
  assert.equal(await teacher.locator('a.brand').getAttribute('aria-current'), 'page');

  await teacher.goto(base + '/help');
  await teacher.getByRole('link', { name: 'Min kalender', exact: true }).filter({ visible: true }).waitFor();
  await pupil.goto(base + '/help');
  const pupilGuide = pupil.locator('article').getByRole('link', { name: 'Min kalender' });
  await pupilGuide.waitFor();
  assert.equal(await pupilGuide.getAttribute('href'), '/student/calendar');

  await pupil.getByLabel('Kontomenu').click();
  await pupil.getByRole('button', { name: 'Log ud', exact: true }).click();
  await pupilTwin.getByRole('heading', { name: 'Log ind', exact: true }).waitFor();

  console.log('Kalender-QA bestået: holdfilter, Outlook-detaljer, Outlook-kopier og gentagelse, overlap, notifikation, aflysning, questfrister, mobil og underviserforside.');
} finally {
  await browserInstance.close();
}
