// Run with Node from any directory after editing the artwork or badge catalogue.
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';

// Each miniature has a 64 × 64 canvas. The two inks colour the enamel beneath it.
const designs = [
    ['first-submission', '#205951', '#77ba9e', `<path fill="#fff0c8" d="m15 8 28-3 8 8 4 37-37 5z"/><path fill="#f0c36b" d="m42 5 1 10 8-2"/><path fill="none" d="m22 22 19-2m-18 10 13-2"/><circle cx="45" cy="45" r="12" fill="#478967"/><path stroke="white" fill="none" d="m39 45 4 4 9-10"/>`],
    ['wordle-win', '#34477a', '#889dd7', `<g transform="rotate(-10 32 32)"><rect x="6" y="13" width="25" height="25" rx="4" fill="#f6c655"/><path fill="none" d="m12 31 6-13 6 13m-10-4h8"/><rect x="27" y="27" width="28" height="28" rx="4" fill="#4eae7b"/><path stroke="#fffbdf" fill="none" stroke-width="4" d="m33 41 6 6 10-13"/></g>`],
    ['friday', '#503352', '#c387a7', `<path fill="#28374d" d="m6 53 5-26 14-11h14l14 11 5 26q-26 12-52 0z"/><path fill="#fffae5" d="m21 19 11 31 12-31-7-7H28z"/><path fill="#445b74" d="m21 16-6 12 7 3-3 5 13 14zm22 0 6 12-7 3 3 5-13 14z"/><path fill="#f89584" d="m20 19 12 5 12-5v12l-12-5-12 5z"/><rect x="29" y="21" width="6" height="7" rx="2" fill="#d64d5e"/><path fill="#ffc1a0" d="m43 39 6-5 3 5z"/>`],
    ['hawaii', '#1b6460', '#f0af80', `<path fill="#54a69b" d="m20 12 8-3 9 1 9 3 13 14-9 9-5-4 2 22q-14 5-31 0l2-23-6 5-8-9z"/><path fill="#ffdf91" d="m22 12 6-4 5 9-6 7zm17-2 5 4-6 10-5-7z"/><path fill="none" d="m33 19 1 33"/><g fill="#ffaf87" stroke-width="1.4"><path d="M0-2c-7-9-10 2-4 3-8 4 1 10 4 4 2 7 11 1 5-3 8-3 1-10-3-5z" transform="translate(23 35) scale(.8)"/><path d="M40 42q-6-8-7-2 0 3 4 4-6 3-2 6 4 2 6-4 3 5 6 2 3-4-3-5 5-5 1-6-4-1-5 5z"/></g>`],
    ['loop', '#513675', '#ac88cb', `<path fill="#e8cdff" d="M10 30C8 9 35 2 49 17l5-5 2 17-17-2 5-5C32 10 18 18 18 29Z"/><path fill="#ffcd72" d="M54 35c1 20-25 28-40 13l-5 5-2-17 17 2-5 5c11 11 27 4 27-9z"/><path fill="#fff6dc" d="m28 24 13 8-13 8z"/>`],
    ['playtest', '#424778', '#91a3ce', `<path fill="#7e80bd" d="m41 39 14 12-5 6-14-14z"/><circle cx="27" cy="26" r="19" fill="#ffdb99"/><circle cx="27" cy="26" r="14" fill="#b4dded"/><path fill="#fff8e4" d="M14 26q12-17 26 0-13 17-26 0z"/><circle cx="27" cy="26" r="5" fill="#5f4a94"/><circle cx="28" cy="24" r="1.7" fill="white" stroke="none"/>`],
    ['gdd', '#54395d', '#dd96a3', `<path fill="#bd9de6" d="M17 17c7-3 8 1 15 1s10-4 16 0c5 4 11 22 9 28-3 11-13 5-20-4H27C17 53 9 57 7 47c-2-7 3-26 10-30z"/><path fill="#fff4cf" d="M18 23h5v5h5v5h-5v5h-5v-5h-5v-5h5z"/><circle cx="46" cy="26" r="4" fill="#ffcc77"/><circle cx="40" cy="34" r="4" fill="#ffa69f"/><path fill="none" d="m29 38 6 0"/>`],
    ['mqtt', '#164e58', '#69aba9', `<path d="M15 43 31 23l18 20" stroke="#fdd187" fill="none" stroke-width="4"/><circle cx="32" cy="23" r="8" fill="#9ddfc8"/><rect x="4" y="39" width="21" height="13" rx="3" fill="#b3e5ee"/><rect x="40" y="39" width="20" height="13" rx="3" fill="#b3e5ee"/><path fill="none" d="M24 12q8-7 16 0M20 7q12-10 24 0M10 46h5m31 0h5"/>`],
    ['cpp', '#234d51', '#9dc5a3', `<path fill="none" stroke="#ffce79" stroke-width="4" d="M18 8v8m9-8v8m10-8v8m9-8v8M18 48v8m9-8v8m10-8v8m9-8v8M6 22h9m-9 10h9m-9 10h9m34-20h9m-9 10h9m-9 10h9"/><rect x="13" y="14" width="38" height="36" rx="6" fill="#4d9996"/><rect x="17" y="18" width="30" height="28" rx="4" fill="#d2eee0"/><path d="M30 26q-10-4-10 7 0 8 10 5m3-7h5m-2.5-3v6m4-3h5m-2.5-3v6" fill="none"/>`],
    ['api', '#213d64', '#7ba6d5', `<path d="M18 19v11h28v16m-14-16v15" fill="none" stroke="#ffdc86" stroke-width="4"/><rect x="5" y="6" width="30" height="17" rx="4" fill="#8ac4e9"/><path fill="none" d="m11 13 4 3-4 3m9-2h7"/><rect x="17" y="41" width="21" height="13" rx="3" fill="#ffcd83"/><rect x="41" y="41" width="17" height="13" rx="3" fill="#a8ded9"/>`],
    ['sql', '#245149', '#85baa0', `<path fill="#66b8a8" d="M9 16h39v30c0 12-39 12-39 0z"/><ellipse cx="28.5" cy="16" rx="19.5" ry="8" fill="#d4eed0"/><path d="M9 26c0 11 39 11 39 0M9 37c0 11 39 11 39 0" fill="none"/><path fill="#ffcf75" d="m40 41 8-5 10 5v12l-9 6-9-6z"/><path fill="none" d="m41 42 8 5 8-5m-8 5v9"/>`],
    ['git', '#31534a', '#9ac5a4', `<g fill="none" stroke-linecap="round"><path stroke="#172b36" stroke-width="8" d="M20 52V12m0 29c0-13 24-7 24-23"/><path stroke="#9fd4ae" stroke-width="4" d="M20 52V12m0 29c0-13 24-7 24-23"/></g><g fill="#fff0ae"><circle cx="20" cy="12" r="6"/><circle cx="20" cy="51" r="6"/><circle cx="44" cy="15" r="6"/></g><circle cx="20" cy="38" r="4" fill="#94e2c7"/>`],
    ['security', '#283957', '#8499c5', `<path fill="#9bd2eb" d="M32 5q11 7 22 9c1 22-6 34-22 44C16 49 9 36 10 14q11-2 22-9z"/><path fill="#506d91" d="M32 12q9 5 16 6c0 15-5 25-16 33-11-8-16-18-16-33z"/><path d="M25 29v-4q0-13 14 0v4" stroke="#fff0cb" stroke-width="4" fill="none"/><rect x="21" y="27" width="22" height="17" rx="4" fill="#ffcf79"/><path d="M32 34v5"/>`],
    ['ui', '#404668', '#aaa4d3', `<rect x="5" y="10" width="51" height="38" rx="5" fill="#ebf2e8"/><path fill="#78a9ce" d="M5 17q0-7 5-7h41q5 0 5 7v4H5z"/><path d="M11 16h1m5 0h1m5 0h1"/><path fill="#a2cabd" d="M11 27h11v15H11z"/><path d="M27 29h18m-18 6h10" fill="none"/><path fill="#ffce7b" d="m39 33 18 11-8 2-3 10z"/>`],
    ['docs', '#435055', '#b7baa0', `<path fill="#fae8b8" d="M4 14q14-5 28 4 13-9 28-4v37q-14-3-28 5-14-8-28-5z"/><path fill="none" d="M32 18v37M10 25l15 3M10 32l12 2m-12 5 10 1"/><path fill="#8ec9c5" d="m43 13 7-2v20l-4-3-3 5z"/><path d="m38 42 4 3 8-8" fill="none"/>`],
];

