"""Trusted HTTP gateway for the dedicated LUP Hjem.exe runner host."""

import hmac
import json
import math
import os
import re
import secrets
import selectors
import signal
import subprocess
import threading
import time
import uuid
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

SOURCE_BYTES = 128 * 1024
LOG_BYTES = 64 * 1024
ACTIVATION_LOG_BYTES = 8 * 1024
FRAME_BYTES = 160 * 1024
COMPILE_SECONDS = 30
ACTIVE_SECONDS = 3
IDLE_SECONDS = 120
LIFETIME_SECONDS = 900
STOP_TOMBSTONE_SECONDS = 60
SDK_VERSION = 1
CONTAINER_LABEL = "dk.lup.home-runner=1"
ALIAS = re.compile(r"[a-z][a-z0-9_]{0,47}\Z", re.ASCII)
# Use the local worker daemon and an empty CLI configuration. The Docker CLI
# otherwise imports per-user settings, including automatic proxy environment.
DOCKER_ENV = {
    "PATH": os.environ.get("PATH", os.defpath),
    "LANG": "C.UTF-8",
    "DOCKER_HOST": "unix:///var/run/docker.sock",
    "DOCKER_CONFIG": "/etc/lup-home-runner/docker-cli",
}

# Cores, RAM, processes, workspace. Core quota times wall clock caps CPU per
# activation; the independent wall-clock deadline also covers sleeping code.
PROFILES = {
    "python": ("0.5", "128m", "32", "16m"),
    "csharp": ("1", "768m", "96", "96m"),
    "arduino": ("1", "512m", "64", "48m"),
}


class RunnerError(Exception):
    def __init__(self, code, message):
        super().__init__(message)
        self.code = code

    def json(self):
        return {"code": self.code, "message": str(self)}


def encode(frame):
    return (json.dumps(frame, ensure_ascii=False, allow_nan=False, separators=(",", ":")) + "\n").encode()


def identity(value):
    try:
        return str(uuid.UUID(value))
    except (AttributeError, ValueError, TypeError) as error:
        raise RunnerError("INVALID_RUN", "Run- eller sessions-ID er ugyldigt.") from error


def validate_start(body):
    if not isinstance(body.get("language"), str) or body["language"] not in PROFILES:
        raise RunnerError("INVALID_LANGUAGE", "Sproget findes ikke i denne SDK-version.")
    if not isinstance(body.get("source"), str):
        raise RunnerError("SOURCE_LIMIT", "Programmet må højst fylde 128 KiB.")
    try:
        source_bytes = len(body["source"].encode())
    except UnicodeError as error:
        raise RunnerError("INVALID_SOURCE", "Koden skal være gyldig UTF-8-tekst.") from error
    if source_bytes > SOURCE_BYTES:
        raise RunnerError("SOURCE_LIMIT", "Programmet må højst fylde 128 KiB.")
    controller = body.get("controllerId")
    if not isinstance(controller, str) or not 0 < len(controller) <= 128:
        raise RunnerError("INVALID_RUN", "Controller-ID er ugyldigt.")
    aliases = body.get("aliases", {})
    if not isinstance(aliases, dict) or len(aliases) > 512 or any(
        not ALIAS.fullmatch(alias) or not isinstance(kind, str) or len(kind) > 60
        for alias, kind in aliases.items()
    ):
        raise RunnerError("INVALID_RUN", "Aliasoversigten er ugyldig.")
    return identity(body.get("runId")), identity(body.get("sessionId"))


