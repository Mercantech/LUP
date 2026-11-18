const point = (x, y) => ({ x, y });
const wall = (x, y, horizontal, open = false) => ({ edge: { x, y, horizontal }, open });
const piece = (id, definitionId, alias, name, placement = null, extras = {}) =>
    ({ id, definitionId, alias, name, placement, starter: true, goldPaid: 0, ...extras });

function homeGameFixture({ gold = 100, isTeacherSandbox = false, selectedAvatarId = "lupp", lampInStorage = false } = {}) {
    const cells = Array.from({ length: 120 }, (_, i) => point(i % 12, Math.floor(i / 12)));
    const walls = [
        ...Array.from({ length: 12 }, (_, x) => [wall(x, 0, true), wall(x, 10, true)]).flat(),
        ...Array.from({ length: 10 }, (_, y) => [wall(0, y, false, y === 4), wall(12, y, false)]).flat()
    ];
    return {
        layout: {
            revision: 1,
            grid: { columns: 12, rows: 10, entry: point(0, 4), walls, rooms: { living_room: cells } },
            items: [
                piece("desk", "desk_basic", "reading_desk", "Skrivebord", { anchor: point(6, 3), rotation: 0 }, { role: "primary_reading_desk" }),
                piece("controller", "home_controller", "home_controller", "Hjemmecontroller",
                    { anchor: point(9, 0), rotation: 180, wall: wall(9, 0, true).edge }),
                piece("lamp", "lamp_basic", "desk_lamp", "Gulvlampe", lampInStorage ? null : { anchor: point(5, 4), rotation: 0 }),
                piece("sofa-starter", "sofa_basic", "sofa", "Sofa", { anchor: point(1, 8), rotation: 0 }),
                piece("rug-starter", "rug_basic", "rug", "Tæppe", { anchor: point(2, 8), rotation: 0 }),
                piece("daylight", "daylight_sensor", "daylight", "Dagslyssensor"),
                piece("presence", "presence_sensor", "presence", "Tilstedeværelsessensor"),
                piece("switch", "switch_basic", "auto_enabled", "Kontakt")
            ]
        },
        gold, completedTasks: 0, drafts: [], progress: [], canUndo: false, lastRun: null,
        runnerAvailable: true, highestAvailableTask: 0,
        reflection: "", classrooms: [], isTeacherSandbox, selectedAvatarId
    };
}

module.exports = { homeGameFixture, piece, point };