const catalog = readFileSync(new URL('../shared/Badges.cs', import.meta.url), 'utf8');
const levels = new Map([...catalog.matchAll(/^\s*new\("([^"]+)",\s*"[^"]+",\s*"[^"]+",\s*"[^"]+",\s*"[^"]+",\s*(\d+)(?:,\s*true)?\),/gm)].map(m => [m[1], Number(m[2])]));
if (levels.size !== designs.length || designs.some(([id]) => !levels.has(id))) throw Error('Badge catalogue and artwork must cover the same IDs.');

const frames = [
    ['#ffddab','#976137','M60 18C83 18 102 36 102 61S82 105 60 109C38 105 18 86 18 61S37 18 60 18Z'],
    ['#e4f4ff','#627c99','M60 14 94 28 103 53 96 84 80 102 60 112 40 102 24 84 17 53 26 28Z'],
    ['#ffdf7e','#a66d2c','M60 11 81 20 99 27 106 55 99 84 82 103 60 115 38 103 21 84 14 55 21 27 39 20Z'],
    ['#a7f0f4','#39738c','M60 8 72 18 96 23 104 40 109 60 99 86 83 105 60 116 37 105 21 86 11 60 16 40 24 23 48 18Z'],
    ['#ffe8a4','#ab724b','M60 5 75 17 96 18 105 34 112 58 103 87 82 108 60 118 38 108 17 87 8 58 15 34 24 18 45 17Z'],
];
const eventFrame = ['#ffe4a4','#a0603b','M60 9Q69 19 79 17Q84 28 96 29Q96 41 106 47Q101 57 106 68Q98 75 97 87Q85 89 79 100Q68 99 60 110Q51 100 40 101Q35 91 23 88Q23 76 14 69Q19 58 14 47Q24 40 24 29Q36 27 41 17Q52 18 60 9Z'];
const diamond = (x,y,r) => '<path d="M'+x+' '+(y-r)+'l'+r+' '+r+'-'+r+' '+r+'-'+r+'-'+r+'Z"/>';
function medal([id, dark, light, drawing], rank, single) {
    const [shine, shade, outline] = single ? eventFrame : frames[rank-1];
    const ribbons = single || rank >= 3 ? '<path d="m29 83-7 34 13-5 7 8 9-29m18 0 9 29 7-8 13 5-7-34" fill="'+dark+'" stroke="#273242" stroke-width="2"/>' : '';
    const wings = !single && rank >= 4 ? '<g fill="url(#metal)" stroke="#36404e" stroke-width="1.8"><path id="wing" d="M24 47 4 33l3 15 12 10-14-6 5 14 13 8-10-2 8 13 9 4Z"/><use href="#wing" transform="translate(120 0) scale(-1 1)"/></g>' : '';
    const crown = !single && rank === 5 ? '<path d="m45 18-4-13 11 6 8-10 8 10 11-6-4 13z" fill="url(#metal)" stroke="#69503d" stroke-width="2"/>' : '';
    const band = single ? '<path d="m48 85 8 4m8 0 8-4" stroke="'+shine+'" fill="none" stroke-width="2.5"/>'+diamond(60,88,3) : Array.from({length:rank},(_,i)=>diamond(60+(i-(rank-1)/2)*8,89-Math.abs(i-(rank-1)/2),3)).join('');
    return '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 124" width="120" height="124">\n'+
        '<defs><linearGradient id="metal" x2=".77" y2="1" gradientUnits="objectBoundingBox"><stop stop-color="'+shine+'"/><stop offset=".35" stop-color="'+shade+'"/><stop offset=".52" stop-color="'+shine+'"/><stop offset="1" stop-color="'+shade+'"/></linearGradient><linearGradient id="enamel" x2=".45" y2="1"><stop stop-color="'+light+'"/><stop offset="1" stop-color="'+dark+'"/></linearGradient></defs>\n'+
        ribbons+wings+'<path d="'+outline+'" fill="url(#metal)" stroke="#323843" stroke-width="2.3"/>\n'+
        '<path d="'+outline+'" transform="translate(60 61) scale(.88) translate(-60 -61)" fill="url(#enamel)" stroke="'+dark+'" stroke-width="2"/>\n'+
        '<path d="'+outline+'" transform="translate(60 61) scale(.80) translate(-60 -61)" fill="none" stroke="'+shine+'" stroke-width="1.1" opacity=".75"/>\n'+
        '<g transform="translate(28 25)" stroke="#293541" stroke-width="2.4" stroke-linejoin="round" stroke-linecap="round">'+drawing+'</g>\n'+
        '<g fill="'+shine+'">'+band+'</g>'+crown+'</svg>\n';
}
const root = new URL('../web/wwwroot/images/badges/', import.meta.url);
for (const design of designs) {
    const id = design[0], count = levels.get(id);
    if (count !== 1 && count !== frames.length) throw Error('Unknown badge level count for '+id);
    mkdirSync(root,{recursive:true});
    for (let rank=1;rank<=count;rank++) {
        const file = count === 1 ? id : id+'-'+rank;
        writeFileSync(new URL(file+'.svg',root), medal(design,rank,count === 1));
    }
}