class Session:
    def __init__(self, run, session_id, request):
        self.run = run
        self.id = session_id
        self.controller = request["controllerId"]
        self.language = request["language"]
        self.aliases = request.get("aliases", {})
        self.container = "lup-home-" + session_id
        self.created = time.monotonic()
        self.touched = self.created
        self.log_bytes = 0
        self.sequence = 1
        self.next_tick = 0
        self.done = False
        self.ready = False
        self.process = None
        self.cleaned = False
        self.closed = threading.Event()
        self.exchange_lock = threading.Lock()
        self.process_lock = threading.Lock()

    def start(self, request):
        cpu, memory, pids, work = PROFILES[self.language]
        # The gateway removes the container before releasing its slot.
        command = [
            "docker", "create", "--pull=never", "--runtime=runsc",
            "--name", self.container, "--label", CONTAINER_LABEL,
            "--network=none", "--read-only", "--cap-drop=ALL", "--user=65532:65532",
            "--security-opt=no-new-privileges", "--log-driver=none",
            "--memory", memory, "--memory-swap", memory, "--cpus", cpu,
            "--pids-limit", pids, "--cgroupns=private", "--shm-size=8m",
            "--ulimit=nofile=128:128", "--ulimit=core=0:0",
            "--tmpfs", f"/work:rw,exec,nosuid,nodev,mode=1777,size={work}",
            "--tmpfs", "/tmp:rw,noexec,nosuid,nodev,mode=1777,size=16m",
            "-i", f"lup-home-{self.language}:sdk{SDK_VERSION}",
        ]
        with self.process_lock:
            if self.closed.is_set():
                raise RunnerError("RUN_CANCELLED", "Forsøget blev stoppet.")
            try:
                subprocess.run(command, timeout=8, check=True, capture_output=True, env=DOCKER_ENV)
                if self.closed.is_set():
                    raise RunnerError("RUN_CANCELLED", "Forsøget blev stoppet.")
                self.process = subprocess.Popen(
                    ["docker", "start", "-ai", self.container],
                    stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                    stderr=subprocess.DEVNULL, bufsize=0, env=DOCKER_ENV,
                )
                os.set_blocking(self.process.stdin.fileno(), False)
            except (OSError, subprocess.SubprocessError) as error:
                raise RunnerError("RUNNER_UNAVAILABLE", "Runner-værten kunne ikke starte sandboxen.") from error
        frame = self.exchange({"type": "start", "source": request["source"]},
                              max(1, COMPILE_SECONDS - (time.monotonic() - self.created)))
        if frame.get("type") == "error":
            raise RunnerError("COMPILE_ERROR", str(frame.get("message", "Kompileringen fejlede."))[:12000])
        if frame != {"type": "ready"}:
            raise RunnerError("RUNNER_PROTOCOL", "Sandboxen sendte ikke en gyldig startbesked.")
        self.ready = True
        self.touched = time.monotonic()

    def exchange(self, frame, timeout):
        deadline = time.monotonic() + timeout
        data = memoryview(encode(frame))
        result = bytearray()
        process = self.process
        if process is None or self.closed.is_set():
            raise RunnerError("RUN_CANCELLED", "Forsøget blev stoppet.")
        try:
            with selectors.DefaultSelector() as io:
                io.register(process.stdin, selectors.EVENT_WRITE)
                while data:
                    if not io.select(max(0, deadline - time.monotonic())) or self.closed.is_set():
                        raise TimeoutError
                    data = data[os.write(process.stdin.fileno(), data):]
                io.unregister(process.stdin)
                io.register(process.stdout, selectors.EVENT_READ)
                while len(result) <= FRAME_BYTES:
                    if not io.select(max(0, deadline - time.monotonic())) or self.closed.is_set():
                        raise TimeoutError
                    chunk = os.read(process.stdout.fileno(), 4096)
                    if not chunk:
                        raise OSError
                    result.extend(chunk)
                    if b"\n" in chunk:
                        if len(result) > FRAME_BYTES or result.count(b"\n") != 1 or not result.endswith(b"\n"):
                            break
                        parsed = json.loads(result)
                        if isinstance(parsed, dict):
                            return parsed
                        break
        except TimeoutError as error:
            if self.closed.is_set():
                raise RunnerError("RUN_CANCELLED", "Forsøget blev stoppet.") from error
            code = "COMPILE_TIMEOUT" if not self.ready else "RUN_TIMEOUT"
            raise RunnerError(code, "Programmet overskred runnerens tidsgrænse.") from error
        except (OSError, ValueError, RecursionError) as error:
            if self.closed.is_set():
                raise RunnerError("RUN_CANCELLED", "Forsøget blev stoppet.") from error
            code = "RUNTIME_ERROR" if self.ready else "RUNNER_UNAVAILABLE"
            raise RunnerError(code, "Sandboxen afbrød forbindelsen.") from error
        raise RunnerError("RUNNER_PROTOCOL", "Sandboxens svar havde ugyldigt format eller omfang.")

    def activate(self, request):
        if not self.exchange_lock.acquire(blocking=False):
            raise RunnerError("INVALID_ACTIVATION", "Controlleren afvikler allerede en aktivering.")
        try:
            if self.closed.is_set() or self.done or not self.ready:
                raise RunnerError("INVALID_ACTIVATION", "Kørslen modtager ikke flere aktiveringer.")
            if self.expired(time.monotonic()):
                raise RunnerError("RUN_EXPIRED", "Kørslen er ikke længere aktiv.")
            if (request.get("runId") != self.run or request.get("controllerId") != self.controller
                    or type(request.get("sequence")) is not int or request["sequence"] != self.sequence
                    or type(request.get("tick")) is not int or request["tick"] != self.next_tick
                    or type(request.get("timeMs")) is not int or request["timeMs"] != self.next_tick * 100):
                raise RunnerError("INVALID_ACTIVATION", "Run, controller, sekvens eller tick matcher ikke den åbne aktivering.")
            values = request.get("values")
            if not isinstance(values, dict) or len(values) > 512 or any(
                not ALIAS.fullmatch(alias) or type(value) not in (bool, int)
                for alias, value in values.items()
            ):
                raise RunnerError("INVALID_ACTIVATION", "Sensorøjebliksbilledet er ugyldigt.")
            self.touched = time.monotonic()
            self.sequence += 1
            activation = secrets.token_hex(16)
            frame = self.exchange({
                "type": "snapshot", "timeMs": request["timeMs"], "values": values,
                "aliases": self.aliases, "activation": activation,
            }, ACTIVE_SECONDS)
            if self.closed.is_set():
                raise RunnerError("RUN_CANCELLED", "Forsøget blev stoppet.")
            if frame.get("activation") != activation:
                raise RunnerError("RUNNER_PROTOCOL", "Sandboxens svar tilhørte en anden aktivering.")
            outcome = self.validate_batch(frame)
            self.done = outcome["done"]
            self.next_tick = request["tick"] + max(1, math.ceil(outcome["nextDelayMs"] / 100))
            return outcome
        finally:
            self.exchange_lock.release()

    def expired(self, now):
        return now - self.touched > IDLE_SECONDS or now - self.created > LIFETIME_SECONDS

    def validate_batch(self, frame):
        if frame.get("type") == "error":
            code = frame.get("code")
            allowed = {"UNKNOWN_ALIAS", "DEVICE_NOT_INSTALLED", "CAPABILITY_LOCKED", "INVALID_VALUE", "INVALID_TYPE"}
            code = code if isinstance(code, str) and code in allowed else "RUNTIME_ERROR"
            raise RunnerError(code, str(frame.get("message", "Programmet stoppede med en fejl."))[:12000])
        commands, delay, logs, done = (frame.get(key) for key in ("commands", "nextDelayMs", "logs", "done"))
        if (frame.get("type") != "batch" or not isinstance(commands, list) or len(commands) > 256
                or type(delay) not in (int, float) or not 0 <= delay <= 9007199254740991
                or not isinstance(logs, list) or len(logs) > 256 or type(done) is not bool):
            raise RunnerError("RUNNER_PROTOCOL", "Sandboxen afleverede en ugyldig kommandobatch.")
        clean_commands = []
        for command in commands:
            if (not isinstance(command, dict) or not isinstance(command.get("alias"), str)
                    or not ALIAS.fullmatch(command["alias"]) or command.get("action") != "setPower"
                    or type(command.get("value")) is not bool):
                raise RunnerError("INVALID_VALUE", "En kommando matchede ikke den tilladte enhedsmodel.")
            alias = command["alias"]
            kind = self.aliases.get(alias)
            if kind == "uninstalled":
                raise RunnerError("DEVICE_NOT_INSTALLED", f"Enheden {alias} ligger på lager.")
            if kind == "locked":
                raise RunnerError("CAPABILITY_LOCKED", f"Enheden {alias} er endnu låst.")
            if kind is None:
                raise RunnerError("UNKNOWN_ALIAS", f"Aliaset {alias} findes ikke i boligen.")
            if kind != "lamp":
                raise RunnerError("INVALID_TYPE", f"Enheden {alias} kan ikke styres som en lampe.")
            clean_commands.append({"alias": alias, "action": "setPower", "value": command["value"]})
        clean_logs = []
        activation_log_bytes = 0
        for log in logs:
            if not isinstance(log, str):
                raise RunnerError("RUNNER_PROTOCOL", "En logbesked var ikke tekst.")
            remaining = min(LOG_BYTES - self.log_bytes, ACTIVATION_LOG_BYTES - activation_log_bytes)
            if remaining <= 0:
                break
            clipped = log.encode()[:remaining].decode(errors="ignore")
            size = len(clipped.encode()) + 1
            activation_log_bytes += size
            self.log_bytes += size
            clean_logs.append(clipped)
        return {"commands": clean_commands, "nextDelayMs": math.ceil(delay), "logs": clean_logs, "done": done}

    def stop(self):
        self.closed.set()
        with self.process_lock:
            if self.cleaned:
                return True
            try:
                removal = subprocess.run(["docker", "rm", "-f", self.container],
                                         timeout=3, check=False, capture_output=True, text=True, env=DOCKER_ENV)
                self.cleaned = removal.returncode == 0 or "No such container" in removal.stderr
            except (OSError, subprocess.TimeoutExpired):
                pass
            if self.process is not None:
                try:
                    self.process.wait(timeout=0.5)
                except subprocess.TimeoutExpired:
                    self.process.kill()
                    self.process.wait()
                self.process.stdin.close()
                self.process.stdout.close()
                self.process = None
            return self.cleaned


