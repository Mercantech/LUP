"""Generate Bilbo's original Foley and local Danish Røst lines."""

import argparse
import hashlib
import importlib.metadata
import json
import math
import os
from pathlib import Path
import random
import struct
import subprocess
import sys
from tempfile import TemporaryDirectory
import wave


RATE = 44100
MODEL = "CoRal-project/roest-v3-chatterbox-500m"
SFX_SOURCE = "original-bilbo-wood-glass-berry-synthesis"
SPEECH_SOURCE = "local-roest-chatterbox-bilbo-merchant"
VOICE = "Bilbo Backend · varm og underspillet købmand"
SETTINGS = {
    "language_id": "da",
    "temperature": 0.62,
    "cfg_weight": 0.5,
    "exaggeration": 0.5,
    "repetition_penalty": 2.0,
    "top_p": 0.95,
}
REFERENCE_LINES = (
    "Godmorgen. Jeg har sat de tomme glas frem og tjekket alle bestillingerne.",
    "Kom bare nærmere. Vi tager én ting ad gangen, og så bliver det godt.",
)
PHRASES = (
    ("phrase_1_welcome", "welcome", "Velkommen til boden. Vil du smage brombær?"),
    ("phrase_2_start", "start", "Først en bestilling, så åbner vi låget."),
    ("phrase_3_success", "success", "Sådan! Præcis ét glas til hver bestilling."),
    ("phrase_4_retry", "retry", "Et øjeblik, jeg tjekker lige ordren."),
    ("phrase_5_milestone", "milestone", "Hele køen er ekspederet. Smukt arbejde!"),
)
REFERENCE_FILTER = (
    "aresample=44100,asetrate=45423,aresample=44100,atempo=0.95,"
    "highpass=f=75,equalizer=f=380:t=q:w=1.2:g=1.8,treble=f=4100:g=-1.5"
)
FINISH_FILTER = (
    "aresample=44100,highpass=f=70,equalizer=f=420:t=q:w=1.25:g=1.5,"
    "equalizer=f=2300:t=q:w=1.3:g=1.0,"
    "acompressor=threshold=0.15:ratio=2:attack=9:release=140:makeup=1.22,"
    "alimiter=limit=0.75:level=false:latency=1,"
    "afade=t=in:d=0.006,areverse,afade=t=in:d=0.010,areverse"
)

