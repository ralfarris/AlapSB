import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
EXE = os.path.join(ROOT, "dist", "AlapSB.exe")
BUILD = os.path.join(ROOT, "build.ps1")


def main():
    if not os.path.exists(EXE) or "--build" in sys.argv:
        print("Membangun AlapSB.exe...")
        subprocess.check_call(
            ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", BUILD],
            cwd=ROOT,
        )
    print(f"Menjalankan {EXE}")
    subprocess.Popen([EXE], cwd=os.path.dirname(EXE))


if __name__ == "__main__":
    main()