class Worker(ThreadingHTTPServer):
    daemon_threads = True

    def __init__(self, address, token, capacity):
        super().__init__(address, Handler)
        self.token = "Bearer " + token
        self.capacity = capacity
        self.sessions = {}
        self.stopped = {}
        self.lock = threading.Lock()
        self.health_lock = threading.Lock()
        self.health_at = None
        self.healthy = False

    def add(self, session):
        with self.lock:
            if session.id in self.stopped:
                raise RunnerError("RUN_CANCELLED", "Forsøget blev stoppet.")
            if session.id in self.sessions:
                raise RunnerError("INVALID_RUN", "Sessions-ID findes allerede på runner-værten.")
            if len(self.sessions) >= self.capacity:
                raise RunnerError("RUNNER_BUSY", "Alle runnerpladser er optaget. Prøv igen om lidt.")
            self.sessions[session.id] = session

    def stop(self, session_id):
        with self.lock:
            session = self.sessions.get(session_id)
            self.stopped[session_id] = time.monotonic()
            if session:
                session.closed.set()
        if session and session.stop():
            with self.lock:
                if self.sessions.get(session_id) is session:
                    del self.sessions[session_id]
        return not session or session.cleaned

    def reap(self):
        while True:
            time.sleep(5)
            with self.lock:
                now = time.monotonic()
                self.stopped = {session_id: timestamp for session_id, timestamp in self.stopped.items()
                                if now - timestamp < STOP_TOMBSTONE_SECONDS}
                expired = [session_id for session_id, session in self.sessions.items()
                           if session.closed.is_set() or session.expired(now)]
                for session_id in expired:
                    self.sessions[session_id].closed.set()
            for session_id in expired:
                self.stop(session_id)

    def touch(self, session_id):
        with self.lock:
            session = self.sessions.get(session_id)
            now = time.monotonic()
            if session is None or not session.ready or session.done or session.closed.is_set() or session.expired(now):
                return False
            session.touched = now
            return True

    def check_health(self):
        with self.health_lock:
            if self.health_at is not None and time.monotonic() - self.health_at < 10:
                return self.healthy
            try:
                host = json.loads(subprocess.check_output(
                    ["docker", "info", "--format={{json .}}"],
                    timeout=1.2, stderr=subprocess.DEVNULL, env=DOCKER_ENV,
                ))
                if (host.get("OSType") != "linux" or host.get("CgroupVersion") != "2"
                        or "runsc" not in (host.get("Runtimes") or {})
                        or not all(host.get(limit) is True for limit in
                                   ("CpuCfsQuota", "MemoryLimit", "SwapLimit", "PidsLimit"))):
                    raise ValueError
                subprocess.run(
                    ["docker", "image", "inspect", *[f"lup-home-{name}:sdk{SDK_VERSION}" for name in PROFILES]],
                    timeout=0.5, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, env=DOCKER_ENV,
                )
                self.healthy = True
            except (OSError, subprocess.SubprocessError, ValueError):
                self.healthy = False
            self.health_at = time.monotonic()
            return self.healthy


