import hashlib
import json
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


WIDTH, HEIGHT = 640, 480
PAPER = "#f8f4ea"
WHITE = "#fffdf8"
NAVY = "#193452"
COBALT = "#255aa5"
BLUE_FILL = "#eaf0fa"
PINE = "#2e5d4a"
GREEN_FILL = "#e9f3ed"
INK = "#283a4d"
MUTED = "#4a6075"
LINE = "#d9d0bf"
ROOT = Path(__file__).resolve().parents[4]
PUBLIC = ROOT / "web/wwwroot/images/characters/gitmejsen"
ANIMATIONS = PUBLIC / "animations"


def font(windows_name, linux_name, size):
    for candidate in (
        Path("C:/Windows/Fonts") / windows_name,
        Path("/usr/share/fonts/truetype/dejavu") / linux_name,
    ):
        if candidate.is_file():
            return ImageFont.truetype(candidate, size)
    raise FileNotFoundError(f"Installér {windows_name} eller {linux_name}.")


TITLE = font("segoeuib.ttf", "DejaVuSans-Bold.ttf", 39)
INTRO = font("segoeui.ttf", "DejaVuSans.ttf", 23)
LABEL = font("segoeuib.ttf", "DejaVuSans-Bold.ttf", 21)
CARD_LABEL = font("segoeuib.ttf", "DejaVuSans-Bold.ttf", 19)
BODY = font("segoeui.ttf", "DejaVuSans.ttf", 19)
CODE = font("consola.ttf", "DejaVuSansMono.ttf", 23)
TAG = font("consola.ttf", "DejaVuSansMono.ttf", 19)
ACTIVITY = Image.open(PUBLIC / "reactions/idea.png").convert("RGBA")
ACTIVITY.thumbnail((99, 99), Image.Resampling.LANCZOS)


def frame(title, subtitle, command, explanation):
    page = Image.new("RGB", (WIDTH, HEIGHT), PAPER)
    canvas = ImageDraw.Draw(page)
    canvas.text((36, 35), title, font=TITLE, fill=NAVY)
    canvas.text((37, 88), subtitle, font=INTRO, fill=MUTED)
    page.paste(ACTIVITY, (506, 20), ACTIVITY)
    canvas.rounded_rectangle((36, 130, 604, 346), radius=20, fill=WHITE, outline=LINE, width=2)
    canvas.rounded_rectangle((36, 366, 604, 450), radius=16, fill=NAVY)
    canvas.text((56, 379), command, font=CODE, fill=WHITE)
    assert canvas.textlength(explanation, font=BODY) < 526, explanation
    canvas.text((57, 414), explanation, font=BODY, fill="#e3edf8")
    return page, canvas


def arrow(canvas, start, finish, y, color):
    canvas.line((start, y, finish - 2, y), width=4, fill=color)
    canvas.polygon(((finish, y), (finish - 8, y - 6), (finish - 8, y + 6)), fill=color)


def area(canvas, x, title, value, selected):
    top, right, bottom = 186, x + 154, 289
    canvas.rounded_rectangle(
        (x, top, right, bottom),
        radius=12,
        fill=BLUE_FILL if selected else PAPER,
        outline=COBALT if selected else LINE,
        width=2,
    )
    canvas.text(((x + right) / 2, 219), title, font=CARD_LABEL, fill=NAVY, anchor="mm")
    canvas.text(((x + right) / 2, 253), value, font=TAG, fill=INK, anchor="mm")


def first_commit():
    title = "Første commit"
    subtitle = "Arbejdsmappe, staging og historik"
    steps = [
        ("git status", "README.md er klar i arbejdsmappen."),
        ("git add README.md", "git add lægger README.md i staging."),
        ('git commit -m "Tilføj README"', "Commit A gemmer indholdet fra staging."),
    ]
    frames = []
    for stage, (command, explanation) in enumerate(steps):
        page, canvas = frame(title, subtitle, command, explanation)
        area(canvas, 54, "Arbejdsmappe", "README.md", stage == 0)
        area(canvas, 243, "Staging", "README.md" if stage >= 1 else "Tom", stage == 1)
        area(canvas, 432, "Commit A", "README.md" if stage >= 2 else "Afventer", stage == 2)
        arrow(canvas, 216, 236, 238, COBALT if stage >= 1 else LINE)
        arrow(canvas, 405, 425, 238, COBALT if stage >= 2 else LINE)
        frames.append(page)
    return frames


def badge(canvas, label, center, node, above, secondary=False):
    x, y = center
    radius = 15
    half_width = canvas.textlength(label, font=TAG) / 2 + 12
    fill, color = (GREEN_FILL, PINE) if secondary else (BLUE_FILL, COBALT)
    end = node[1] - 23 if above else node[1] + 23
    edge = y + radius if above else y - radius
    canvas.line((x, edge, node[0], end), width=2, fill=color)
    canvas.rounded_rectangle((x - half_width, y - radius, x + half_width, y + radius), radius=11, fill=fill)
    canvas.text((x, y), label, font=TAG, fill=color, anchor="mm")


