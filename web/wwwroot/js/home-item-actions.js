export function open(panel, itemId, receiver) {
    if (!panel?.isConnected) return null;
    const stage = panel.closest('.room-stage');
    const scroller = stage.querySelector('.home-board-scroll');
    const selector = `.furniture[data-item-id="${CSS.escape(itemId)}"] .object-handle`;
    const opener = document.activeElement;
    const fromList = opener?.matches('.item-label, [data-inspect-alias]');
    const anchor = () => fromList && opener.isConnected ? opener : scroller.querySelector(selector);
    const controller = new AbortController();
    const {signal} = controller;
    const viewport = window.visualViewport;
    let closing = false;

    function restoreFocus() {
        const trigger = opener?.matches('button') && opener.isConnected ? opener : anchor();
        trigger?.focus({preventScroll: true});
    }

    function close(restore) {
        if (closing || signal.aborted) return;
        closing = true;
        if (restore) restoreFocus();
        receiver.invokeMethodAsync('CloseFromBrowser').catch(() => {});
    }

    function reposition() {
        const trigger = anchor();
        if (!trigger || !trigger.getClientRects().length) { close(false); return; }
        const rect = trigger.getBoundingClientRect();
        const edge = 8, gap = 7;
        const left = (viewport?.offsetLeft ?? 0) + edge, top = (viewport?.offsetTop ?? 0) + edge;
        const rootRight = document.documentElement.getBoundingClientRect().right;
        const visibleRight = viewport ? viewport.offsetLeft + viewport.width : window.innerWidth;
        const right = Math.min(rootRight, visibleRight) - edge;
        const bottom = top + (viewport?.height ?? window.innerHeight) - edge * 2;
        const clip = fromList ? null : scroller.getBoundingClientRect();
        const editing = panel.contains(document.activeElement) && document.activeElement.matches('input, textarea, select');
        if (!editing && (rect.bottom < Math.max(top - edge, clip?.top ?? -Infinity)
            || rect.top > Math.min(bottom + edge, clip?.bottom ?? Infinity)
            || rect.right < Math.max(left - edge, clip?.left ?? -Infinity)
            || rect.left > Math.min(right + edge, clip?.right ?? Infinity))) {
            close(false);
            return;
        }
        panel.style.maxWidth = `${right-left}px`;
        panel.style.maxHeight = `${bottom-top}px`;
        const {width, height} = panel.getBoundingClientRect();
        const above = rect.top - top, below = bottom - rect.bottom;
        let x = rect.left + (rect.width - width) / 2;
        let y = above >= height + gap || above > below ? rect.top - gap - height : rect.bottom + gap;
        if (Math.max(above, below) < height + gap && (right - rect.right >= width + gap || rect.left - left >= width + gap)) {
            x = right - rect.right >= width + gap ? rect.right + gap : rect.left - gap - width;
            y = rect.top + (rect.height - height) / 2;
        }
        panel.style.left = `${Math.max(left, Math.min(x, right-width))}px`;
        panel.style.top = `${Math.max(top, Math.min(y, bottom-height))}px`;
        panel.dataset.ready = '';
    }

    const outside = event => {
        const target = event.target;
        if (panel.contains(target) || target === opener || target.closest?.('.object-handle')) return;
        close(false);
    };
    document.addEventListener('pointerdown', outside, {capture:true, signal});
    document.addEventListener('focusin', outside, {signal});
    document.addEventListener('keydown', event => {
        if (event.key !== 'Escape') return;
        event.preventDefault();
        event.stopPropagation();
        close(true);
    }, {capture:true, signal});
    window.addEventListener('scroll', event => {
        if (!panel.contains(event.target)) reposition();
    }, {capture:true, passive:true, signal});
    window.addEventListener('resize', reposition, {passive:true, signal});
    viewport?.addEventListener('scroll', reposition, {passive:true, signal});
    viewport?.addEventListener('resize', reposition, {passive:true, signal});
    const observer = new ResizeObserver(reposition);
    observer.observe(scroller);
    observer.observe(panel);
    const trigger = anchor();
    if (trigger) observer.observe(trigger);

    panel.showPopover?.();
    reposition();
    if (!closing) {
        const action = panel.querySelector('.item-action-content button:not(:disabled), .item-action-content summary');
        (action ?? panel.querySelector('.close-actions'))?.focus({preventScroll:true});
    }
    return {
        reposition,
        restoreFocus,
        dispose() {
            controller.abort();
            observer.disconnect();
            if (panel.isConnected && panel.hidePopover && panel.matches(':popover-open')) panel.hidePopover();
        }
    };
}
