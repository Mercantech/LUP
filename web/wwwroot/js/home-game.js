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

export function focusCell(board, x, y) {
    board.querySelector(`[data-x="${x}"][data-y="${y}"]`)?.focus({ preventScroll: true });
}

export function keepArrowKeysOnBoard(board) {
    board.addEventListener("keydown", event => {
        if (["ArrowUp", "ArrowRight", "ArrowDown", "ArrowLeft"].includes(event.key)) {
            event.preventDefault();
        }
    });
}