# Cue recipes. Each event sets the instrument, onset, frequency and weight.
# The wooden counter, glass jars, brass lids, paper orders and jam are separate
# sound sources. Footsteps use soft pads because Bilbo is barefoot.
RECIPES = {
    "idle": ("Boden falder til ro", 0.52, [("cloth", .01, 560, .34), ("wood", .16, 150, .08)]),
    "blink": ("Lille blink", 0.14, [("drop", .014, 680, .44)]),
    "wave": ("Hilsen fra boden", 0.67, [("swish", .01, 720, .23), ("cloth", .13, 760, .20), ("tin", .38, 390, .13)]),
    "think": ("En bestilling overvejes", 0.84, [("wood", .03, 218, .30), ("paper", .21, 1550, .24), ("tin", .51, 455, .18)]),
    "talk": ("Små forklarende fagter", 0.76, [("wood", .01, 294, .38), ("wood", .20, 336, .25), ("wood", .43, 272, .22)]),
    "point": ("Her er ordren", 0.32, [("paper", .01, 1600, .26), ("tin", .08, 620, .22)]),
    "celebrate": ("En kø er ekspederet", 1.14, [("glass", .01, 525, .38), ("glass", .15, 656, .31), ("glass", .30, 790, .34), ("tin", .45, 975, .19)]),
    "encourage": ("En venlig gestus", 0.91, [("wood", .01, 248, .34), ("glass", .20, 523, .25), ("tin", .39, 697, .17)]),
    "surprised": ("Hvordan blev det til to glas?", 0.45, [("drop", .02, 440, .32), ("drop", .10, 721, .37), ("tin", .24, 880, .16)]),
    "unlock": ("Boden åbner", 1.38, [("wood", .01, 170, .26), ("tin", .10, 690, .33), ("glass", .27, 495, .33), ("glass", .42, 620, .37), ("glass", .56, 744, .31)]),
    "walk": ("Bare fod på træ", 0.25, [("step", .012, 108, .68), ("cloth", .025, 550, .12)]),
    "run": ("Raskt trin på træ", 0.20, [("step", .006, 142, .79), ("swish", .012, 430, .16)]),
    "jump": ("Spring fra planken", 0.49, [("step", .00, 144, .49), ("rise", .05, 224, .46), ("cloth", .11, 605, .16)]),
    "land": ("Fødderne tilbage på gulvet", 0.31, [("step", .00, 74, .86), ("wood", .04, 164, .23), ("cloth", .05, 535, .15)]),
    "sit": ("Sæt dig på stolen", 0.51, [("cloth", .01, 450, .39), ("creak", .15, 91, .49), ("wood", .26, 130, .16)]),
    "sleep": ("Et stille pust", 1.27, [("swish", .04, 215, .19), ("swish", .56, 147, .17), ("cloth", .68, 285, .08)]),
    "pickup": ("Fingre om glasset", 0.47, [("cloth", .02, 590, .13), ("glass", .12, 422, .43), ("tin", .27, 619, .11)]),
    "interact": ("En ordre lægges på disken", 0.57, [("paper", .01, 1310, .31), ("wood", .14, 255, .42), ("paper", .25, 1820, .18)]),
    "hit": ("Træf mod forklæde og vest", 0.31, [("step", .00, 87, .76), ("wood", .013, 238, .46), ("cloth", .020, 800, .18)]),
    "knockout": ("Boden rasler", 0.95, [("wood", .01, 105, .63), ("glass", .05, 324, .35), ("tin", .18, 457, .26), ("wood", .28, 137, .32), ("drop", .46, 288, .18)]),
    "respawn": ("Tilbage bag disken", 0.94, [("rise", .01, 242, .24), ("wood", .17, 176, .34), ("glass", .35, 588, .31), ("glass", .49, 724, .26)]),
    "ability_1_pulse": ("Syltetøjskast", 0.74, [("swish", .02, 920, .41), ("tin", .23, 710, .24), ("glass", .30, 513, .40), ("drop", .47, 272, .21)]),
    "ability_2_magnet": ("Køgreb", 0.86, [("paper", .04, 1470, .29), ("swish", .18, 785, .38), ("creak", .34, 128, .36), ("wood", .52, 190, .42)]),
    "ability_3_shield": ("Glasværn", 0.82, [("wood", .00, 166, .54), ("tin", .04, 688, .41), ("glass", .14, 828, .34), ("glass", .36, 555, .13)]),
    "ability_4_dash": ("Bodryk", 0.61, [("step", .01, 112, .58), ("swish", .07, 1390, .55), ("paper", .17, 1670, .16), ("wood", .37, 166, .24)]),
    "ability_5_repair": ("Smagepause", 1.18, [("tin", .01, 428, .22), ("glass", .08, 351, .24), ("drop", .34, 376, .47), ("drop", .58, 522, .28), ("glass", .73, 676, .26)]),
    "ultimate_charge": ("Ordrekøen · opladning", 1.59, [("paper", .01, 1330, .26), ("wood", .11, 168, .41), ("wood", .39, 197, .41), ("wood", .66, 228, .37), ("rise", .86, 276, .44), ("tin", 1.22, 741, .31)]),
    "ultimate_release": ("Ordrekøen · udlevering", 1.37, [("wood", .01, 155, .62), ("glass", .12, 415, .39), ("swish", .20, 870, .48), ("glass", .35, 528, .47), ("glass", .58, 647, .43), ("drop", .77, 276, .31)]),
}


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def band_noise(length, rate, center, width, rng):
    import numpy as np

    freqs = np.fft.rfftfreq(length, 1 / rate)
    spectrum = np.fft.rfft(rng.normal(0, 1, length))
    spectrum *= np.exp(-0.5 * ((freqs - center) / width) ** 2)
    noise = np.fft.irfft(spectrum, n=length)
    return noise / max(1e-6, np.std(noise))


