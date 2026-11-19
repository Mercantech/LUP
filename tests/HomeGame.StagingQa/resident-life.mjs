import assert from 'node:assert/strict';

export async function checkResidentLife(page, home, check, furnished) {
    const runRequests = [];
    const recordRun = request => {
        if (request.method() === 'POST' && new URL(request.url()).pathname === '/api/me/home-game/runs')
            runRequests.push(request.url());
    };
    page.on('request', recordRun);
    try {
        if (furnished) await page.reload({waitUntil:'domcontentloaded'});
        await page.bringToFront();
        const life = page.getByTestId('resident-life');
        await life.and(page.locator('[data-source="room"]')).waitFor({state:'visible'});
        const intent = page.getByTestId('resident-intent');
        assert.equal(await intent.inputValue(), 'auto');
        const targets = Object.fromEntries(home.layout.items.filter(item => item.placement)
            .map(item => [item.alias, item.definitionId]));
        const result = await page.evaluate(async ({furnished, targets}) => {
            const start = performance.now();
            const cells = new Set(), wants = new Set(), activities = new Set();
            let first, previous, recovered;
            do {
                const room = document.querySelector('[data-testid="home-resident"]');
                const life = document.querySelector('[data-testid="resident-life"]');
                const wish = life?.querySelector('[data-testid="resident-want"]');
                const energy = life?.querySelector('meter[data-need="energy"]');
                const inspiration = life?.querySelector('meter[data-need="inspiration"]');
                if (!room || !wish || !energy || !inspiration || life.dataset.source !== 'room')
                    throw Error('Missing resident in the open room');
                const {cellX, cellY, activity} = room.dataset;
                const {want, targetAlias, destinationX, destinationY} = wish.dataset;
                const current = {cellX, cellY, activity, want, targetAlias, energy:energy.value, inspiration:inspiration.value};
                if (![energy, inspiration].every(meter => meter.min===0 && meter.max===100 && Number.isFinite(meter.value)))
                    throw Error('Invalid need meter');
                if (!/^\d+$/.test(cellX) || !/^\d+$/.test(cellY) || !document.querySelector(
                    '.home-board button.tile[data-x="'+cellX+'"][data-y="'+cellY+'"]'))
                    throw Error('Resident is outside the board');
                cells.add(cellX+','+cellY);
                wants.add(want);
                activities.add(activity);
                first ??= current;
                if (previous && previous.activity === activity && previous.targetAlias === targetAlias &&
                    cellX === destinationX && cellY === destinationY) {
                    const at = targets[targetAlias];
                    const inspiringVisit = activity === 'reading' && want === 'read' && at === 'desk_basic' ||
                        activity === 'visitingPlant' && want === 'visitPlant' && at === 'planter_pump';
                    const resting = activity === 'relaxing' && want === 'rest' && at === 'armchair';
                    if (inspiringVisit && current.inspiration > previous.inspiration ||
                        resting && current.energy > previous.energy)
                        recovered = current;
                }
                const moving = cells.size >= 3 && activities.has('walking');
                if (moving && (furnished ? recovered : first.energy !== current.energy || first.inspiration !== current.inspiration))
                    return {cells:cells.size, wants:[...wants], first, last:current, recovered};
                previous=current;
                await new Promise(resolve => setTimeout(resolve,130));
            } while (performance.now()-start < (furnished ? 110000 : 25000));
            throw Error('Resident activity timed out '+JSON.stringify({cells:cells.size,wants:[...wants],activities:[...activities],first,previous}));
        }, {furnished, targets});
        if (furnished) {
            await page.screenshot({path:'/artifacts/resident-life.png',fullPage:true});
            check('Resident chose reachable furniture and recovered a need by using it', result);
        } else {
            await intent.selectOption('idle');
            await page.locator('[data-testid="home-resident"][data-activity="idle"]').waitFor();
            await page.reload({waitUntil:'domcontentloaded'});
            await life.waitFor();
            assert.equal(await intent.inputValue(), 'idle');
            await intent.selectOption('auto');
            await page.locator('[data-testid="home-resident"][data-activity="walking"]').waitFor();
            check('Selected character explored an empty home, needs changed, and automatic movement resumed after a manual stop', result);
        }
        assert.equal(runRequests.length, 0, 'Ordinary resident movement started a code run');
    } finally {
        page.off('request', recordRun);
    }
}
