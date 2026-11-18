"""Run the six pilot tasks with real SDKs and the authoritative shared simulator.

Only the hardcoded programs below are compiled and launched by this local test.
"""

from contextlib import contextmanager
import json
from pathlib import Path
from queue import Empty, Queue
import subprocess
import sys
import tempfile
from threading import Thread

from verify import (ROOT, TEMPORARY_ROOT, build_arduino_fixture, build_csharp_fixture,
                    build_csharp_sdk, csharp_build)
from worker import encode

TASK_PROGRAM = {"P01": 0, "P02": 0, "P03": 1, "P04": 2, "P05": 3, "P06": 3}
PYTHON = [
    'import home\nhome.set("desk_lamp", True)\n',
    *[
        f'import home\nwhile True:\n    home.set("desk_lamp", {" and ".join(rules)})\n    home.wait(0.2)\n'
        for rules in (
            ['home.read("daylight") < 300'],
            ['home.read("daylight") < 300', 'home.read("presence")'],
            ['home.read("daylight") < 300', 'home.read("presence")', 'home.read("auto_enabled")'],
        )
    ],
]
CSHARP = [
    'using HomeSdk;\nHome.Set("desk_lamp", true);\n',
    *[
        f'using HomeSdk;\nwhile (true) {{ Home.Set("desk_lamp", {" && ".join(rules)}); Home.Wait(0.2); }}\n'
        for rules in (
            ['Home.Read("daylight") < 300'],
            ['Home.Read("daylight") < 300', 'Home.ReadBool("presence")'],
            ['Home.Read("daylight") < 300', 'Home.ReadBool("presence")', 'Home.ReadBool("auto_enabled")'],
        )
    ],
]
ARDUINO = [
    '#include <VirtualHome.h>\nvoid setup() { home.set("desk_lamp", true); }\nvoid loop() { delay(1000); }\n',
    *[
        f'#include <VirtualHome.h>\nvoid setup() {{ home.begin(); }}\n'
        f'void loop() {{ home.set("desk_lamp", {" && ".join(rules)}); delay(200); }}\n'
        for rules in (
            ['home.read("daylight") < 300'],
            ['home.read("daylight") < 300', 'home.readBool("presence")'],
            ['home.read("daylight") < 300', 'home.readBool("presence")', 'home.readBool("auto_enabled")'],
        )
    ],
]


@contextmanager
def peer(command):
    with subprocess.Popen([str(part) for part in command],
                          stdin=subprocess.PIPE, stdout=subprocess.PIPE) as process:
        messages = Queue()

        def collect():
            for line in process.stdout:
                messages.put(line)
            messages.put(None)

        Thread(target=collect, daemon=True).start()

        def send(value):
            process.stdin.write(encode(value))
            process.stdin.flush()

        def receive():
            try:
                line = messages.get(timeout=12)
            except Empty as error:
                raise RuntimeError(f"Timed out waiting for {command[0]}.") from error
            return json.loads(line) if line else None

        try:
            yield send, receive
        finally:
            process.stdin.close()
            try:
                process.wait(timeout=2)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()


def play_scenario(start, simulation, language, commands):
    task_index = TASK_PROGRAM[start["task"]]
    send_simulator, receive_simulator = simulation
    with peer(commands[task_index]) as (send_runtime, receive_runtime):
        if language == "python":
            send_runtime({"type": "start", "source": PYTHON[task_index]})
            if receive_runtime() != {"type": "ready"}:
                raise RuntimeError("The Python fixture could not start.")
        while (frame := receive_simulator()) is not None:
            if frame["type"] == "outcome":
                if not frame["outcome"]["passed"]:
                    raise RuntimeError(f"{language}/{frame['task']}: {frame['outcome']}")
                return
            if frame["type"] != "snapshot":
                raise RuntimeError(f"Unexpected simulator frame: {frame}")
            send_runtime({**frame, "aliases": start["aliases"]})
            batch = receive_runtime()
            if batch is None or batch.get("type") != "batch":
                raise RuntimeError(f"{language}/{start['task']}/{start['scenario']}: {batch}")
            send_simulator(batch)
    raise RuntimeError(f"The simulator closed during {start['task']}/{start['scenario']}.")


def verify_language(simulator, language, commands):
    passed = {}
    with peer(simulator) as simulation:
        _, receive = simulation
        while (start := receive()) is not None:
            if start["type"] != "scenario":
                raise RuntimeError(f"Unexpected scenario start: {start}")
            play_scenario(start, simulation, language, commands)
            passed[start["task"]] = passed.get(start["task"], 0) + 1
    if passed != {"P01": 1, "P02": 1, "P03": 2, "P04": 4, "P05": 5, "P06": 5}:
        raise RuntimeError(f"Missing pilot scenarios for {language}: {passed}")
    for task, count in passed.items():
        print(f"{language:>7} {task} passed ({count} scenario{'s' if count != 1 else ''})", flush=True)


def main():
    with tempfile.TemporaryDirectory(dir=TEMPORARY_ROOT) as directory:
        work = Path(directory)
        csharp_build(ROOT / "verification" / "HomeVerification.csproj", work / "simulator")
        simulator = ["dotnet", work / "simulator" / "HomeVerification.dll"]
        verify_language(simulator, "python", [[sys.executable, ROOT / "sandbox.py", "python"]] * len(PYTHON))
        sdk = build_csharp_sdk(work)
        csharp = [build_csharp_fixture(work / f"csharp{index}", source, sdk) for index, source in enumerate(CSHARP)]
        verify_language(simulator, "csharp", csharp)
        arduino = [build_arduino_fixture(work / f"arduino{index}", source) for index, source in enumerate(ARDUINO)]
        verify_language(simulator, "arduino", arduino)


if __name__ == "__main__":
    main()
