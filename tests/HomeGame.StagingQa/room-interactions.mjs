import assert from 'node:assert/strict';
import { join } from 'node:path';

// Called serially in the disposable teacher workshop by browser.mjs. All
// writes below come from clicks on the real app. getHome verifies persistence.
export async function checkRoomInteractions(page, getHome, check, artifactDir = '/artifacts') {
    const initial = await getHome();
    assert.ok(initial.isTeacherSandbox);
    const item = (home, id) => home.layout.items.find(candidate => candidate.definitionId === id);
    const button = (container, name) => container.getByRole('button', {name, exact: true});
    const object = id => page.locator(`.room-shell .furniture:not(.ghost)[data-definition="${id}"]`);
    const tile = (x, y) => page.locator(`.home-board button.tile[data-x="${x}"][data-y="${y}"]`);
    const inspector = page.getByRole('region', {name: 'Valgt genstand', exact: true});
    const select = id => page.locator(`.items li[data-definition="${id}"] .item-label`).click();

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
        const image = node.locator(`img.object-art[src$="/${suffix}.webp"]`);
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
    await button(page, 'I boligen').click();
    await select('bookcase_low');
    await button(inspector, 'Flyt').click();
    await button(page.locator('.placement-bar'), 'Drej 90°').click();
    await page.locator('.furniture.ghost[data-definition="bookcase_low"][data-rotation="180"]').waitFor();
    await button(page.locator('.placement-bar'), 'Annullér').click();
    assert.equal((await getHome()).layout.revision, initial.layout.revision);

    for (const [rotation, art] of [[180,'bookcase-back'],[270,'bookcase-side'],[0,'bookcase'],[90,'bookcase-side']]) {
        const saved = await saveByClick(button(inspector, 'Drej på stedet'));
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

    await select('soil_probe');
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

    await select('planter_pump');
    let rotated;
    for (const [rotation, suffix] of [[180,'-back'],[270,'-side'],[0,'']]) {
        rotated = await saveByClick(button(inspector, 'Drej på stedet'));
        const probe = item(rotated,'soil_probe').placement;
        assert.equal(probe.hostId, pot.id);
        assert.equal(probe.rotation, rotation);
        if (rotation === 180) assert.deepEqual(probe.anchor, {x:pot.placement.anchor.x+1,y:pot.placement.anchor.y});
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
    await object('flip_sign').getByRole('button').click();
    await device.getByTestId('device-off').click();
    await checkArt('flip_sign','flip-sign-moon');
    await device.getByTestId('device-on').click();
    await checkArt('flip_sign','flip-sign');
    await button(page,'Indret').click();
    await button(page,'I boligen').click();
    await select('planter_pump'); // The embedded probe shares this pot's hit area.
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