class Handler(BaseHTTPRequestHandler):
    server_version = "LupHomeRunner/1"

    def authorized(self):
        value = self.headers.get("Authorization", "")
        if hmac.compare_digest(value.encode(), self.server.token.encode()):
            return True
        self.respond(401, {"error": {"code": "UNAUTHORIZED", "message": "Runneradgang afvist."}})
        return False

    def read_json(self, limit):
        self.connection.settimeout(10)
        try:
            length = int(self.headers.get("Content-Length", "0"))
            if not 0 < length <= limit:
                raise ValueError
            frame = json.loads(self.rfile.read(length))
            if not isinstance(frame, dict):
                raise ValueError
            return frame
        except (ValueError, TimeoutError, RecursionError) as error:
            raise RunnerError("RUNNER_PROTOCOL", "Anmodningen havde ugyldigt format eller omfang.") from error

    def respond(self, status, body):
        payload = encode(body)
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def do_GET(self):
        if not self.authorized():
            return
        if self.path == "/health":
            if not self.server.check_health():
                self.respond(503, {"status": "unavailable"})
                return
            self.respond(200, {"status": "ready", "sdkVersion": SDK_VERSION})
            return
        match = re.fullmatch(r"/sessions/([a-f0-9-]{36})", self.path)
        if match and self.server.touch(match[1]):
            self.send_response(204)
            self.send_header("Cache-Control", "no-store")
            self.end_headers()
            return
        self.respond(404, {})

    def do_POST(self):
        if not self.authorized():
            return
        if self.path == "/sessions":
            try:
                request = self.read_json(800 * 1024)
                run, session_id = validate_start(request)
                if not self.server.check_health():
                    raise RunnerError("RUNNER_UNAVAILABLE", "Runner-værten mangler gVisor, kvoter eller sprogimages.")
                session = Session(run, session_id, request)
                self.server.add(session)
                try:
                    session.start(request)
                    self.respond(200, {"sessionId": session_id})
                except (RunnerError, OSError):
                    self.server.stop(session_id)
                    raise
            except RunnerError as error:
                self.respond(200, {"sessionId": None, "error": error.json()})
            return
        match = re.fullmatch(r"/sessions/([a-f0-9-]{36})/activate", self.path)
        if not match:
            self.respond(404, {})
            return
        session_id = match[1]
        request = {}
        session = None
        try:
            request = self.read_json(48 * 1024)
            with self.server.lock:
                session = self.server.sessions.get(session_id)
            if session is None:
                raise RunnerError("RUN_EXPIRED", "Kørslen er ikke længere aktiv.")
            result = session.activate(request)
        except RunnerError as error:
            if session and error.code != "INVALID_ACTIVATION":
                self.server.stop(session_id)
            result = {"commands": [], "nextDelayMs": 0, "logs": [], "done": False, "error": error.json()}
        self.respond(200, {**{key: request.get(key) for key in ("runId", "controllerId", "tick", "sequence")}, **result})

    def do_DELETE(self):
        if not self.authorized():
            return
        match = re.fullmatch(r"/sessions/([a-f0-9-]{36})", self.path)
        if match:
            removed = self.server.stop(match[1])
            self.respond(200 if removed else 202, {"status": "closed" if removed else "closing"})
        else:
            self.respond(404, {})


