let socket, retry, generation = 0;
export function disconnect() {
    generation++;
    clearTimeout(retry);
    if (socket) { socket.close(); socket = undefined; }
}
export function connect(dotnet, endpoint, code, key) {
    disconnect();
    const current = generation;
    const url = new URL(endpoint);
    url.protocol = url.protocol === "https:" ? "wss:" : "ws:";
    let failures = 0;
    function status(value) { if (current === generation) dotnet.invokeMethodAsync("ConnectionStatus", value).catch(() => {}); }
    function open() {
        if (current !== generation) return;
        status("Forbinder…");
        const ws = new WebSocket(url);
        socket = ws;
        ws.onopen = () => { if (current === generation) ws.send(JSON.stringify({code, key})); };
        ws.onmessage = event => {
            if (current !== generation) return;
            failures = 0;
            dotnet.invokeMethodAsync("ReceiveSnapshot", event.data).catch(() => {});
        };
        ws.onclose = () => {
            if (current !== generation) return;
            if (++failures > 5) { status("Ingen forbindelse. Kontrollér rumkode og nøgle."); return; }
            status("Forbindelsen mistet. Prøver igen…");
            retry = setTimeout(open, Math.min(15000, 1000 * 2 ** failures));
        };
        ws.onerror = () => ws.close();
    }
    open();
}
