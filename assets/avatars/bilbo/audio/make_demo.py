"""Assemble a short MP3 featuring Bilbo's speech and signature actions."""
import argparse
import json
from pathlib import Path
import subprocess
from tempfile import TemporaryDirectory
import wave


RATE = 44100
CLIPS = [
    "phrase_1_welcome", "walk", "pickup", "ability_1_pulse", "ability_2_magnet",
    "ability_3_shield", "ultimate_charge", "ultimate_release", "phrase_5_milestone",
]


def make_demo(audio):
    with TemporaryDirectory(prefix=".bilbo-demo-", dir=audio) as temp:
        master = Path(temp) / "demo.wav"
        cursor = 0
        cues = []
        with wave.open(str(master), "wb") as stream:
            stream.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
            for clip in CLIPS:
                cues.append({"id": clip, "startSeconds": round(cursor / RATE, 3)})
                with wave.open(str(audio / f"{clip}.wav"), "rb") as wav:
                    count = wav.getnframes()
                    stream.writeframes(wav.readframes(count))
                gap = round(RATE * (.32 if clip.startswith("phrase_") else .16))
                stream.writeframes(bytes(gap * 2))
                cursor += count + gap
        subprocess.run([
            "ffmpeg", "-nostdin", "-hide_banner", "-loglevel", "error", "-n", "-i", str(master),
            "-c:a", "libmp3lame", "-q:a", "3", str(audio / "bilbo-lydproeve.mp3"),
        ], check=True, timeout=30)
    (audio / "bilbo-lydproeve-cues.json").write_text(
        json.dumps({"durationSeconds": round(cursor / RATE, 3), "cues": cues}, indent=2) + "\n",
        encoding="utf-8",
    )


if __name__ == "__main__":
    cli = argparse.ArgumentParser(description=__doc__)
    cli.add_argument("audio", type=Path, help="Directory with Bilbo's rendered WAVs")
    make_demo(cli.parse_args().audio.resolve(strict=True))
