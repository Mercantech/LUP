import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { join } from 'node:path';

// Check visibility before Playwright can scroll the control into view for a click.
async function visibleControl(control) {
    await control.waitFor({state: 'visible'});
    const reachable = await control.evaluate(el => {
        const r = el.getBoundingClientRect();
        const hit = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2);
        return r.left >= 0 && r.top >= 0 && r.right <= innerWidth + 1 && r.bottom <= innerHeight + 1 &&
            (hit === el || el.contains(hit));
    });
    assert.ok(reachable, (await control.getAttribute('aria-label') || await control.innerText()) + ' should be on screen and clickable');
}

// Artwork can overlap another sprite. Use an actual exposed point on the chosen
// handle, just as a pupil would, and wait for responsive layout after resizing.
async function exposedPoint(handle) {
    return handle.evaluate(async el => {
        const object = el.closest('[data-definition]');
        const end = performance.now() + 2500;
        let diagnostic;
        do {
            await new Promise(requestAnimationFrame);
            const rect = el.getBoundingClientRect();
            const left = Math.max(0, rect.left), right = Math.min(innerWidth, rect.right);
            const top = Math.max(0, rect.top), bottom = Math.min(innerHeight, rect.bottom);
            const fractions = [.5, .25, .75, .1, .9];
            if (left < right && top < bottom) {
                for (const fy of fractions) for (const fx of fractions) {
                    const x = left + (right - left) * fx, y = top + (bottom - top) * fy;
                    const hit = document.elementFromPoint(x, y);
                    if (hit && (hit === el || el.contains(hit))) return {x, y};
                    if (fx === .5 && fy === .5) diagnostic = {
                        hit:hit?.tagName, class:hit?.className,
                        coveringObject:hit?.closest('[data-definition]')?.dataset.definition
                    };
                }
            }
            diagnostic = {...diagnostic, object:object?.dataset.definition,
                rect:rect.toJSON(), viewport:[innerWidth,innerHeight], pageScroll:[scrollX,scrollY],
                room:el.closest('.room-shell')?.getBoundingClientRect().toJSON()};
        } while (performance.now() < end);
        throw Error('Furniture has no exposed click target: ' + JSON.stringify(diagnostic));
    });
}

export async function checkContextActions(page, definition, output) {
    const size = page.viewportSize();
    const object = page.locator(`.room-shell .furniture:not(.ghost)[data-definition="${definition}"]`);
    const handle = object.locator('.object-handle');
    const menu = page.getByTestId('item-actions');
    const placement = page.getByTestId('placement-actions');
    const button = (region, name) => region.getByRole('button', {name, exact:true});
    const scrollOffsets = () => page.locator('.room-shell').evaluate(el => {
        const offsets = [];
        for (let parent = el; parent; parent = parent.parentElement) offsets.push([parent.scrollLeft,parent.scrollTop]);
        return offsets;
    });

    const touch = await page.evaluate(() => navigator.maxTouchPoints > 0);
    const viewports = touch ? [size] : [{width:1280,height:800}, {width:375,height:667}];
    const activate = control => touch ? control.tap() : control.click();
    for (const viewport of viewports) {
        await page.setViewportSize(viewport);
        await object.locator('img.object-art').evaluate(image => image.decode());
        await handle.evaluate(el => el.scrollIntoView({block:'center', inline:'nearest', behavior:'instant'}));
        const point = await exposedPoint(handle);
        const before = await scrollOffsets();
        if (touch) await page.touchscreen.tap(point.x, point.y);
        else await page.mouse.click(point.x, point.y);
        await visibleControl(button(menu, 'Flyt'));
        await visibleControl(button(menu, 'Drej 90°'));
        assert.equal(await menu.getAttribute('data-item-id'), await object.getAttribute('data-item-id'));
        assert.equal(await menu.getAttribute('role'), 'dialog');
        assert.ok((await menu.getAttribute('aria-label')).trim());
        assert.equal(await menu.locator('details.item-more').evaluate(details => details.open), false,
            'Secondary actions stay folded until requested');
        assert.deepEqual(await scrollOffsets(), before, 'Opening the furniture menu scrolled the room');
        const screen = touch ? 'touch' : viewport.width < 600 ? 'mobile' : 'desktop';
        if (output) await page.screenshot({path:join(output, `item-actions-${screen}.png`)});
        if (touch) await button(menu,'Luk handlinger').tap();
        else await page.keyboard.press('Escape');
        await menu.waitFor({state:'hidden'});
        if (touch) {
            const again = await exposedPoint(handle);
            await page.touchscreen.tap(again.x, again.y);
        }
        else {
            assert.ok(await handle.evaluate(el => document.activeElement === el), 'Escape returns focus to the furniture');
            await handle.press('Enter');
        }
        await visibleControl(button(menu,'Flyt'));
        await activate(button(menu,'Flyt'));
        await visibleControl(button(placement,'Annullér'));
        await visibleControl(placement.getByTestId('confirm-placement'));
        const after = await scrollOffsets();
        assert.deepEqual(after, before, `Moving a visible item at ${viewport.width}×${viewport.height} scrolled the room from ${JSON.stringify(before)} to ${JSON.stringify(after)}`);
        await activate(button(placement,'Annullér'));
        await placement.waitFor({state:'hidden'});
        if (await menu.isVisible()) await page.keyboard.press('Escape');
    }
    await page.setViewportSize(size);
}

