"""Hjem.exe Python SDK, version 1."""

import io
import json
import math
import sys
import traceback

_input = sys.stdin.buffer
_output = sys.stdout.buffer
_snapshot = None
_commands = []
_logs = []
_log_bytes = 0
_fault = None


class HomeError(Exception):
    def __init__(self, code, message):
        super().__init__(message)
        self.code = code


def _fail(code, message):
    global _fault
    if _fault is None:
        _fault = HomeError(code, message)
    raise _fault


def _kind(alias):
    if not isinstance(alias, str):
        _fail("INVALID_TYPE", "Enhedens alias skal være tekst.")
    kind = _snapshot["aliases"].get(alias)
    if kind is None:
        _fail("UNKNOWN_ALIAS", f"Aliaset {alias!r} findes ikke i boligen.")
    if kind == "uninstalled":
        _fail("DEVICE_NOT_INSTALLED", f"Enheden {alias!r} ligger på lager.")
    if kind == "locked":
        _fail("CAPABILITY_LOCKED", f"Enheden {alias!r} er endnu låst.")
    return kind


def read(alias):
    """Read a number or boolean from the current activation's sensor snapshot."""
    kind = _kind(alias)
    if kind not in ("daylight", "presence", "switch", "brightness", "light"):
        _fail("INVALID_TYPE", f"Enheden {alias!r} kan ikke aflæses som sensor.")
    if alias not in _snapshot["values"]:
        _fail("CAPABILITY_LOCKED", f"Sensoren {alias!r} er ikke tilgængelig i dette forsøg.")
    return _snapshot["values"][alias]


def set(alias, value):
    """Queue a lamp power command for this activation."""
    if _kind(alias) != "lamp" or type(value) is not bool:
        _fail("INVALID_TYPE", "En lampe kan kun tændes eller slukkes med True eller False.")
    if len(_commands) >= 256:
        _fail("INVALID_VALUE", "En aktivering kan højst indeholde 256 kommandoer.")
    _commands.append({"alias": alias, "action": "setPower", "value": value})


def wait(seconds):
    """Commit pending commands and wait for simulated seconds."""
    if type(seconds) not in (int, float) or not 0 <= seconds <= 9007199254740:
        _fail("INVALID_VALUE", "Ventetiden skal være et endeligt tal på mindst nul.")
    _yield(math.ceil(seconds * 1000), False)
    _receive()


def now_ms():
    """Return the authoritative time of this activation in milliseconds."""
    return _snapshot["timeMs"]


def log(value):
    """Append plain text to the controller log."""
    global _log_bytes
    remaining = min(16384 - _log_bytes, 2048)
    if remaining <= 0 or len(_logs) >= 256:
        return
    line = str(value)[:remaining].encode(errors="replace")[:remaining].decode(errors="ignore")
    _logs.append(line)
    _log_bytes += len(line.encode()) + 1


class _LogWriter(io.TextIOBase):
    def write(self, text):
        if text.strip("\r\n"):
            log(text)
        return len(text)

    def flush(self):
        pass


def _emit(frame):
    # Keep UTF-8 printable while correctly escaping control characters.
    if _snapshot is not None:
        frame["activation"] = _snapshot.get("activation")
    _output.write((json.dumps(frame, ensure_ascii=False, allow_nan=False, separators=(",", ":")) + "\n").encode())
    _output.flush()


def _receive():
    global _snapshot
    payload = _input.readline()
    if not payload:
        raise EOFError("Simulatorforbindelsen er lukket.")
    _snapshot = json.loads(payload)
    if _snapshot.get("type") != "snapshot":
        raise ValueError("Ugyldigt sensorøjebliksbillede.")


def _yield(delay, done):
    global _log_bytes
    if _fault is not None:
        raise _fault
    _emit({"type": "batch", "commands": _commands, "nextDelayMs": delay, "logs": _logs, "done": done})
    _commands.clear()
    _logs.clear()
    _log_bytes = 0


def _run(student):
    sys.stdout = sys.stderr = _LogWriter()
    sys.stdin = io.StringIO()
    try:
        _receive()
        exec(student, {"__name__": "__main__", "__file__": "program.py"})
        _yield(0, True)
    except BaseException as error:
        fault = _fault if _fault is not None else error
        _emit({
            "type": "error",
            "code": fault.code if isinstance(fault, HomeError) else "RUNTIME_ERROR",
            "message": "".join(traceback.format_exception(fault, limit=8))[-12000:],
        })