def instrument(name, pitch, length, rng):
    import numpy as np

    t = np.arange(length) / RATE
    sinusoid = lambda f: np.sin(2 * np.pi * f * t)
    attack = np.minimum(1, t / .004)
    if name in {"wood", "step", "glass", "tin"}:
        modes = {
            "wood": [(1, .72, 23), (1.62, .36, 38), (3.14, .23, 56)],
            "step": [(1, .80, 32), (.73, .48, 44), (2.17, .19, 59)],
            "glass": [(1, .67, 9), (2.56, .31, 18), (4.08, .19, 25), (6.72, .08, 34)],
            "tin": [(1, .51, 17), (2.36, .29, 28), (4.78, .19, 48), (6.91, .11, 63)],
        }[name]
        tone = sum(gain * sinusoid(pitch * factor) * np.exp(-t * decay)
                   for factor, gain, decay in modes)
        noise = band_noise(length, RATE, 4.7 * pitch, 650, rng) * np.exp(-t * 83)
        return attack * (tone + (.11 if name == "step" else .06) * noise)
    if name == "drop":
        phase = 2 * np.pi * (pitch * t + pitch * 3.4 * t ** 2)
        return attack * np.exp(-t * 17) * (np.sin(phase) + .23 * np.sin(2.02 * phase))
    if name == "rise":
        phase = 2 * np.pi * (pitch * t + 265 * t ** 2)
        return np.minimum(1, t / .07) * np.exp(-t * 4.5) * (np.sin(phase) + .23 * np.sin(2 * phase))
    if name == "creak":
        tone = sinusoid(pitch) + .25 * sinusoid(pitch * 2.08)
        envelope = np.minimum(1, t / .048) * np.exp(-t * 9)
        return envelope * (tone * (.60 + .4 * np.sin(2 * np.pi * 18 * t) ** 2)
                           + .16 * band_noise(length, RATE, 950, 500, rng))
    noise = band_noise(length, RATE, pitch * (1.35 if name == "paper" else 1), pitch * .62, rng)
    if name == "paper":
        envelope = np.minimum(1, t / .008) * np.exp(-t * 22) * (.55 + .45 * np.sin(2 * np.pi * 48 * t) ** 2)
    elif name == "cloth":
        envelope = np.minimum(1, t / .055) * np.exp(-t * 14)
    else:
        envelope = np.minimum(1, t / .092) * np.exp(-t * 9)
    return .37 * noise * envelope


def generate_effects(output):
    import numpy as np
    from scipy.signal import butter, sosfilt
    import soundfile as sf

    highpass = butter(2, 41, "highpass", fs=RATE, output="sos")
    effects = []
    for action_id, (label, duration, events) in RECIPES.items():
        seed = int.from_bytes(hashlib.sha256(("bilbo/" + action_id).encode()).digest()[:4], "big")
        rng = np.random.default_rng(seed)
        samples = np.zeros(round(duration * RATE), dtype=np.float64)
        for name, onset, pitch, gain in events:
            start = round(onset * RATE)
            samples[start:] += gain * instrument(name, pitch, len(samples) - start, rng)
        samples = sosfilt(highpass, samples)
        samples -= samples.mean()
        count = round(RATE * .009)
        samples[:count] *= np.linspace(0, 1, count)
        samples[-count:] *= np.linspace(1, 0, count)
        samples = samples / max(1e-6, np.max(np.abs(samples))) * {
            "ability_1_pulse": .68, "ultimate_release": .73, "knockout": .71, "hit": .60,
            "idle": .31, "blink": .29, "sleep": .29, "walk": .49, "run": .54,
        }.get(action_id, .56)
        filename = action_id + ".wav"
        sf.write(output / filename, samples, RATE, subtype="PCM_16")
        effects.append({
            "id": action_id, "label": label, "file": filename, "path": "audio/" + filename,
            "duration": duration, "durationSeconds": duration, "loop": False,
            "kind": "sfx", "source": SFX_SOURCE, "gain": .65, "description": label + ".",
        })
    return effects


def apply_filter(source, destination, filters):
    subprocess.run([
        "ffmpeg", "-nostdin", "-hide_banner", "-loglevel", "error", "-n",
        "-i", str(source), "-af", filters, "-ac", "1", "-ar", str(RATE), "-c:a", "pcm_s16le",
        str(destination),
    ], check=True, timeout=25)