// Called serially in the disposable teacher workshop by browser.mjs. All
// writes below come from clicks on the real app. getHome verifies persistence.
export async function checkRoomInteractions(page, getHome, check, artifactDir = '/artifacts') {
    const initial = await getHome();
    assert.ok(initial.isTeacherSandbox);
    const item = (home, id) => home.layout.items.find(candidate => candidate.definitionId === id);
    const button = (container, name) => container.getByRole('button', {name, exact: true});
    const object = id => page.locator(`.room-shell .furniture:not(.ghost)[data-definition="${id}"]`);
    const tile = (x, y) => page.locator(`.home-board button.tile[data-x="${x}"][data-y="${y}"]`);
    const inspector = page.getByTestId('item-actions');
    const select = async (id, fromList = false) => {
        const expected = await object(id).getAttribute('data-item-id');
        if (await inspector.isVisible() && await inspector.getAttribute('data-item-id') === expected) return;
        if (fromList) await page.locator(`.items li[data-definition="${id}"] .item-label`).click();
        else {
            const handle = object(id).locator('.object-handle');
            await handle.scrollIntoViewIfNeeded();
            const point = await exposedPoint(handle);
            await page.mouse.click(point.x, point.y);
        }
        await inspector.and(page.locator(`[data-item-id="${expected}"]`)).waitFor({state:'visible'});
    };

    async function saveByClick(target) {
        const [response] = await Promise.all([
            page.waitForResponse(r => new URL(r.url()).pathname === '/api/me/home-game/build' && r.request().method() === 'POST'),
            target.click()
        ]);
        assert.equal(response.status(), 200, await response.text());
        return response.json();
    }
    async function checkArt(id, suffix, rotation) {
        const node = rotation === undefined ? object(id) : object(id).and(page.locator(`[data-rotation="${rotation}"]`));
        const image = node.locator(`img.object-art[src="images/home-workshop/0108ab341b3c/${suffix}.webp"]`);
        await image.waitFor({state:'visible'});
        const dimensions = await image.evaluate(async image => {
            await image.decode();
            const bounds = image.getBoundingClientRect();
            return [image.naturalWidth, image.naturalHeight, bounds.width, bounds.height];
        });
        assert.ok(dimensions.every(size => size > 0), suffix + ' must decode and be visible');
        if (!await node.evaluate(el => el.classList.contains('wall-piece'))) {
            const [width, height, drawnWidth, drawnHeight] = dimensions;
            assert.ok(Math.abs(width / height - drawnWidth / drawnHeight) < .02,
                suffix + ' furniture must retain its authored proportions on the floor');
        }
    }

    await button(page, 'Indret').click();
    await button(page, 'Butik').click();
    const shopImage = page.locator('.items li[data-definition="bookcase_low"] img[src="images/home-workshop/0108ab341b3c/bookcase.webp"]');
    await shopImage.waitFor({state:'visible'});
    await shopImage.evaluate(image => image.decode());
    await button(page, 'I boligen').click();
    const [moduleResponse] = await Promise.all([
        page.waitForResponse(response => {
            const url = new URL(response.url());
            return url.origin === new URL(page.url()).origin && url.pathname === '/js/home-item-actions.js';
        }),
        checkContextActions(page, 'bookcase_low', artifactDir)
    ]);
    assert.equal(moduleResponse.status(),200);
    const moduleUrl = new URL(moduleResponse.url());
    assert.ok(moduleUrl.searchParams.get('v'), 'The real furniture menu must load its versioned module');
    check('Furniture action menu works without scrolling on desktop and mobile', {
        path:moduleUrl.pathname+moduleUrl.search, sha256:createHash('sha256').update(await moduleResponse.body()).digest('hex')
    });
    assert.equal(await page.getByTestId('light-panel').isVisible(),false, 'Decorating a bookcase needs no light panel');
    await select('bookcase_low');
    await button(inspector, 'Flyt').click();
    await button(page.locator('.placement-bar'), 'Drej 90°').click();
    await page.locator('.furniture.ghost[data-definition="bookcase_low"][data-rotation="180"]').waitFor();
    await button(page.locator('.placement-bar'), 'Annullér').click();
    assert.equal((await getHome()).layout.revision, initial.layout.revision);

    for (const [rotation, art] of [[180,'bookcase-back'],[270,'bookcase-side'],[0,'bookcase'],[90,'bookcase-side']]) {
        if (rotation === 180) await select('bookcase_low');
        const saved = await saveByClick(button(inspector, 'Drej 90°'));
        assert.equal(item(saved, 'bookcase_low').placement.rotation, rotation);
        await checkArt('bookcase_low', art, rotation);
        if (rotation === 90 || rotation === 270) {
            const transform = await object('bookcase_low').locator('.object-art').evaluate(el => {
                const value = getComputedStyle(el).transform;
                return value === 'none' ? 1 : new DOMMatrixReadOnly(value).a;
            });
            assert.equal(transform, rotation === 90 ? -1 : 1);
        }
    }

    await select('corkboard');
    await button(inspector, 'Flyt').click();
    await tile(4, 5).click();
    await page.locator('[data-testid="confirm-placement"]:disabled').waitFor();
    assert.deepEqual(item(await getHome(),'corkboard').placement, item(initial,'corkboard').placement);
    const sideWall = page.locator('.mount-point[data-edge="left"][aria-label="Montér på væggen ved felt 0, 5"]');
    const wallSave = await saveByClick(sideWall);
    assert.deepEqual(item(wallSave,'corkboard').placement.wall, {x:0,y:5,horizontal:false});
    await object('corkboard').and(page.locator('[data-edge="left"]')).waitFor();
    await checkArt('corkboard', 'pinboard', 90);

    await select('soil_probe', true);
    await inspector.getByText('Flere muligheder', {exact:true}).click();
    const stored = await saveByClick(button(inspector, 'Læg på lager'));
    assert.equal(item(stored,'soil_probe').placement, null);
    await button(page.locator('.items li[data-definition="soil_probe"]'), 'Placér').click();
    await tile(6,3).click(); // The desk has ports, but cannot host a soil probe.
    await page.locator('[data-testid="confirm-placement"]:disabled').waitFor();
    assert.equal((await getHome()).layout.revision, stored.layout.revision);
    const pot = item(stored,'planter_pump');
    const mounted = await saveByClick(tile(pot.placement.anchor.x, pot.placement.anchor.y));
    assert.equal(item(mounted,'soil_probe').placement.hostId, pot.id);
    assert.equal(item(mounted,'soil_probe').placement.portId, 'soil');
    await checkArt('soil_probe', 'soil-probe-side', 90);

    await select('planter_pump', true);
    const moved = { x: pot.placement.anchor.x + 1, y: pot.placement.anchor.y };
    const bar = page.locator('.placement-bar');
    async function previewMountedMove() {
        await select('planter_pump', true);
        await button(inspector, 'Flyt').click();
        await tile(moved.x, moved.y).hover();
        await page.waitForFunction(x => document.querySelector('.ghost.planter_pump')?.style.getPropertyValue('--x') === String(x), moved.x);
        await button(bar, 'Drej 90°').click();
        const preview = page.locator('.furniture.ghost[data-rotation="180"]');
        const plot = preview.and(page.locator('[data-definition="planter_pump"]'));
        const sensor = preview.and(page.locator('[data-definition="soil_probe"]'));
        await sensor.waitFor({state:'visible'});
        assert.equal(await plot.evaluate(el => el.style.getPropertyValue('--x')), String(moved.x));
        assert.deepEqual(await sensor.boundingBox(), await plot.boundingBox());
        assert.equal(await object('soil_probe').count(), 0);
    }
    await previewMountedMove();
    await button(bar, 'Annullér').click();
    await checkArt('planter_pump','planter-side',90);
    await checkArt('soil_probe','soil-probe-side',90);
    assert.equal((await getHome()).layout.revision, mounted.layout.revision);
    assert.deepEqual(await object('soil_probe').boundingBox(), await object('planter_pump').boundingBox());
    await previewMountedMove();
    let rotated = await saveByClick(page.getByTestId('confirm-placement'));
    assert.deepEqual(item(rotated,'planter_pump').placement.anchor, moved);
    const movedProbe = item(rotated,'soil_probe').placement;
    assert.equal(movedProbe.hostId, pot.id);
    assert.equal(movedProbe.rotation, 180);
    assert.deepEqual(movedProbe.anchor, { x: moved.x + 1, y: moved.y });
    await checkArt('planter_pump','planter-back',180);
    await checkArt('soil_probe','soil-probe-back',180);
    assert.deepEqual(await object('soil_probe').boundingBox(), await object('planter_pump').boundingBox());
    for (const [rotation, suffix] of [[270,'-side'],[0,'']]) {
        if (rotation === 270) await select('planter_pump', true);
        rotated = await saveByClick(button(inspector, 'Drej 90°'));
        const probe = item(rotated,'soil_probe').placement;
        assert.equal(probe.hostId, pot.id);
        assert.equal(probe.rotation, rotation);
        await checkArt('planter_pump',rotation === 270 ? 'planter-side-reverse' : 'planter'+suffix,rotation);
        await checkArt('soil_probe','soil-probe'+suffix,rotation);
    }
    await page.reload({waitUntil:'domcontentloaded'});
    await checkArt('planter_pump','planter',0);
    await checkArt('soil_probe','soil-probe',0);
    await checkArt('bookcase_low','bookcase-side',90);
    await object('corkboard').and(page.locator('[data-edge="left"]')).waitFor();
    const restored = await getHome();
    assert.equal(restored.gold, initial.gold);
    assert.equal(restored.layout.revision, rotated.layout.revision);
    assert.deepEqual(item(restored,'soil_probe').placement, item(rotated,'soil_probe').placement);

    const device = page.getByTestId('device-inspector');
    await select('flip_sign');
    if (await button(inspector, 'Vis sol').isVisible()) await button(inspector, 'Vis sol').click();
    await button(inspector, 'Vis måne').click();
    await checkArt('flip_sign','flip-sign-moon');
    await button(inspector, 'Vis sol').click();
    await checkArt('flip_sign','flip-sign');
    await button(page,'Indret').click();
    await button(page,'I boligen').click();
    await select('soil_probe');
    await page.keyboard.press('Escape');
    await inspector.waitFor({state:'hidden'});
    await select('planter_pump');
    await device.getByTestId('device-off').click();
    const dry = Number(await object('soil_probe').getAttribute('data-moisture'));
    assert.equal(await object('planter_pump').locator('.water-drop').count(),0);
    await device.getByTestId('device-on').click();
    await object('planter_pump').locator('.water-drop').waitFor({state:'visible'});
    const wet = Number(await object('soil_probe').getAttribute('data-moisture'));
    assert.ok(wet>dry && wet<=100, `Probe should show actual watering, ${dry} → ${wet}`);
    assert.equal(Number(await object('planter_pump').getAttribute('data-moisture')), wet);
    await page.screenshot({path:join(artifactDir,'workshop-furnished.png'),fullPage:true});
    await page.emulateMedia({reducedMotion:'reduce'});
    assert.equal(await object('planter_pump').locator('.water-drop').evaluate(el => getComputedStyle(el).animationName),'none');
    assert.equal(await object('roller_blind').locator('.blind-cloth').evaluate(el => getComputedStyle(el).transitionDuration),'0s');
    await page.emulateMedia({reducedMotion:'no-preference'});
    check('Real teacher UI rotated furniture through four views, moved a board to the wall and mounted a live soil probe',
        {savedRevision:rotated.layout.revision,teacherGold:restored.gold,dryPercent:dry,wetPercent:wet});
}
