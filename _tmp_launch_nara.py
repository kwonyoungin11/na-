import os
import subprocess
import sys
import time

CREATE_BREAKAWAY_FROM_JOB = 0x01000000
CREATE_NEW_PROCESS_GROUP = 0x00000200

exe = r"A:\c\cc-feature-implementer\desktop\NaraCollector.Desktop\bin\Debug\net8.0-windows10.0.19041.0\동국문화재연구원_낙찰분석프로그램.exe"
cwd = os.path.dirname(exe)

if not os.path.isfile(exe):
    raise SystemExit(f"exe not found: {exe}")

flags = CREATE_BREAKAWAY_FROM_JOB | CREATE_NEW_PROCESS_GROUP
try:
    proc = subprocess.Popen([exe], cwd=cwd, close_fds=True, creationflags=flags)
    print(f"launched_breakaway pid={proc.pid}")
except OSError as exc:
    print(f"breakaway_failed {exc}")
    proc = subprocess.Popen([exe], cwd=cwd, close_fds=True, creationflags=CREATE_NEW_PROCESS_GROUP)
    print(f"launched_fallback pid={proc.pid}")

time.sleep(2)
alive = proc.poll() is None
print(f"alive_after_2s={alive} returncode={proc.poll()}")
sys.exit(0 if alive else 1)