def link(canvas, first, second, color):
    canvas.line((*first, *second), fill=color, width=5)


def commit(canvas, point, letter, color=COBALT):
    x, y = point
    canvas.ellipse((x - 23, y - 23, x + 23, y + 23), fill=WHITE, outline=color, width=4)
    canvas.text(point, letter, font=LABEL, fill=NAVY, anchor="mm")


def new_branch():
    title, subtitle = "Opret en branch", "Samme udgangspunkt, eget spor"
    steps = [
        ("git log --oneline", "main peger på B."),
        ("git switch -c forsog", "main og forsog peger begge på B."),
        ('git commit -m "Afprøv ny idé"', "C gemmer indholdet fra staging. Kun forsog flytter."),
    ]
    a, b, c = (115, 229), (280, 229), (444, 267)
    frames = []
    for stage, (command, explanation) in enumerate(steps):
        page, canvas = frame(title, subtitle, command, explanation)
        link(canvas, a, b, COBALT)
        badge(canvas, "main", (280, 170), b, above=True)
        if stage == 1:
            badge(canvas, "forsog", (280, 292), b, above=False, secondary=True)
        if stage == 2:
            link(canvas, b, c, PINE)
            badge(canvas, "forsog", (444, 319), c, above=False, secondary=True)
            commit(canvas, c, "C", PINE)
        commit(canvas, a, "A")
        commit(canvas, b, "B")
        frames.append(page)
    return frames


def merge_branches():
    title, subtitle = "Flet branches", "Et merge med to forældre"
    steps = [
        ("git switch main", "main er aktiv. Branchene har hver en ny commit."),
        ("git merge --no-ff forsog", "Git fletter ændringerne fra B og C."),
        ("git log --graph --oneline --all", "main peger på M. M har B og C som forældre."),
    ]
    a, b, c, m = (115, 237), (283, 198), (283, 276), (463, 237)
    frames = []
    for stage, (command, explanation) in enumerate(steps):
        page, canvas = frame(title, subtitle, command, explanation)
        link(canvas, a, b, COBALT)
        link(canvas, a, c, PINE)
        if stage == 2:
            link(canvas, b, m, COBALT)
            link(canvas, c, m, PINE)
            badge(canvas, "main", (463, 171), m, above=True)
            commit(canvas, m, "M")
        else:
            badge(canvas, "main", (283, 153), b, above=True)
        badge(canvas, "forsog", (283, 323), c, above=False, secondary=True)
        commit(canvas, a, "A")
        commit(canvas, b, "B")
        commit(canvas, c, "C", PINE)
        frames.append(page)
    return frames


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def export(slug, title, topic, alt, frames):
    gif = ANIMATIONS / f"{slug}.gif"
    poster = ANIMATIONS / f"{slug}.webp"
    duration = [2600, 3300, 4500]
    palette_frames = [
        frame.quantize(colors=256, dither=Image.Dither.NONE) for frame in frames
    ]
    palette_frames[0].save(
        gif,
        save_all=True,
        append_images=palette_frames[1:],
        duration=duration,
        disposal=2,
        loop=0,
        optimize=True,
    )
    frames[-1].save(poster, lossless=True, method=6)
    with Image.open(gif) as result:
        assert result.size == (WIDTH, HEIGHT) and result.n_frames == len(frames)
        for index, time in enumerate(duration):
            result.seek(index)
            assert result.info["duration"] == time
            result.load()
    with Image.open(poster) as result:
        assert result.size == (WIDTH, HEIGHT) and result.format == "WEBP"
        result.verify()
    url = "/images/characters/gitmejsen/animations"
    return {
        "id": slug,
        "title": title,
        "topic": topic,
        "alt": alt,
        "width": WIDTH,
        "height": HEIGHT,
        "frameCount": len(frames),
        "frameDurationsMs": duration,
        "gif": f"{url}/{gif.name}",
        "poster": f"{url}/{poster.name}",
        "gifSha256": sha256(gif),
        "posterSha256": sha256(poster),
    }


def main():
    ANIMATIONS.mkdir(parents=True, exist_ok=True)
    entries = [
        export(
            "foerste-commit",
            "Første commit",
            "Arbejdsmappe, staging og historik",
            "README.md føjes til staging med git add. Git commit gemmer det valgte indhold som commit A.",
            first_commit(),
        ),
        export(
            "ny-branch",
            "Opret en branch",
            "Samme udgangspunkt, eget spor",
            "main og forsog peger først på B. Et nyt commit C flytter kun forsog videre.",
            new_branch(),
        ),
        export(
            "flet-branches",
            "Flet branches",
            "Et merge med to forældre",
            "Commit B og C har fælles forælder A. Merge-commit M får B og C som forældre, og main flytter til M.",
            merge_branches(),
        ),
    ]
    manifest = {
        "generator": "Pillow",
        "mascot": "/images/characters/gitmejsen/reactions/idea.png",
        "animations": entries,
    }
    Path(__file__).with_name("manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    for entry in entries:
        print(f"{entry['id']}: {entry['width']}x{entry['height']}, {entry['frameCount']} billeder")


if __name__ == "__main__":
    main()
