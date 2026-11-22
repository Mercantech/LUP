export function createCoverUrl(bytes) {
    return URL.createObjectURL(new Blob([bytes], { type: 'image/webp' }));
}

export function revokeCoverUrl(url) {
    URL.revokeObjectURL(url);
}
