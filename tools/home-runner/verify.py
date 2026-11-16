"""Smoke checks with repository-owned programs. No arbitrary local code input."""

import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import urllib.error
import urllib.request
import uuid

import worker

ROOT = Path(__file__).resolve().parent
TEMPORARY_ROOT = os.environ.get("LUP_RUNNER_TEST_TMP")
CPP_HEADER_DIR = os.environ.get("LUP_RUNNER_CPP_INCLUDE")
ALIASES = {"daylight": "daylight", "presence": "presence", "desk_lamp": "lamp",
           "reading_lux": "light", "spare": "uninstalled"}
READINGS = [
    {"daylight": 40, "presence": True, "reading_lux": 40},
    {"daylight": 450, "presence": True, "reading_lux": 378},
    {"daylight": 40, "presence": False, "reading_lux": 40},
]
PYTHON_SOURCE = """import home
for _ in range(2):
    home.set("desk_lamp", home.read("daylight") < 300 and home.read("presence"))
    home.log(home.read("reading_lux"))
    home.wait(0.2)
home.set("desk_lamp", home.read("daylight") < 300 and home.read("presence"))
home.log(home.read("reading_lux"))
"""
CSHARP_SOURCE = """using HomeSdk;
for (var i = 0; i < 3; i++)
{
    Home.Set("desk_lamp", Home.Read("daylight") < 300 && Home.ReadBool("presence"));
    Home.Log(Home.Read("reading_lux"));
    if (i < 2) Home.Wait(0.2);
}
"""
ARDUINO_SOURCE = """#include <VirtualHome.h>
void setup() { home.begin(); }
void loop() {
    home.set("desk_lamp", home.read("daylight") < 300 && home.readBool("presence"));
    Serial.println(home.read("reading_lux"));
    delay(200);
}
"""


def process(command, payload=b"", timeout=45, cwd=None):
    result = subprocess.run([str(part) for part in command], input=payload, capture_output=True,
                            timeout=timeout, cwd=cwd)
    if result.returncode:
        raise RuntimeError((result.stdout + result.stderr).decode(errors="replace"))
    return result.stdout


def build_csharp_sdk(work):
    shutil.copytree(ROOT / "csharp" / "sdk", work / "sdk",
                    ignore=shutil.ignore_patterns("obj", "bin"))
    csharp_build(work / "sdk" / "HomeRunner.csproj", work / "bootstrap")
    return work / "bootstrap" / "HomeRunner.dll"


def csharp_build(project, output, *options):
    return process(["dotnet", "build", project, "-o", output, "--configfile",
                    ROOT / "csharp" / "NuGet.Config", "--disable-build-servers",
                    "-p:UseSharedCompilation=false", *options], timeout=90)


def build_csharp_fixture(work, source, sdk):
    work.mkdir()
    project = work / "Student.csproj"
    shutil.copyfile(ROOT / "csharp" / "Student.csproj", project)
    (work / "Program.cs").write_text(source, encoding="utf-8")
    csharp_build(project, work / "compiled", f"-p:LupSdkAssembly={sdk}")
    return ["dotnet", sdk, work / "compiled" / "Student.dll"]


def build_arduino_fixture(work, source):
    work.mkdir()
    sketch = work / "sketch.cpp"
    sketch.write_text(source, encoding="utf-8")
    executable = work / ("sketch.exe" if os.name == "nt" else "sketch")
    sdk = ROOT / "arduino"
    if shutil.which("g++"):
        includes = ["-I", CPP_HEADER_DIR] if CPP_HEADER_DIR else []
        command = ["g++", "-std=c++17", "-I", sdk, *includes, sketch, sdk / "VirtualHome.cpp", "-o", executable]
    else:
        includes = [f"/I{CPP_HEADER_DIR}"] if CPP_HEADER_DIR else []
        command = ["cl", "/nologo", "/EHsc", "/std:c++17", "/utf-8", f"/I{sdk}", *includes,
                   sketch, sdk / "VirtualHome.cpp", f"/Fe:{executable}"]
    process(command, cwd=work)
    return [executable]


def frames(source=None):
    start = b"" if source is None else worker.encode({"type": "start", "source": source})
    return start + b"".join(worker.encode({
        "type": "snapshot", "timeMs": index * 200, "aliases": ALIASES, "values": values,
        "activation": str(index),
    }) for index, values in enumerate(READINGS))


def decode(output):
    return [json.loads(line) for line in output.splitlines()]