def main():
    token = os.environ["HOME_RUNNER_TOKEN"]
    if len(token) < 32 or not token.isascii():
        raise ValueError("HOME_RUNNER_TOKEN must contain at least 32 ASCII characters.")
    capacity = int(os.environ.get("HOME_RUNNER_CAPACITY", "8"))
    if capacity < 1:
        raise ValueError("HOME_RUNNER_CAPACITY must be positive.")
    # This service exclusively owns the labelled sandboxes on its dedicated
    # worker daemon. A restart reconciles stopped and orphaned containers.
    containers = subprocess.check_output(
        ["docker", "ps", "-aq", "--filter", "label=" + CONTAINER_LABEL],
        text=True, timeout=10, env=DOCKER_ENV,
    ).split()
    if containers:
        subprocess.run(["docker", "rm", "-f", *containers], timeout=15, check=True, env=DOCKER_ENV)
    worker = Worker((os.environ.get("HOME_RUNNER_BIND", "127.0.0.1"),
                     int(os.environ.get("HOME_RUNNER_PORT", "8096"))), token, capacity)
    threading.Thread(target=worker.reap, daemon=True).start()

    def shutdown(_signum, _frame):
        raise KeyboardInterrupt

    signal.signal(signal.SIGTERM, shutdown)
    try:
        worker.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        worker.server_close()
        for session_id in list(worker.sessions):
            worker.stop(session_id)


if __name__ == "__main__":
    main()
