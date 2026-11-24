"""Compiles one source file inside the sandbox and starts its actual runtime."""

import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import traceback

ROOT = Path(__file__).resolve().parent


def emit(frame):
    print(json.dumps(frame, ensure_ascii=True, allow_nan=False, separators=(",", ":")), flush=True)


def compile_program(command):
    with open("/work/diagnostics.txt", "w+b") as diagnostics:
        status = subprocess.run(command, stdin=subprocess.DEVNULL,
                                stdout=diagnostics, stderr=subprocess.STDOUT, check=False).returncode
        if status == 0:
            return True
        diagnostics.seek(0)
        message = diagnostics.read(12_000).decode(errors="replace").strip() or "Kompileringen fejlede."
    emit({"type": "error", "message": message})
    return False


def start_python(source):
    import home

    try:
        student = compile(source, "program.py", "exec")
    except SyntaxError:
        emit({"type": "error", "message": traceback.format_exc(limit=0)})
        return
    emit({"type": "ready"})
    home._run(student)


def start_csharp(source):
    project = Path("/work/Student.csproj")
    shutil.copyfile(ROOT / "csharp" / "Student.csproj", project)
    Path("/work/Program.cs").write_text(source, encoding="utf-8")
    if compile_program([
        "dotnet", "build", str(project), "-c", "Release", "--disable-build-servers",
        "--configfile", str(ROOT / "csharp" / "NuGet.Config"), "-o", "/work/compiled",
        "-p:UseSharedCompilation=false", f"-p:LupSdkAssembly={ROOT / 'bootstrap' / 'HomeRunner.dll'}",
    ]):
        emit({"type": "ready"})
        os.execvp("dotnet", ["dotnet", str(ROOT / "bootstrap" / "HomeRunner.dll"), "/work/compiled/Student.dll"])


def start_arduino(source):
    program = Path("/work/sketch.cpp")
    program.write_text(source, encoding="utf-8")
    if compile_program([
        "g++", "-std=c++17", "-O1", "-fmax-errors=12", "-I", str(ROOT / "arduino"),
        str(program), str(ROOT / "VirtualHome.o"), "-o", "/work/sketch",
    ]):
        emit({"type": "ready"})
        os.execv("/work/sketch", ["/work/sketch"])


def main():
    # Read one byte at a time so no stdin from the activation protocol is lost
    # when exec replaces the compiler launcher. The source frame has a hard cap.
    data = bytearray()
    while len(data) <= 800 * 1024:
        char = os.read(0, 1)
        if not char or char == b"\n":
            break
        data.extend(char)
    start = json.loads(data)
    if start.get("type") != "start" or not isinstance(start.get("source"), str):
        raise ValueError("Invalid start frame.")
    source = start["source"]
    {"python": start_python, "csharp": start_csharp, "arduino": start_arduino}[sys.argv[1]](source)


if __name__ == "__main__":
    main()