class GatewayTests(unittest.TestCase):
    def setUp(self):
        self.run = str(uuid.uuid4())
        self.session = worker.Session(self.run, str(uuid.uuid4()), {
            "controllerId": "controller", "language": "python", "aliases": ALIASES,
        })

    def test_rejects_stale_or_unopened_activation(self):
        self.session.ready = True
        with self.assertRaisesRegex(worker.RunnerError, "matcher"):
            self.session.activate({"runId": self.run, "controllerId": "controller",
                                   "sequence": 2, "tick": 0, "timeMs": 0})
        self.assertEqual(self.session.sequence, 1)

    def test_discards_entire_batch_for_uninstalled_device(self):
        with self.assertRaises(worker.RunnerError) as error:
            self.session.validate_batch({
                "type": "batch", "nextDelayMs": 0, "logs": [], "done": True,
                "commands": [
                    {"alias": "desk_lamp", "action": "setPower", "value": True},
                    {"alias": "spare", "action": "setPower", "value": False},
                ],
            })
        self.assertEqual(error.exception.code, "DEVICE_NOT_INSTALLED")

    def test_rejects_a_delayed_sandbox_batch(self):
        self.session.ready = True
        request = {"runId": self.run, "controllerId": "controller", "tick": 0, "sequence": 1,
                   "timeMs": 0, "values": {}}
        with patch.object(self.session, "exchange", return_value={"activation": "previous"}):
            with self.assertRaises(worker.RunnerError) as error:
                self.session.activate(request)
        self.assertEqual(error.exception.code, "RUNNER_PROTOCOL")

    def test_plain_text_log_is_bounded(self):
        result = self.session.validate_batch({
            "type": "batch", "nextDelayMs": 0, "logs": ["æ" * 20000], "done": True, "commands": [],
        })
        self.assertLessEqual(len(result["logs"][0].encode()), worker.ACTIVATION_LOG_BYTES)

    def test_stop_before_start_does_not_cancel_a_new_scenario(self):
        with patch.object(worker.ThreadingHTTPServer, "__init__", return_value=None):
            gateway = worker.Worker(("127.0.0.1", 0), "a" * 32, capacity=1)
        self.assertTrue(gateway.stop(self.session.id))
        with self.assertRaises(worker.RunnerError) as error:
            gateway.add(self.session)
        self.assertEqual(error.exception.code, "RUN_CANCELLED")
        next_scenario = worker.Session(self.run, str(uuid.uuid4()), {
            "controllerId": "controller", "language": "python", "aliases": ALIASES,
        })
        gateway.add(next_scenario)
        self.assertEqual(gateway.sessions, {next_scenario.id: next_scenario})


class SDKAssertions:
    def verify_batches(self, batches, done):
        self.assertEqual(len(batches), 3)
        self.assertEqual([value["commands"] for value in batches], [
            [{"alias": "desk_lamp", "action": "setPower", "value": expected}]
            for expected in (True, False, False)
        ])
        self.assertEqual([value["logs"] for value in batches], [["40"], ["378"], ["40"]])
        self.assertEqual([value["nextDelayMs"] for value in batches], [200, 200, 0 if done else 200])
        self.assertEqual([value["done"] for value in batches], [False, False, done])


class LocalTrustedSDKTests(SDKAssertions, unittest.TestCase):
    def test_python_reference(self):
        output = process([sys.executable, ROOT / "sandbox.py", "python"], frames(PYTHON_SOURCE))
        ready, *batches = decode(output)
        self.assertEqual(ready, {"type": "ready"})
        self.verify_batches(batches, True)

    def test_caught_invalid_command_invalidates_python_batch(self):
        source = """import home
home.set("desk_lamp", True)
try:
    home.set("spare", True)
except home.HomeError:
    pass
"""
        output = process([sys.executable, ROOT / "sandbox.py", "python"], frames(source))
        ready, error = decode(output)
        self.assertEqual(ready, {"type": "ready"})
        self.assertEqual(error["type"], "error")
        self.assertEqual(error["code"], "DEVICE_NOT_INSTALLED")

    @unittest.skipUnless(shutil.which("dotnet"), "dotnet 10 is required")
    def test_csharp_reference(self):
        with tempfile.TemporaryDirectory(dir=TEMPORARY_ROOT) as directory:
            work = Path(directory)
            sdk = build_csharp_sdk(work)
            command = build_csharp_fixture(work / "student", CSHARP_SOURCE, sdk)
            batches = decode(process(command, frames()))
            self.verify_batches(batches, True)

    @unittest.skipUnless(shutil.which("g++") or shutil.which("cl"), "a C++17 compiler is required")
    def test_arduino_reference(self):
        batches = self.run_arduino(ARDUINO_SOURCE, frames())
        self.verify_batches(batches[:3], False)

    @unittest.skipUnless(shutil.which("g++") or shutil.which("cl"), "a C++17 compiler is required")
    def test_arduino_millis_and_wrap_without_delay(self):
        source = """#include <VirtualHome.h>
std::uint32_t previous;
void setup() { previous = millis(); }
void loop() {
    const auto now = millis();
    Serial.println(now);
    Serial.println(std::uint32_t(now - previous));
    previous = now;
}
"""
        times = [2**32 - 96, 2**32 + 4, 2**32 + 104]
        payload = b"".join(worker.encode({
            "type": "snapshot", "timeMs": time, "aliases": {}, "values": {},
            "activation": str(time),
        }) for time in times)
        batches = self.run_arduino(source, payload)
        self.assertEqual([batch["logs"] for batch in batches[:3]],
                         [[str(times[0]), "0"], ["4", "100"], ["104", "100"]])
        self.assertEqual([batch["nextDelayMs"] for batch in batches[:3]], [0, 0, 0])

    def run_arduino(self, source, payload):
        with tempfile.TemporaryDirectory(dir=TEMPORARY_ROOT) as directory:
            command = build_arduino_fixture(Path(directory) / "student", source)
            return decode(process(command, payload))


