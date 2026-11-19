const key = (subject, language) => `lup.home-game.draft.v1.${encodeURIComponent(subject)}.${language}`;

export function readDraft(subject, language) {
    return localStorage.getItem(key(subject, language));
}

export function writeDraft(subject, language, source) {
    localStorage.setItem(key(subject, language), source);
}

export function removeDraft(subject, language) {
    localStorage.removeItem(key(subject, language));
}

export function openResidentRoom(callback, subject, teacher, interval) {
    const storageKey = `lup.home-game.resident.v1.${teacher ? "teacher" : "student"}.${encodeURIComponent(subject)}`;
    let checkpoint, timer, closed = false, ticking = false, lastSaved = 0;

    function flush() {
        if (!checkpoint) return;
        try { localStorage.setItem(storageKey, JSON.stringify(checkpoint)); } catch { }
        checkpoint = null;
        lastSaved = Date.now();
    }

    function schedule() {
        clearTimeout(timer);
        if (!closed && !ticking && !document.hidden) timer = setTimeout(tick, interval);
    }
    async function tick() {
        if (closed || document.hidden) return;
        ticking = true;
        try { await callback.invokeMethodAsync("AdvanceResidentAsync"); }
        finally { ticking = false; schedule(); }
    }
    function visibilityChanged() {
        if (document.hidden) flush();
        schedule();
    }
    document.addEventListener("visibilitychange", visibilityChanged);
    window.addEventListener("pagehide", flush);
    schedule();
    return {
        read() {
            try { return JSON.parse(localStorage.getItem(storageKey)); }
            catch { return null; }
        },
        remember(value) {
            checkpoint = value;
            if (Date.now() - lastSaved >= 5000) flush();
        },
        close() {
            closed = true;
            clearTimeout(timer);
            document.removeEventListener("visibilitychange", visibilityChanged);
            window.removeEventListener("pagehide", flush);
            flush();
        }
    };
}

export function focusCell(board, x, y) {
    const cell = board.querySelector(`button[data-x="${x}"][data-y="${y}"]`);
    if (!cell) return;
    const scroller = cell.closest(".home-board-scroll");
    if (scroller) {
        const bounds = scroller.getBoundingClientRect();
        const tile = cell.getBoundingClientRect();
        const left = Math.min(0, tile.left - bounds.left) + Math.max(0, tile.right - bounds.right);
        const top = Math.min(0, tile.top - bounds.top) + Math.max(0, tile.bottom - bounds.bottom);
        if (left || top) scroller.scrollBy({left, top, behavior: "instant"});
    }
    cell.focus({preventScroll: true});
}

export function focusPlacement(room, x, y) {
    const stage = room.querySelector(".room-stage.with-placement");
    if (!stage) return;
    const viewport = window.visualViewport;
    const top = viewport?.offsetTop ?? 0;
    const bottom = top + (viewport?.height ?? window.innerHeight);
    const dock = stage.querySelector(".placement-dock");
    const minimum = Math.min(bottom - top, (dock?.offsetHeight ?? 0) + 80);
    stage.style.removeProperty("--placement-space");
    const bounds = stage.getBoundingClientRect();
    if (bounds.bottom < top + minimum || bounds.top > bottom - minimum) {
        // Opening from the shop or inventory can leave the whole room off screen.
        stage.style.setProperty("--placement-space", `${bottom - top}px`);
        stage.scrollIntoView({block: "center", inline: "nearest", behavior: "instant"});
    }
    // Include the rows already above the viewport when placing in a tall room.
    const available = bottom - stage.getBoundingClientRect().top;
    stage.style.setProperty("--placement-space", `calc(100dvh + ${available - window.innerHeight}px)`);
    focusCell(stage, x, y);
}

export function keepArrowKeysOnBoard(board) {
    board.addEventListener("keydown", event => {
        if (["ArrowUp", "ArrowRight", "ArrowDown", "ArrowLeft"].includes(event.key)) {
            event.preventDefault();
        }
    });
}
