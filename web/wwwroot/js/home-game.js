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
    board.querySelector(`[data-x="${x}"][data-y="${y}"]`)?.focus({ preventScroll: true });
}

export function focusPlacement(room, x, y) {
    const cell = room.querySelector(`button[data-x="${x}"][data-y="${y}"]`);
    if (cell) {
        room.scrollIntoView({block: "nearest"});
        cell.focus({preventScroll: true});
    }
}

export function keepArrowKeysOnBoard(board) {
    board.addEventListener("keydown", event => {
        if (["ArrowUp", "ArrowRight", "ArrowDown", "ArrowLeft"].includes(event.key)) {
            event.preventDefault();
        }
    });
}