@unittest.skipUnless(os.environ.get("HOME_RUNNER_URL") and os.environ.get("HOME_RUNNER_TOKEN"),
                     "a deployed gVisor worker is required")
class DeployedRunnerTests(SDKAssertions, unittest.TestCase):
    def request(self, method, route, body=None, authenticate=True):
        data = None if body is None else worker.encode(body)
        request = urllib.request.Request(os.environ["HOME_RUNNER_URL"].rstrip("/") + route, method=method,
                                         data=data, headers={"Content-Type": "application/json"})
        if authenticate:
            request.add_header("Authorization", "Bearer " + os.environ["HOME_RUNNER_TOKEN"])
        with urllib.request.urlopen(request, timeout=37) as response:
            return json.load(response)

    def start(self, language, source):
        run = str(uuid.uuid4())
        session_id = str(uuid.uuid4())
        self.addCleanup(self.request, "DELETE", "/sessions/" + session_id)
        reply = self.request("POST", "/sessions", {
            "runId": run, "sessionId": session_id, "controllerId": "controller", "language": language,
            "source": source, "aliases": ALIASES,
        })
        return run, session_id, reply

    def activate(self, run, session_id, tick, sequence, values):
        return self.request("POST", f"/sessions/{session_id}/activate", {
            "runId": run, "controllerId": "controller", "sequence": sequence,
            "tick": tick, "timeMs": tick * 100, "values": values,
        })

    def test_refs_across_three_runtimes(self):
        self.assertEqual(self.request("GET", "/health")["status"], "ready")
        for language, source in (("python", PYTHON_SOURCE), ("csharp", CSHARP_SOURCE), ("arduino", ARDUINO_SOURCE)):
            with self.subTest(language=language):
                run, session_id, reply = self.start(language, source)
                try:
                    self.assertEqual(reply.get("sessionId"), session_id, reply)
                    batches = [self.activate(run, session_id, index * 2, index + 1, values)
                               for index, values in enumerate(READINGS)]
                    self.verify_batches(batches, language != "arduino")
                finally:
                    self.request("DELETE", "/sessions/" + session_id)

    def test_missing_bearer(self):
        with self.assertRaises(urllib.error.HTTPError) as error:
            self.request("GET", "/health", authenticate=False)
        self.assertEqual(error.exception.code, 401)

    def test_syntax_error_and_infinite_loop_timeout(self):
        _, _, broken = self.start("python", "if :\n")
        self.assertEqual(broken["error"]["code"], "COMPILE_ERROR")
        run, session_id, ready = self.start("python", "while True:\n    pass\n")
        self.assertEqual(ready.get("sessionId"), session_id, ready)
        batch = self.activate(run, session_id, 0, 1, READINGS[0])
        self.assertEqual(batch["error"]["code"], "RUN_TIMEOUT")
        self.assertFalse(batch["commands"])
        after = self.activate(run, session_id, 0, 1, READINGS[0])
        self.assertEqual(after["error"]["code"], "RUN_EXPIRED")

    def test_arduino_loop_millis_advances_without_delay(self):
        run, session_id, reply = self.start("arduino", """#include <VirtualHome.h>
void setup() {}
void loop() { Serial.println(millis()); }
""")
        self.assertEqual(reply.get("sessionId"), session_id, reply)
        self.assertEqual([self.activate(run, session_id, tick, tick + 1, {})["logs"] for tick in range(3)],
                         [["0"], ["100"], ["200"]])

    def test_cancel_before_start_rejects_delayed_request(self):
        session_id = str(uuid.uuid4())
        self.request("DELETE", "/sessions/" + session_id)
        reply = self.request("POST", "/sessions", {
            "runId": str(uuid.uuid4()), "sessionId": session_id, "controllerId": "controller",
            "language": "python", "source": "print(42)", "aliases": {},
        })
        self.assertEqual(reply["error"]["code"], "RUN_CANCELLED")

    def test_student_process_has_isolated_user_and_no_host_interfaces(self):
        run, session_id, reply = self.start("python", """import home
import os
import socket
home.log(os.getuid())
home.log(",".join(name for _, name in socket.if_nameindex()))
home.log(os.path.exists("/var/run/docker.sock"))
home.log("HOME_RUNNER_TOKEN" in os.environ)
""")
        self.assertEqual(reply.get("sessionId"), session_id, reply)
        batch = self.activate(run, session_id, 0, 1, {})
        self.assertEqual(batch["logs"], ["65532", "lo", "False", "False"])
        self.assertTrue(batch["done"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
