export function createCoverUrl(bytes) {
    return URL.createObjectURL(new Blob([bytes], { type: 'image/webp' }));
}

export function revokeCoverUrl(url) {
    URL.revokeObjectURL(url);
}

export function focusCoverPosition(slider) {
    const menu = slider.closest(".notion-workspace")?.querySelector(".notion-document-more[open]");
    if (menu) menu.open = false;
    slider.focus();
}

export function focusCoverMenu(banner) {
    banner.closest(".notion-workspace")?.querySelector(".notion-document-more > summary")?.focus();
}

export function downloadCover(bytes, filename) {
    const url = createCoverUrl(bytes);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => revokeCoverUrl(url), 30_000);
}