def signal(path):
    with wave.open(str(path), "rb") as stream:
        if (stream.getnchannels(), stream.getsampwidth(), stream.getframerate(), stream.getcomptype()) != (1, 2, RATE, "NONE"):
            raise ValueError(f"Incorrect WAV format: {path.name}")
        count = stream.getnframes()
        frames = stream.readframes(count)
    if len(frames) != 2 * count or not count:
        raise ValueError(f"Incomplete WAV: {path.name}")
    samples = [value / 32768 for (value,) in struct.iter_unpack("<h", frames)]
    peak = max(map(abs, samples))
    rms = math.sqrt(sum(sample * sample for sample in samples) / count)
    dc = sum(samples) / count
    if not .003 < rms or not .025 <= peak <= .77 or abs(dc) > .003:
        raise ValueError(f"Invalid PCM signal in {path.name}: {peak=}, {rms=}, {dc=}")
    return {
        "durationSeconds": round(count / RATE, 4), "sha256": sha256(path),
        "peak": round(peak, 6), "rms": round(rms, 6), "dcOffset": round(dc, 7),
        "channels": 1, "sampleRate": RATE, "sampleWidthBytes": 2,
    }


def generate_speech(model_path, output, source_output, scratch):
    os.environ["HF_HUB_OFFLINE"] = "1"
    os.environ["TRANSFORMERS_OFFLINE"] = "1"
    import numpy as np
    import soundfile as sf
    import torch
    from chatterbox.mtl_tts import ChatterboxMultilingualTTS

    source = json.loads((model_path / "source.json").read_text(encoding="utf-8"))
    revision = source["revision"]
    if source["model"] != MODEL or len(revision) != 40:
        raise ValueError("The local model source metadata does not identify Danish Røst")
    device = "cuda" if torch.cuda.is_available() else "cpu"
    torch.set_num_threads(4)
    print(f"Loading local {MODEL} @ {revision} on {device}", flush=True)
    model = ChatterboxMultilingualTTS.from_local(model_path, device=device)

    def synthesize(text, seed, reference=None):
        random.seed(seed)
        np.random.seed(seed)
        torch.manual_seed(seed)
        options = dict(SETTINGS)
        if reference:
            options["audio_prompt_path"] = str(reference)
        with torch.inference_mode():
            samples = model.generate(text, **options).detach().cpu().squeeze().numpy()
        if samples.ndim != 1 or not samples.size or not np.isfinite(samples).all():
            raise ValueError("Røst returned invalid speech")
        peak = float(np.abs(samples).max())
        if peak < .003:
            raise ValueError("Røst returned silent speech")
        return samples * min(1, .9 / peak)

    reference_seeds = [51628, 51629]
    print("Preparing Bilbo's synthetic shopkeeper voice", flush=True)
    reference_chunks = [synthesize(line, seed) for line, seed in zip(REFERENCE_LINES, reference_seeds)]
    reference_raw = scratch / "neutral-reference.wav"
    sf.write(reference_raw, np.concatenate([
        reference_chunks[0], np.zeros(round(model.sr * .24)), reference_chunks[1],
    ]), model.sr, subtype="PCM_16")
    reference = source_output / "bilbo-reference.wav"
    ref_filter = REFERENCE_FILTER + ",alimiter=limit=0.75:level=false:latency=1"
    apply_filter(reference_raw, reference, ref_filter)
    ref_signal = signal(reference)
    if not 5 <= ref_signal["durationSeconds"] <= 22:
        raise ValueError("Expected at least five seconds of usable voice conditioning")
    voice_id = hashlib.sha256(MODEL.encode() + revision.encode() + reference.read_bytes()).hexdigest()
    clips = []
    for index, (phrase_id, event, line) in enumerate(PHRASES):
        seed = 42107 + index
        filename = phrase_id + ".wav"
        print(f"Rendering {phrase_id}", flush=True)
        raw = scratch / filename
        sf.write(raw, synthesize(line, seed, reference), model.sr, subtype="PCM_16")
        destination = output / filename
        apply_filter(raw, destination, FINISH_FILTER)
        measurement = signal(destination)
        duration = measurement["durationSeconds"]
        if not max(1, len(line.split()) * .12) <= duration <= 14:
            raise ValueError(f"Unexpected speech duration for {phrase_id}: {duration} s")
        clips.append({
            "id": phrase_id, "event": event, "text": line, "language": "da-DK",
            "file": filename, "path": "audio/" + filename, "available": True,
            "duration": duration, "durationSeconds": duration, "source": SPEECH_SOURCE,
            "sha256": measurement["sha256"], "seed": seed,
        })
    voice = {
        "provider": "Røst", "model": MODEL, "modelRevision": revision, "voice": VOICE,
        "voiceId": voice_id, "style": "synthetic-character", "generation": SETTINGS,
        "chatterboxVersion": importlib.metadata.version("chatterbox-tts"),
        "reference": {
            "file": reference.name, "source": "Original Danish speech synthesized by Røst",
            "texts": REFERENCE_LINES, "seeds": reference_seeds,
            "sha256": ref_signal["sha256"], "durationSeconds": ref_signal["durationSeconds"],
            "filters": ref_filter,
        },
        "finishFilters": FINISH_FILTER,
    }
    return clips, voice


