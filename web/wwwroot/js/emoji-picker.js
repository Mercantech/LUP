const sessions = new WeakMap();

export function destroy(anchor, restoreFocus = false) {
    const session = sessions.get(anchor);
    if (session) {
        session.controller.abort();
        session.observer.disconnect();
        if (session.panel.popover === 'manual' && session.panel.matches(':popover-open')) session.panel.hidePopover();
        sessions.delete(anchor);
    }
    if (restoreFocus && anchor instanceof HTMLElement && anchor.isConnected)
        anchor.focus({ preventScroll: true });
}

export function mount(anchor, panel, callback, focusSearch = false) {
    if (!(anchor instanceof HTMLElement) || !(panel instanceof HTMLElement) ||
        !anchor.isConnected || !panel.isConnected) return false;
    let session = sessions.get(anchor);
    if (session?.panel !== panel) {
        destroy(anchor);
        session = createSession(anchor, panel, callback);
        sessions.set(anchor, session);
    }
    session.fit();
    session.syncResults();
    if (focusSearch) {
        const search = panel.querySelector('.emoji-picker-upload-name:not(:disabled), .emoji-picker-search:not(:disabled)');
        if (search) {
            search.focus({ preventScroll: true });
            return true;
        }
    }
    return false;
}

function createSession(anchor, panel, callback) {
    const controller = new AbortController();
    const { signal } = controller;
    const floating = getComputedStyle(panel).position !== 'static';
    const topLayer = floating && typeof panel.showPopover === 'function';
    if (topLayer) {
        panel.popover = 'manual';
        Object.assign(panel.style, { position: 'fixed', inset: 'auto', margin: '0' });
        panel.showPopover();
    }
    let options = [];
    let results, currentOption, pageUp, pageDown, lastQuery, lastCategory;
    function syncPaging() {
        if (!results || !pageUp || !pageDown) return;
        const remaining = results.scrollHeight - results.clientHeight;
        pageUp.parentElement.hidden = remaining <= 1;
        pageUp.disabled = results.scrollTop <= 1;
        pageDown.disabled = results.scrollTop >= remaining - 1;
    }
    function showPreview(option) {
        const preview = panel.querySelector('.emoji-picker-preview');
        if (!option || !preview) return;
        preview.querySelector('img').src = option.querySelector('img').src;
        preview.querySelector('span').textContent = option.title;
    }
    function syncResults() {
        const list = panel.querySelector('.emoji-picker-results');
        const changed = results !== list || lastQuery !== list?.dataset.query || lastCategory !== list?.dataset.category;
        results = list;
        options = [...panel.querySelectorAll('.emoji-picker-option')];
        pageUp = panel.querySelector('.emoji-picker-page-up');
        pageDown = panel.querySelector('.emoji-picker-page-down');
        if (changed && list) {
            list.scrollTop = 0;
            lastQuery = list.dataset.query;
            lastCategory = list.dataset.category;
            showPreview(options[0]);
        }
        if (currentOption) currentOption.tabIndex = -1;
        currentOption = options.includes(document.activeElement) ? document.activeElement : options[0];
        if (currentOption) currentOption.tabIndex = 0;
        syncPaging();
    }
    panel.addEventListener('scroll', syncPaging, { signal, capture: true, passive: true });
    panel.addEventListener('click', event => {
        const button = event.target.closest('.emoji-picker-paging button');
        if (!results || !button) return;
        results.scrollBy({ top: (button === pageUp ? -1 : 1) * results.clientHeight });
    }, { signal });
    let pointerX, pointerY;
    panel.addEventListener('pointermove', event => {
        if (event.pointerType === 'touch' || (event.clientX === pointerX && event.clientY === pointerY)) return;
        pointerX = event.clientX;
        pointerY = event.clientY;
        const option = event.target.closest('.emoji-picker-option');
        if (option && option.title !== panel.querySelector('.emoji-picker-preview span')?.textContent) showPreview(option);
    }, { signal });
    panel.addEventListener('focusin', event => {
        const option = event.target.closest('.emoji-picker-option');
        if (!option) return;
        if (currentOption) currentOption.tabIndex = -1;
        currentOption = option;
        option.tabIndex = 0;
        showPreview(option);
    }, { signal });
    let closing = false;
    function close(restoreFocus) {
        if (closing || panel.dataset.uploadBusy === 'true') return;
        closing = true;
        callback.invokeMethodAsync('Dismiss', restoreFocus).catch(() => {}).finally(() => { closing = false; });
    }
    function fit() {
        if (!anchor.isConnected || !panel.isConnected) {
            destroy(anchor);
            return;
        }
        if (!floating) { syncPaging(); return; }
        const viewport = window.visualViewport;
        const top = (viewport?.offsetTop ?? 0) + 8;
        const bottom = top + (viewport?.height ?? window.innerHeight) - 16;
        const left = (viewport?.offsetLeft ?? 0) + 8;
        const right = left + (viewport?.width ?? document.documentElement.clientWidth) - 16;
        const button = anchor.getBoundingClientRect();
        const above = button.top - top - 6;
        const below = bottom - button.bottom - 6;
        const desired = Math.min(424, bottom - top);
        const flip = below < desired && above > below;
        const available = flip ? above : below;
        panel.style.maxHeight = Math.max(0, available < 260 ? desired : Math.min(desired, available)) + 'px';
        if (topLayer) {
            const { width, height } = panel.getBoundingClientRect();
            const preferredTop = flip ? button.top - 6 - height : button.bottom + 6;
            panel.style.left = Math.max(left, Math.min(button.right - width, right - width)) + 'px';
            panel.style.top = Math.max(top, Math.min(preferredTop, bottom - height)) + 'px';
        } else {
            panel.style.top = flip ? 'auto' : 'calc(100% + 6px)';
            panel.style.bottom = flip ? 'calc(100% + 6px)' : 'auto';
            panel.style.transform = '';
            const bounds = panel.getBoundingClientRect();
            const shiftX = bounds.left < left ? left - bounds.left
                : bounds.right > right ? right - bounds.right : 0;
            const shiftY = bounds.top < top ? top - bounds.top
                : bounds.bottom > bottom ? bottom - bounds.bottom : 0;
            panel.style.transform = `translate(${shiftX}px, ${shiftY}px)`;
        }
        syncPaging();
    }
    function focusOption(button) {
        if (!button) return;
        button.focus({ preventScroll: true });
        const bounds = button.getBoundingClientRect();
        const viewport = results.getBoundingClientRect();
        if (bounds.top < viewport.top) results.scrollTop += bounds.top - viewport.top;
        else if (bounds.bottom > viewport.bottom) results.scrollTop += bounds.bottom - viewport.bottom;
    }
    panel.addEventListener('keydown', event => {
        if (event.isComposing) return;
        if (event.key === 'Escape') {
            event.preventDefault();
            event.stopPropagation();
            if (panel.dataset.uploadBusy === 'true') return;
            if (panel.dataset.uploadDraft === 'true') callback.invokeMethodAsync('CancelUploadDraft').catch(() => {});
            else close(true);
            return;
        }
        if (event.target.matches('.emoji-picker-upload input') && event.key === 'Enter') {
            event.preventDefault();
            event.stopPropagation();
            callback.invokeMethodAsync('SaveUpload').catch(() => {});
            return;
        }
        if (event.target.matches('.emoji-picker-search')) {
            if (event.key === 'ArrowDown' || event.key === 'Enter') {
                event.preventDefault();
                event.stopPropagation();
                if (event.key === 'Enter') options[0]?.click();
                else focusOption(options[0]);
            }
            return;
        }
        const current = options.indexOf(event.target);
        if (current < 0) return;
        const rowWidth = getComputedStyle(options[0].parentElement).gridTemplateColumns.split(' ').length;
        let next;
        if (event.key === 'ArrowRight') next = Math.min(options.length - 1, current + 1);
        else if (event.key === 'ArrowLeft') next = Math.max(0, current - 1);
        else if (event.key === 'ArrowDown') next = Math.min(options.length - 1, current + rowWidth);
        else if (event.key === 'ArrowUp') next = current < rowWidth ? -1 : current - rowWidth;
        else if (event.key === 'Home') next = 0;
        else if (event.key === 'End') next = options.length - 1;
        if (next === undefined) return;
        event.preventDefault();
        event.stopPropagation();
        if (next < 0) panel.querySelector('.emoji-picker-search')?.focus({ preventScroll: true });
        else focusOption(options[next]);
    }, { signal });
    document.addEventListener('pointerdown', event => {
        if (event.target instanceof Node && !panel.contains(event.target) && !anchor.contains(event.target))
            close(false);
    }, { signal, capture: true });
    document.addEventListener('focusin', event => {
        if (event.target instanceof Node && !panel.contains(event.target) && !anchor.contains(event.target))
            close(false);
    }, { signal });
    window.addEventListener('resize', fit, { signal, passive: true });
    window.addEventListener('scroll', event => {
        if (!(event.target instanceof Node) || !panel.contains(event.target)) fit();
    }, { signal, passive: true, capture: true });
    window.visualViewport?.addEventListener('resize', fit, { signal, passive: true });
    window.visualViewport?.addEventListener('scroll', fit, { signal, passive: true });
    const observer = new ResizeObserver(fit);
    observer.observe(anchor);
    observer.observe(panel);
    return { controller, panel, observer, fit, syncResults };
}
