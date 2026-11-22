import test from 'node:test';
import assert from 'node:assert/strict';
import { addDays, eventsOnDay, weekStart } from '../calendar.mjs';

process.env.TZ = 'Europe/Copenhagen';

test('weeks begin Monday and preserve local midnight across the Danish daylight-saving change', () => {
  const monday = weekStart(new Date(2026, 2, 29, 13));
  const followingMonday = addDays(monday, 7);
  assert.equal(monday.toISOString(), '2026-03-22T23:00:00.000Z');
  assert.equal(followingMonday.toISOString(), '2026-03-29T22:00:00.000Z');
});

test('agenda counts dates, spanning meetings and point-in-time deadlines on the right day', () => {
  const allDay = { start: '2026-11-22T00:00:00Z', end: '2026-11-24T00:00:00Z', isAllDay: true };
  const overnight = { start: '2026-11-22T22:00:00Z', end: '2026-11-23T02:00:00Z', isAllDay: false };
  const deadline = { start: '2026-11-22T23:00:00Z', end: '2026-11-22T23:00:00Z', isAllDay: false };
  const endsAtMidnight = { start: '2026-11-22T20:00:00Z', end: '2026-11-22T23:00:00Z', isAllDay: false };
  const events = [deadline, overnight, endsAtMidnight, allDay];

  assert.deepEqual(eventsOnDay(events, new Date(2026, 10, 22)), [allDay, endsAtMidnight, overnight]);
  assert.deepEqual(eventsOnDay(events, new Date(2026, 10, 23)), [allDay, overnight, deadline]);
  assert.deepEqual(eventsOnDay(events, new Date(2026, 10, 24)), []);
});