def build(model, output, source_output):
    if output.exists() or source_output.exists():
        raise ValueError("Use new output and source directories")
    output.parent.mkdir(parents=True, exist_ok=True)
    source_output.parent.mkdir(parents=True, exist_ok=True)
    with TemporaryDirectory(prefix=".bilbo-audio-", dir=output.parent) as folder:
        scratch = Path(folder)
        stage, sources = scratch / "audio", scratch / "sources"
        stage.mkdir()
        sources.mkdir()
        effects = generate_effects(stage)
        phrases, voice = generate_speech(model, stage, sources, scratch)
        manifest = {
            "schemaVersion": 1, "avatarId": "bilbo", "name": "Bilbo Backend",
            "sampleRate": RATE, "channels": 1, "format": "PCM16 WAV", "sfx": effects,
            "phrases": phrases, "speech": dict(voice, available=True, language="da-DK", status="ready"),
            "playback": {
                "defaultMuted": True, "maxConcurrentSfx": 3, "phraseCooldownSeconds": 18,
                "stopPreviousPhrase": True, "idleSoundsAutomatic": False,
                "footsteps": "one clip per foot-contact event",
            },
            "license": {
                "proceduralSfx": "Original procedural synthesis made for Bilbo Backend.",
                "speech": "Synthesized locally with Danish Røst and an original synthetic character reference.",
            },
        }
        provenance = dict(
            voice, schemaVersion=1, avatarId="bilbo", language="da",
            phrases=[{key: clip[key] for key in ("id", "file", "text", "sha256", "seed")} for clip in phrases],
        )
        measurements = [dict(id=entry["id"], file=entry["file"], **signal(stage / entry["file"]))
                        for entry in effects + phrases]
        if len(measurements) != 33 or len({entry["sha256"] for entry in measurements}) != 33:
            raise ValueError("Expected 33 unique WAVs")
        for entry, measured in zip(effects + phrases, measurements):
            if abs(entry["durationSeconds"] - measured["durationSeconds"]) > .001:
                raise ValueError(f"Manifest duration mismatch: {entry['id']}")
        report = {
            "passed": True, "scope": "File bytes, WAV format, signal and metadata alignment",
            "files": len(measurements), "sfxCount": len(effects), "spokenCount": len(phrases),
            "sampleRate": RATE, "channels": 1, "format": "PCM16 WAV",
            "maxPeak": max(entry["peak"] for entry in measurements),
            "maxAbsDc": max(abs(entry["dcOffset"]) for entry in measurements),
            "allWaveformsDistinct": True, "errors": [], "items": measurements,
        }
        write_json(stage / "audio-manifest.json", manifest)
        write_json(stage / "phrases.json", phrases)
        write_json(stage / "speech-provenance.json", provenance)
        write_json(stage / "audio-validation.json", report)
        sources.rename(source_output)
        stage.rename(output)
    print(f"Validated and exported 28 effects and 5 Røst phrases: {output}", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--source-output", type=Path, required=True)
    args = parser.parse_args()
    try:
        build(args.model.resolve(strict=True), args.output.absolute(), args.source_output.absolute())
    except (OSError, ValueError, KeyError, RuntimeError, subprocess.SubprocessError) as error:
        print(f"Bilbo audio build failed: {error}", file=sys.stderr, flush=True)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
