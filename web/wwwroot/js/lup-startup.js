(() => {
    const cast = [
        {
            name: "LUPP",
            art: "images/characters/lupp/reactions/encouragement.webp",
            line: "»Jeg tester lige startknappen. En god bane skal også have en god begyndelse.«"
        },
        {
            name: "Raymond",
            art: "images/characters/raymond/reactions/join.webp",
            line: "»Jeg laver lige en JOIN. En elefant glemmer aldrig den primære nøgle.«"
        },
        {
            name: "Gummianden",
            art: "images/characters/gummianden/reactions/thinking.webp",
            line: "»Jeg bad koden forklare sig. Den begyndte helt fra den første variabel.«"
        },
        {
            name: "Bilbo Backend",
            art: "images/characters/bilbo/reactions/idempotency.webp",
            line: "»Jeg tæller syltetøjsglassene. Ét klik skal kun bestille ét glas.«"
        },
        {
            name: "Kamæleonen",
            art: "images/characters/chameleon/reactions/patient.webp",
            line: "»Jeg stemmer lige banjoen. Så lyder det rigtigt, når modellen rammer plet.«"
        },
        {
            name: "Næsebladet",
            art: "images/characters/bat/reactions/wave.webp",
            line: "»Jeg har sendt et signal. Ekkoet skal lige finde tilbage.«"
        },
        {
            name: "Cyberbien",
            art: "images/characters/bee/reactions/thumbs-up.webp",
            line: "»Jeg kigger lige i loggen. Den summer lidt mere end normalt.«"
        },
        {
            name: "Docker",
            art: "images/characters/whale/reactions/encouragement.webp",
            line: "»Den sidste container er på vej i havn. Så kan vi gå ombord.«"
        }
    ];

    const rotationMs = 7000;
    const motion = window.matchMedia("(prefers-reduced-motion: reduce)");
    window.lupStartup = {
        preview: () => ({ characters: cast, rotationMs, reducedMotion: motion.matches }),
        canRotate: () => !document.hidden && !motion.matches
    };

    const splash = document.querySelector("[data-lup-startup]");
    if (!splash) return;

    const portrait = splash.querySelector("[data-startup-portrait]");
    const quote = splash.querySelector("[data-startup-quote]");
    const character = splash.querySelector("[data-startup-character]");
    let index = Math.floor(Math.random() * cast.length);
    function show(entry) {
        if (portrait.getAttribute("src") === entry.art) return;
        const image = new Image();
        image.onload = () => {
            if (!splash.isConnected || cast[index] !== entry) return;
            portrait.src = entry.art;
            quote.textContent = entry.line;
            character.textContent = entry.name;
        };
        image.src = entry.art;
    }

    show(cast[index]);
    const timer = window.setInterval(() => {
        if (!window.lupStartup.canRotate()) return;
        index = (index + 1) % cast.length;
        show(cast[index]);
    }, rotationMs);
    new MutationObserver((_, observer) => {
        if (splash.isConnected) return;
        window.clearInterval(timer);
        observer.disconnect();
    }).observe(splash.parentElement, { childList: true });
})();
